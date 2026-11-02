using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost;

public sealed class ProductionCostService : IProductionCostService
{
    private static readonly (int Index, string Code, string Name, string Engine, string? BiCode)[] StepCatalog =
    {
        (1, ProductionCostStepCode.FaPrepaidLock, "Khóa TSCĐ / CP trả trước", ProductionCostEngine.Bi, ProductionCostStepCode.FaPrepaidLock),
        (2, ProductionCostStepCode.CostAllocation, "Phân bổ 622 / 627", ProductionCostEngine.Gk, null),
        (3, ProductionCostStepCode.CogsSummary, "Kết chuyển chi phí (EXP_TO_154_ONLY)", ProductionCostEngine.Bi, ProductionCostStepCode.CogsSummary),
        (4, ProductionCostStepCode.CostCalc, "Tính dở dang / giá thành", ProductionCostEngine.Gk, null),
        (5, ProductionCostStepCode.Valuation, "Cập nhật giá nhập kho TP", ProductionCostEngine.Gk, null),
        (6, ProductionCostStepCode.ProfitLossReport, "Báo cáo lãi lỗ", ProductionCostEngine.Bi, ProductionCostStepCode.ProfitLossReport),
    };

    private readonly IProductionCostRepository _repo;

    public ProductionCostService(IProductionCostRepository repo)
    {
        _repo = repo;
    }

    public async Task<ProductionCostPeriodDto> GetPeriodAsync(string companyCd, string periodYm, string? databaseName = null)
    {
        EnsureCompany(companyCd);
        var err = ProductionCostValidation.ValidateQuery(periodYm, null, null);
        if (err != null)
            throw new ArgumentException(err);

        if (!await _repo.IsSchemaAvailableAsync(databaseName))
        {
            return new ProductionCostPeriodDto
            {
                SchemaAvailable = false,
                PeriodExists = false,
                CompanyCd = companyCd,
                PeriodYm = periodYm,
                Message = "Schema production_cost_* chưa có trên database (chờ migration 2B DEV)."
            };
        }

        var period = await _repo.GetPeriodRowAsync(companyCd, periodYm, databaseName);
        if (period == null)
        {
            return new ProductionCostPeriodDto
            {
                SchemaAvailable = true,
                PeriodExists = false,
                CompanyCd = companyCd,
                PeriodYm = periodYm,
                CostEngine = ProductionCostEngine.None,
                Message = "Kỳ G-K chưa được tạo."
            };
        }

        period.SchemaAvailable = true;
        period.PeriodExists = true;
        period.CurrentRun = await _repo.GetCurrentRunAsync(companyCd, periodYm, databaseName);
        return period;
    }

    public async Task<ProductionCostStepsResponse> GetStepsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName);
        var response = new ProductionCostStepsResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.DetailVersion
        };

        if (!ctx.SchemaAvailable || !ctx.PeriodExists)
            return response;

        IReadOnlyDictionary<string, string> biStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            biStatuses = await _repo.GetBiStepStatusesAsync(companyCd, query.PeriodYm, databaseName);
        }
        catch
        {
            // acc_period_lock_step luôn có trên PeriodLock; nếu lỗi đọc, để trống BiStatus
        }

        var gkSteps = ctx.Run != null
            ? await _repo.GetGkStepsAsync(companyCd, ctx.Run.RunId, databaseName)
            : Array.Empty<ProductionCostStepDto>();
        var gkMap = gkSteps.ToDictionary(s => s.StepIndex);

        var steps = new List<ProductionCostStepDto>();
        foreach (var def in StepCatalog)
        {
            gkMap.TryGetValue(def.Index, out var stored);
            string? biStatus = null;
            if (!string.IsNullOrEmpty(def.BiCode) && biStatuses.TryGetValue(def.BiCode, out var st))
                biStatus = st;

            var status = stored?.Status;
            if (string.IsNullOrWhiteSpace(status))
            {
                // B1/B3/B6: ưu tiên trạng thái B-I khi chưa có dòng G-K
                status = !string.IsNullOrWhiteSpace(biStatus)
                    ? MapBiStatusToGk(biStatus!)
                    : ProductionCostStepStatus.NotStarted;
            }

            string? previous = null;
            if (ctx.Run != null && string.Equals(status, ProductionCostStepStatus.Reopened, StringComparison.OrdinalIgnoreCase))
            {
                previous = await _repo.GetPreviousConfirmedSummaryAsync(companyCd, ctx.Run.RunId, def.Index, databaseName)
                    ?? stored?.ResultSummary;
            }

            steps.Add(new ProductionCostStepDto
            {
                StepIndex = def.Index,
                StepCode = stored?.StepCode ?? (def.Index == 3 ? ProductionCostStepCode.ExpTo154Only : def.Code),
                StepName = def.Name,
                Engine = stored?.Engine ?? def.Engine,
                Status = status!,
                BiStepCode = stored?.BiStepCode ?? def.BiCode,
                BiStatus = biStatus,
                ResultSummary = stored?.ResultSummary,
                CompletedBy = stored?.CompletedBy,
                CompletedAt = stored?.CompletedAt,
                PreviousConfirmedSummary = previous
            });
        }

        response.Steps = steps;
        return response;
    }

    public async Task<ProductionCostObjectsResponse> GetObjectsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName);
        var response = new ProductionCostObjectsResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId
        };
        if (ctx.Run == null)
            return response;

        response.Items = await _repo.GetObjectsAsync(companyCd, ctx.Run.RunId, databaseName);
        return response;
    }

    public async Task<ProductionCostAllocPoolResponse> GetAllocPoolAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName);
        var response = new ProductionCostAllocPoolResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.DetailVersion,
            AllocateType = ctx.Run?.AllocateType
        };
        if (ctx.Run == null || ctx.DetailVersion is null)
            return response;

        response.Items = await _repo.GetAllocPoolAsync(companyCd, ctx.Run.RunId, ctx.DetailVersion.Value, databaseName);
        return response;
    }

    public async Task<ProductionCostAllocBasisResponse> GetAllocBasisAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName);
        var response = new ProductionCostAllocBasisResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.DetailVersion,
            AllocateType = ctx.Run?.AllocateType
        };
        if (ctx.Run == null || ctx.DetailVersion is null)
            return response;

        var version = ctx.DetailVersion.Value;
        response.NvlBasis = await _repo.GetNvlBasisAsync(companyCd, ctx.Run.RunId, version, databaseName);
        response.NvlBasisLines = await _repo.GetNvlBasisLinesAsync(companyCd, ctx.Run.RunId, version, databaseName);
        response.Orphans = await _repo.GetNvlOrphansAsync(companyCd, ctx.Run.RunId, version, databaseName);

        // allocate_type=1: tiêu thức SL từ alloc_detail (không tính lại)
        var details = await _repo.GetAllocDetailAsync(companyCd, ctx.Run.RunId, version, databaseName);
        response.TpBasis = details
            .GroupBy(d => new { d.ProductId, d.DepartmentId, d.ProductCd, d.DepartmentCd })
            .Select(g => new ProductionCostTpBasisDto
            {
                ProductId = g.Key.ProductId,
                DepartmentId = g.Key.DepartmentId,
                ProductCd = g.Key.ProductCd,
                DepartmentCd = g.Key.DepartmentCd,
                Quantity = g.Max(x => x.Quantity)
            })
            .OrderBy(x => x.ProductId)
            .ToList();

        return response;
    }

    public async Task<ProductionCostAllocDetailResponse> GetAllocDetailAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName);
        var response = new ProductionCostAllocDetailResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.DetailVersion,
            AllocateType = ctx.Run?.AllocateType
        };
        if (ctx.Run == null || ctx.DetailVersion is null)
            return response;

        response.Items = await _repo.GetAllocDetailAsync(companyCd, ctx.Run.RunId, ctx.DetailVersion.Value, databaseName);
        return response;
    }

    public async Task<ProductionCostWipResponse> GetWipAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName);
        var response = new ProductionCostWipResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.DetailVersion,
            WipMethod = ctx.Run?.WipMethod,
            RunStatus = ctx.Run?.Status
        };
        if (ctx.Run == null || ctx.DetailVersion is null)
            return response;

        var version = ctx.DetailVersion.Value;
        response.Items = await _repo.GetWipAsync(companyCd, ctx.Run.RunId, version, databaseName);
        // Nguồn chi phí đã loại EXP_TO_154 — đọc production_cost_source, không cộng LOCK
        response.Sources = await _repo.GetSourcesAsync(companyCd, ctx.Run.RunId, version, databaseName);
        return response;
    }

    public async Task<ProductionCostReceiptsResponse> GetReceiptsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName);
        var response = new ProductionCostReceiptsResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.DetailVersion
        };
        if (ctx.Run == null || ctx.DetailVersion is null)
            return response;

        response.Items = await _repo.GetReceiptsAsync(companyCd, ctx.Run.RunId, ctx.DetailVersion.Value, databaseName);
        return response;
    }

    public async Task<ProductionCostHistoryResponse> GetHistoryAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: false);
        var response = new ProductionCostHistoryResponse
        {
            SchemaAvailable = ctx.SchemaAvailable,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = query.RunId ?? ctx.Run?.RunId
        };
        if (!ctx.SchemaAvailable)
            return response;

        response.Items = await _repo.GetHistoryAsync(companyCd, query.PeriodYm, query.RunId ?? ctx.Run?.RunId, databaseName);
        return response;
    }

    public async Task<ProductionCostComparisonResponse> GetComparisonAsync(
        string companyCd,
        string periodYm,
        long? baseRunId,
        int? baseRunVersion,
        long? compareRunId,
        int? compareRunVersion,
        string? databaseName = null)
    {
        EnsureCompany(companyCd);
        var err = ProductionCostValidation.ValidateQuery(periodYm, baseRunId, baseRunVersion);
        if (err != null)
            throw new ArgumentException(err);
        err = ProductionCostValidation.ValidateQuery(periodYm, compareRunId, compareRunVersion);
        if (err != null)
            throw new ArgumentException(err);

        var response = new ProductionCostComparisonResponse { PeriodYm = periodYm };

        if (!await _repo.IsSchemaAvailableAsync(databaseName))
        {
            response.SchemaAvailable = false;
            response.Message = "Schema production_cost_* chưa có trên database (chờ migration 2B DEV).";
            return response;
        }

        response.SchemaAvailable = true;
        var period = await _repo.GetPeriodRowAsync(companyCd, periodYm, databaseName);
        response.PeriodExists = period != null;
        if (period == null)
        {
            response.Message = "Kỳ G-K chưa được tạo.";
            return response;
        }

        var baseRun = await _repo.ResolveRunAsync(companyCd, periodYm, baseRunId, baseRunVersion, databaseName);
        var compareRun = await _repo.ResolveRunAsync(companyCd, periodYm, compareRunId, compareRunVersion, databaseName);
        if (baseRun == null || compareRun == null)
        {
            response.Message = "Không tìm thấy RUN để so sánh.";
            return response;
        }

        var baseVer = ProductionCostValidation.ResolveDetailVersion(baseRunVersion, baseRun.RunVersion);
        var compareVer = ProductionCostValidation.ResolveDetailVersion(compareRunVersion, compareRun.RunVersion);

        response.BaseRunId = baseRun.RunId;
        response.BaseRunVersion = baseVer;
        response.CompareRunId = compareRun.RunId;
        response.CompareRunVersion = compareVer;
        response.Lines = await _repo.GetComparisonLinesAsync(
            companyCd, baseRun.RunId, baseVer, compareRun.RunId, compareVer, databaseName);
        return response;
    }

    private async Task<RunContext> ResolveContextAsync(
        string companyCd,
        ProductionCostQuery query,
        string? databaseName,
        bool requireRun = true)
    {
        EnsureCompany(companyCd);
        var err = ProductionCostValidation.ValidateQuery(query.PeriodYm, query.RunId, query.RunVersion);
        if (err != null)
            throw new ArgumentException(err);

        if (!await _repo.IsSchemaAvailableAsync(databaseName))
        {
            return new RunContext(false, false, null, null);
        }

        var period = await _repo.GetPeriodRowAsync(companyCd, query.PeriodYm, databaseName);
        if (period == null)
            return new RunContext(true, false, null, null);

        var run = await _repo.ResolveRunAsync(companyCd, query.PeriodYm, query.RunId, query.RunVersion, databaseName);
        if (requireRun && run == null)
            return new RunContext(true, true, null, null);

        int? detailVersion = run == null
            ? null
            : ProductionCostValidation.ResolveDetailVersion(query.RunVersion, run.RunVersion);

        return new RunContext(true, true, run, detailVersion);
    }

    private static void EnsureCompany(string companyCd)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new InvalidOperationException("COMPANY_CD is required from authentication context.");
    }

    private static string MapBiStatusToGk(string biStatus) =>
        biStatus.ToUpperInvariant() switch
        {
            "DONE" => ProductionCostStepStatus.Done,
            "PROCESSING" => ProductionCostStepStatus.Processing,
            "ERROR" => ProductionCostStepStatus.Error,
            "OPEN" => ProductionCostStepStatus.NotStarted,
            _ => biStatus
        };

    private sealed record RunContext(
        bool SchemaAvailable,
        bool PeriodExists,
        ProductionCostRunDto? Run,
        int? DetailVersion);
}

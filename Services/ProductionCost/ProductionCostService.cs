using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost;

public sealed class ProductionCostService : IProductionCostService
{
    /// <summary>
    /// Catalog cố định 6 bước G-K.
    /// StepCode = DDL production_cost_step; BiStepCode = PeriodLock; CogsRule chỉ trên period.
    /// Bước 3: StepCode=EXP_TO_154_ONLY, BiStepCode=COGS_SUMMARY (không đổi khi có/không có dữ liệu).
    /// </summary>
    private static readonly (int Index, string Code, string Name, string Engine, string? BiCode)[] StepCatalog =
    {
        (1, ProductionCostStepCode.FaPrepaidLock, "Khóa TSCĐ / CP trả trước", ProductionCostEngine.Bi, ProductionCostBiStepCode.FaPrepaidLock),
        (2, ProductionCostStepCode.CostAllocation, "Phân bổ 622 / 627", ProductionCostEngine.Gk, null),
        (3, ProductionCostStepCode.ExpTo154Only, "Kết chuyển chi phí (EXP_TO_154_ONLY / COGS_SUMMARY)", ProductionCostEngine.Bi, ProductionCostBiStepCode.CogsSummary),
        (4, ProductionCostStepCode.CostCalc, "Tính dở dang / giá thành", ProductionCostEngine.Gk, null),
        (5, ProductionCostStepCode.Valuation, "Cập nhật giá nhập kho TP", ProductionCostEngine.Gk, null),
        (6, ProductionCostStepCode.ProfitLossReport, "Báo cáo lãi lỗ", ProductionCostEngine.Bi, ProductionCostBiStepCode.ProfitLossReport),
    };

    private readonly IProductionCostRepository _repo;

    public ProductionCostService(IProductionCostRepository repo)
    {
        _repo = repo;
    }

    public async Task<ProductionCostPeriodDto> GetPeriodAsync(string companyCd, string periodYm, string? databaseName = null)
    {
        EnsureCompany(companyCd);
        EnsureDatabase(databaseName);
        var err = ProductionCostValidation.ValidateQuery(periodYm, null, null);
        if (err != null)
            throw new ArgumentException(err);

        var probe = await _repo.ProbeSchemaAsync(databaseName);
        if (!probe.IsReady)
        {
            return new ProductionCostPeriodDto
            {
                SchemaAvailable = false,
                SchemaStatus = probe.Status,
                MissingTables = probe.MissingTables,
                PeriodExists = false,
                CompanyCd = companyCd,
                PeriodYm = periodYm,
                Message = SchemaMessage(probe)
            };
        }

        var period = await _repo.GetPeriodRowAsync(companyCd, periodYm, databaseName);
        if (period == null)
        {
            return new ProductionCostPeriodDto
            {
                SchemaAvailable = true,
                SchemaStatus = ProductionCostSchemaStatus.Ready,
                PeriodExists = false,
                CompanyCd = companyCd,
                PeriodYm = periodYm,
                CostEngine = ProductionCostEngine.None,
                Message = "Kỳ G-K chưa được tạo."
            };
        }

        period.SchemaAvailable = true;
        period.SchemaStatus = ProductionCostSchemaStatus.Ready;
        period.PeriodExists = true;
        period.CurrentRun = await _repo.GetCurrentRunAsync(companyCd, periodYm, databaseName);
        return period;
    }

    public async Task<ProductionCostStepsResponse> GetStepsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: false, loadVersionInfo: false);
        var response = new ProductionCostStepsResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            MissingTables = ctx.Schema.MissingTables.Count > 0 ? ctx.Schema.MissingTables : null,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.Run?.RunVersion,
            CogsRule = ctx.PeriodCogsRule,
            Message = ctx.Message
        };

        // GET steps luôn trả catalog 6 bước — không tạo bản ghi DB.
        IReadOnlyDictionary<string, string> biStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var biQueryFailed = false;
        string? biQueryError = null;

        // PeriodLock độc lập schema G-K — vẫn đọc khi schema thiếu, trừ khi caller không có DB.
        try
        {
            biStatuses = await _repo.GetBiStepStatusesAsync(companyCd, query.PeriodYm, databaseName);
        }
        catch (Exception ex)
        {
            biQueryFailed = true;
            biQueryError = ex.Message;
        }

        IReadOnlyList<ProductionCostStepDto> gkSteps = Array.Empty<ProductionCostStepDto>();
        if (ctx.Schema.IsReady && ctx.Run != null)
            gkSteps = await _repo.GetGkStepsAsync(companyCd, ctx.Run.RunId, databaseName);

        var gkMap = gkSteps.ToDictionary(s => s.StepIndex);
        var steps = new List<ProductionCostStepDto>(StepCatalog.Length);

        foreach (var def in StepCatalog)
        {
            gkMap.TryGetValue(def.Index, out var stored);
            string? biStatus = null;
            if (!string.IsNullOrEmpty(def.BiCode))
            {
                if (biQueryFailed)
                {
                    // Không coi lỗi truy vấn B-I như NOT_STARTED / OPEN.
                    throw new InvalidOperationException(
                        $"Không đọc được trạng thái B-I (acc_period_lock_step) cho bước {def.BiCode}: {biQueryError}");
                }

                biStatuses.TryGetValue(def.BiCode, out biStatus);
            }

            string status;
            var mismatch = false;
            if (!string.IsNullOrEmpty(def.BiCode))
            {
                (status, mismatch) = ProductionCostValidation.ResolveBiBackedStatus(stored?.Status, biStatus);
            }
            else
            {
                status = string.IsNullOrWhiteSpace(stored?.Status)
                    ? ProductionCostStepStatus.NotStarted
                    : stored!.Status;
            }

            string? previous = null;
            if (ctx.Run != null &&
                string.Equals(status, ProductionCostStepStatus.Reopened, StringComparison.OrdinalIgnoreCase))
            {
                previous = await _repo.GetPreviousConfirmedSummaryAsync(companyCd, ctx.Run.RunId, def.Index, databaseName)
                    ?? stored?.ResultSummary;
            }

            // StepCode / BiStepCode luôn theo catalog — không đổi khi chưa có dòng G-K.
            steps.Add(new ProductionCostStepDto
            {
                StepIndex = def.Index,
                StepCode = def.Code,
                StepName = def.Name,
                Engine = def.Engine,
                Status = status,
                BiStepCode = def.BiCode,
                BiStatus = biStatus,
                StatusMismatch = mismatch,
                ResultSummary = stored?.ResultSummary,
                CompletedBy = stored?.CompletedBy,
                CompletedAt = stored?.CompletedAt,
                PreviousConfirmedSummary = previous
            });
        }

        response.Steps = steps;
        if (!ctx.PeriodExists && string.IsNullOrEmpty(response.Message))
            response.Message = ctx.Schema.IsReady ? "Kỳ G-K chưa được tạo." : SchemaMessage(ctx.Schema);

        return response;
    }

    public async Task<ProductionCostObjectsResponse> GetObjectsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: true, loadVersionInfo: false);
        var response = new ProductionCostObjectsResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            Message = ctx.Message
        };
        if (ctx.Run == null)
            return response;

        response.Items = await _repo.GetObjectsAsync(companyCd, ctx.Run.RunId, databaseName);
        return response;
    }

    public async Task<ProductionCostAllocPoolResponse> GetAllocPoolAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: true, loadVersionInfo: true);
        var response = new ProductionCostAllocPoolResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.VersionInfo?.RunVersion,
            RunVersionExists = ctx.VersionInfo?.VersionExists,
            HasDetailData = ctx.VersionInfo?.HasDetailData,
            AllocateType = ctx.Run?.AllocateType,
            Message = ctx.Message ?? ctx.VersionInfo?.Message
        };
        if (ctx.Run == null || ctx.VersionInfo is not { VersionExists: true, RunVersion: not null })
            return response;

        response.Items = await _repo.GetAllocPoolAsync(companyCd, ctx.Run.RunId, ctx.VersionInfo.RunVersion.Value, databaseName);
        response.HasDetailData = response.Items.Count > 0;
        return response;
    }

    public async Task<ProductionCostAllocBasisResponse> GetAllocBasisAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: true, loadVersionInfo: true);
        var response = new ProductionCostAllocBasisResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.VersionInfo?.RunVersion,
            RunVersionExists = ctx.VersionInfo?.VersionExists,
            HasDetailData = ctx.VersionInfo?.HasDetailData,
            AllocateType = ctx.Run?.AllocateType,
            Message = ctx.Message ?? ctx.VersionInfo?.Message
        };
        if (ctx.Run == null || ctx.VersionInfo is not { VersionExists: true, RunVersion: not null })
            return response;

        var version = ctx.VersionInfo.RunVersion.Value;
        response.NvlBasis = await _repo.GetNvlBasisAsync(companyCd, ctx.Run.RunId, version, databaseName);
        response.NvlBasisLines = await _repo.GetNvlBasisLinesAsync(companyCd, ctx.Run.RunId, version, databaseName);
        response.Orphans = await _repo.GetNvlOrphansAsync(companyCd, ctx.Run.RunId, version, databaseName);

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

        response.HasDetailData = response.NvlBasis.Count > 0
            || response.NvlBasisLines.Count > 0
            || response.Orphans.Count > 0
            || response.TpBasis.Count > 0;
        return response;
    }

    public async Task<ProductionCostAllocDetailResponse> GetAllocDetailAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: true, loadVersionInfo: true);
        var response = new ProductionCostAllocDetailResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.VersionInfo?.RunVersion,
            RunVersionExists = ctx.VersionInfo?.VersionExists,
            HasDetailData = ctx.VersionInfo?.HasDetailData,
            AllocateType = ctx.Run?.AllocateType,
            Message = ctx.Message ?? ctx.VersionInfo?.Message
        };
        if (ctx.Run == null || ctx.VersionInfo is not { VersionExists: true, RunVersion: not null })
            return response;

        response.Items = await _repo.GetAllocDetailAsync(companyCd, ctx.Run.RunId, ctx.VersionInfo.RunVersion.Value, databaseName);
        response.HasDetailData = response.Items.Count > 0;
        return response;
    }

    public async Task<ProductionCostWipResponse> GetWipAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: true, loadVersionInfo: true);
        var response = new ProductionCostWipResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.VersionInfo?.RunVersion,
            RunVersionExists = ctx.VersionInfo?.VersionExists,
            HasDetailData = ctx.VersionInfo?.HasDetailData,
            WipMethod = ctx.Run?.WipMethod,
            RunStatus = ctx.Run?.Status,
            Message = ctx.Message ?? ctx.VersionInfo?.Message
        };
        if (ctx.Run == null || ctx.VersionInfo is not { VersionExists: true, RunVersion: not null })
            return response;

        var version = ctx.VersionInfo.RunVersion.Value;
        response.Items = await _repo.GetWipAsync(companyCd, ctx.Run.RunId, version, databaseName);
        // Nguồn chi phí đã loại EXP_TO_154 — đọc production_cost_source, không cộng LOCK
        response.Sources = await _repo.GetSourcesAsync(companyCd, ctx.Run.RunId, version, databaseName);
        response.HasDetailData = response.Items.Count > 0 || response.Sources.Count > 0;
        return response;
    }

    public async Task<ProductionCostReceiptsResponse> GetReceiptsAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: true, loadVersionInfo: true);
        var response = new ProductionCostReceiptsResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = ctx.Run?.RunId,
            RunVersion = ctx.VersionInfo?.RunVersion,
            RunVersionExists = ctx.VersionInfo?.VersionExists,
            HasDetailData = ctx.VersionInfo?.HasDetailData,
            Message = ctx.Message ?? ctx.VersionInfo?.Message
        };
        if (ctx.Run == null || ctx.VersionInfo is not { VersionExists: true, RunVersion: not null })
            return response;

        response.Items = await _repo.GetReceiptsAsync(companyCd, ctx.Run.RunId, ctx.VersionInfo.RunVersion.Value, databaseName);
        response.HasDetailData = response.Items.Count > 0;
        return response;
    }

    public async Task<ProductionCostHistoryResponse> GetHistoryAsync(string companyCd, ProductionCostQuery query, string? databaseName = null)
    {
        var ctx = await ResolveContextAsync(companyCd, query, databaseName, requireRun: false, loadVersionInfo: false);
        var response = new ProductionCostHistoryResponse
        {
            SchemaAvailable = ctx.Schema.IsReady,
            SchemaStatus = ctx.Schema.Status,
            PeriodExists = ctx.PeriodExists,
            PeriodYm = query.PeriodYm,
            RunId = query.RunId ?? ctx.Run?.RunId,
            Message = ctx.Message
        };
        if (!ctx.Schema.IsReady)
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
        EnsureDatabase(databaseName);
        var err = ProductionCostValidation.ValidateQuery(periodYm, baseRunId, baseRunVersion);
        if (err != null)
            throw new ArgumentException(err);
        err = ProductionCostValidation.ValidateQuery(periodYm, compareRunId, compareRunVersion);
        if (err != null)
            throw new ArgumentException(err);

        var response = new ProductionCostComparisonResponse { PeriodYm = periodYm };
        var probe = await _repo.ProbeSchemaAsync(databaseName);
        response.SchemaStatus = probe.Status;
        response.MissingTables = probe.MissingTables.Count > 0 ? probe.MissingTables : null;
        response.SchemaAvailable = probe.IsReady;

        if (!probe.IsReady)
        {
            response.Message = SchemaMessage(probe);
            return response;
        }

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
            response.Message = "RUN_ID không thuộc COMPANY_CD + PERIOD_YM hoặc không tìm thấy RUN để so sánh.";
            return response;
        }

        var baseHas = await _repo.HasRunVersionDetailAsync(
            companyCd, baseRun.RunId, ProductionCostValidation.ResolveDetailVersion(baseRunVersion, baseRun.RunVersion), databaseName);
        var compareHas = await _repo.HasRunVersionDetailAsync(
            companyCd, compareRun.RunId, ProductionCostValidation.ResolveDetailVersion(compareRunVersion, compareRun.RunVersion), databaseName);

        var baseInfo = ProductionCostValidation.ResolveRunVersionInfo(baseRunVersion, baseRun.RunVersion, baseHas);
        var compareInfo = ProductionCostValidation.ResolveRunVersionInfo(compareRunVersion, compareRun.RunVersion, compareHas);

        response.BaseRunId = baseRun.RunId;
        response.BaseRunVersion = baseInfo.RunVersion;
        response.BaseRunVersionExists = baseInfo.VersionExists;
        response.CompareRunId = compareRun.RunId;
        response.CompareRunVersion = compareInfo.RunVersion;
        response.CompareRunVersionExists = compareInfo.VersionExists;

        if (!baseInfo.VersionExists || !compareInfo.VersionExists)
        {
            response.Message = baseInfo.VersionExists
                ? compareInfo.Message
                : baseInfo.Message;
            return response;
        }

        response.Lines = await _repo.GetComparisonLinesAsync(
            companyCd,
            baseRun.RunId,
            baseInfo.RunVersion!.Value,
            compareRun.RunId,
            compareInfo.RunVersion!.Value,
            databaseName);
        return response;
    }

    private async Task<RunContext> ResolveContextAsync(
        string companyCd,
        ProductionCostQuery query,
        string? databaseName,
        bool requireRun,
        bool loadVersionInfo)
    {
        EnsureCompany(companyCd);
        EnsureDatabase(databaseName);
        var err = ProductionCostValidation.ValidateQuery(query.PeriodYm, query.RunId, query.RunVersion);
        if (err != null)
            throw new ArgumentException(err);

        var probe = await _repo.ProbeSchemaAsync(databaseName);
        if (!probe.IsReady)
            return new RunContext(probe, false, null, null, null, SchemaMessage(probe));

        var period = await _repo.GetPeriodRowAsync(companyCd, query.PeriodYm, databaseName);
        if (period == null)
            return new RunContext(probe, false, null, null, null, "Kỳ G-K chưa được tạo.");

        var run = await _repo.ResolveRunAsync(companyCd, query.PeriodYm, query.RunId, query.RunVersion, databaseName);
        if (query.RunId is > 0 && run == null)
        {
            return new RunContext(
                probe,
                true,
                null,
                null,
                period.CogsRule,
                "RUN_ID không thuộc COMPANY_CD + PERIOD_YM.");
        }

        if (requireRun && run == null)
            return new RunContext(probe, true, null, null, period.CogsRule, "Chưa có CURRENT_RUN cho kỳ G-K.");

        ProductionCostRunVersionInfo? versionInfo = null;
        string? message = null;
        if (run != null && loadVersionInfo)
        {
            var detailVersion = ProductionCostValidation.ResolveDetailVersion(query.RunVersion, run.RunVersion);
            var hasDetail = await _repo.HasRunVersionDetailAsync(companyCd, run.RunId, detailVersion, databaseName);
            versionInfo = ProductionCostValidation.ResolveRunVersionInfo(query.RunVersion, run.RunVersion, hasDetail);
            if (!versionInfo.VersionExists)
                message = versionInfo.Message;
        }

        return new RunContext(probe, true, run, versionInfo, period.CogsRule, message);
    }

    private static void EnsureCompany(string companyCd)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new InvalidOperationException("COMPANY_CD is required from authentication context.");
    }

    private static void EnsureDatabase(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Database name is required from authentication context.");
    }

    private static string SchemaMessage(ProductionCostSchemaProbe probe) =>
        probe.Status switch
        {
            ProductionCostSchemaStatus.Incomplete =>
                "Schema production_cost_* triển khai chưa đầy đủ. Thiếu: " +
                string.Join(", ", probe.MissingTables),
            _ => "Schema production_cost_* chưa có trên database (chờ migration 2B DEV)."
        };

    private sealed record RunContext(
        ProductionCostSchemaProbe Schema,
        bool PeriodExists,
        ProductionCostRunDto? Run,
        ProductionCostRunVersionInfo? VersionInfo,
        string? PeriodCogsRule,
        string? Message);
}

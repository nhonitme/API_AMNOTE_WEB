using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Transfer;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Ghi G-K Bước 2 / Bước 3 (2C-02). Mặc định bị chặn bởi <see cref="IProductionCostWriteGate"/>.
/// Preview dry-run có thể tính thuần khi client gửi payload — không persist.
/// </summary>
public sealed class ProductionCostWriteService : IProductionCostWriteService
{
    private readonly IProductionCostWriteGate _gate;

    public ProductionCostWriteService(IProductionCostWriteGate gate)
    {
        _gate = gate;
    }

    public Task<ProductionCostAllocationWriteResponse> PreviewAllocationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);

        // Preview: tính thử thuần nếu có DryRun — không ghi DB dù gate mở hay đóng.
        if (request.DryRun == null)
        {
            _gate.EnsureWritable();
            // Khi EnableWrites=true: sẽ load pool/basis từ DB rồi tính + ghi DRAFT (2C-02b / sau nghiệm thu 2B).
            throw new InvalidOperationException(
                "Preview allocation từ DB chưa kích hoạt trong 2C-02. " +
                "Gửi DryRun payload để tính thử thuần, hoặc chờ bật ghi sau nghiệm thu 2B.");
        }

        var result = RunDryAllocation(companyCd, request.DryRun);
        ConfirmIdempotency.AssertPreviewDoesNotOverwriteConfirmed(
            runStatus: null,
            wouldPersist: false);

        return Task.FromResult(new ProductionCostAllocationWriteResponse
        {
            WriteEnabled = _gate.IsWriteEnabled,
            Persisted = false,
            PeriodYm = request.PeriodYm,
            RunId = request.RunId,
            RunVersion = request.RunVersion,
            AllocateType = request.DryRun.AllocateType,
            ByDepartment = request.DryRun.ByDepartment,
            Pool = result.Pool,
            Details = result.Details,
            Total622 = result.Total622,
            Total627 = result.Total627,
            Message = "Preview dry-run — không ghi production_cost_* / không tạo chứng từ."
        });
    }

    public Task<ProductionCostAllocationWriteResponse> ConfirmAllocationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();

        // Skeleton sau gate (chưa kích hoạt):
        // 1) C# BEGIN TRANSACTION (owner) — SP confirm KHÔNG START/COMMIT
        // 2) SELECT RUN FOR UPDATE (RUN_ID+RUN_VERSION+COMPANY_CD)
        // 3) CALL sp_pc_alloc_confirm → STATUS=ALLOC_CONFIRMED (không CONFIRMED toàn kỳ)
        // 4) Idempotent: PlanConfirm → NoopAlreadyConfirmed thì không history
        // 5) Exception → ROLLBACK: giữ số liệu/status trước Confirm
        throw new InvalidOperationException(
            "Confirm allocation DB path đã có gate nhưng chưa kích hoạt thực thi trong 2C-02.");
    }

    public Task<ProductionCostTransferWriteResponse> PreviewTransferAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);

        ExpTo154Rules.AssertRuleCode(ProductionCostCogsRule.ExpTo154Only);
        ExpTo154Rules.AssertNoDownstreamTransfer(ProductionCostCogsRule.ExpTo154Only);

        // Dry validate exclusive + allocation confirmed flags từ request DryRun không áp dụng transfer;
        // Preview không persist — không gọi PeriodLock.
        if (!_gate.IsWriteEnabled)
        {
            return Task.FromResult(new ProductionCostTransferWriteResponse
            {
                WriteEnabled = false,
                Persisted = false,
                PeriodYm = request.PeriodYm,
                RunId = request.RunId,
                RunVersion = request.RunVersion,
                Downstream154To155Or632 = false,
                StableKey = ExpTo154Rules.StableKey(companyCd, request.PeriodYm),
                Message = "Preview transfer — chỉ kiểm tra quy tắc; không ghi chứng từ. " + _gate.BlockReason
            });
        }

        _gate.EnsureWritable();
        throw new InvalidOperationException(
            "Preview transfer từ DB chưa kích hoạt thực thi trong 2C-02.");
    }

    public Task<ProductionCostTransferWriteResponse> ConfirmTransferAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();

        ExpTo154Rules.AssertRuleCode(ProductionCostCogsRule.ExpTo154Only);
        ExpTo154Rules.AssertNoDownstreamTransfer(ProductionCostCogsRule.ExpTo154Only);

        // Khi mở: claim GK + PeriodLock EXP_TO_154_ONLY + STABLE_KEY idempotent.
        throw new InvalidOperationException(
            "Confirm transfer DB path đã có gate nhưng chưa kích hoạt thực thi trong 2C-02.");
    }

    public Task<ProductionCostWipWriteResponse> PreviewCostCalcAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);

        if (request.WipDryRun == null)
        {
            _gate.EnsureWritable();
            throw new InvalidOperationException(
                "Preview COST_CALC từ DB chưa kích hoạt trong 2C-03. " +
                "Gửi WipDryRun payload để tính thử thuần.");
        }

        var response = RunDryWip(request);
        response.WriteEnabled = _gate.IsWriteEnabled;
        response.Persisted = false;
        response.PeriodYm = request.PeriodYm;
        response.RunId = request.RunId;
        response.RunVersion = request.RunVersion;
        response.InventoryPriceApplied = false;
        response.Message =
            "Preview COST_CALC dry-run — không ghi production_cost_wip / không cập nhật giá nhập kho.";
        return Task.FromResult(response);
    }

    public Task<ProductionCostWipWriteResponse> ConfirmCostCalcAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();

        // Skeleton sau gate:
        // 1) C# TX owner → sp_pc_source_rebuild (loại XFER) → sp_pc_wip_calculate
        // 2) Không DELETE snapshot CONFIRMED cũ; chỉ ghi RUN_VERSION hiện tại (DRAFT)
        // 3) Receipt prep theo PRODUCT+DEPT+INPUT_ID — chưa apply inventory
        // 4) Confirm → step COST_CALC=DONE ONLY; RUN.STATUS giữ nguyên (AfterCostCalcConfirm)
        //    — không tự CONFIRMED toàn kỳ trước B5/B6
        throw new InvalidOperationException(
            "Confirm COST_CALC DB path đã có gate nhưng chưa kích hoạt thực thi trong 2C-03.");
    }

    /// <summary>Tính phân bổ thuần — dùng unit test / dry-run API.</summary>
    public static AllocationComputeResult RunDryAllocation(string companyCd, AllocationDryRunPayload dryRun)
    {
        if (dryRun.AllocateType is not (0 or 1))
            throw new ArgumentException("AllocateType must be 0 or 1.");

        // Lọc 6212 khỏi pool nếu Include6212=false
        var pool = dryRun.Pool
            .Where(p => PoolAccountRules.IsAllocatableAccount(p.AccountCd, dryRun.Include6212))
            .ToList();

        return AllocationEngine.Run(
            pool,
            dryRun.Products.ToList(),
            new AllocationEngineOptions
            {
                AllocateType = dryRun.AllocateType,
                ByDepartment = dryRun.ByDepartment,
                NumberDigit = dryRun.NumberDigit,
                CompanyCd = companyCd
            });
    }

    /// <summary>Tính dở dang / GT thuần — unit test / dry-run API.</summary>
    public static ProductionCostWipWriteResponse RunDryWip(ProductionCostWriteRequest request)
    {
        var dry = request.WipDryRun
            ?? throw new ArgumentException("WipDryRun is required.");

        if (dry.Products.Count == 0)
            throw new ArgumentException("WipDryRun.Products không được rỗng.");

        var items = WipCostEngine.Run(dry.Products, dry.WipMethod, dry.Config, dry.Sources);
        if (items.Any(i => i.HasErrors))
        {
            var first = items.First(i => i.HasErrors).Errors[0];
            throw new InvalidOperationException(first);
        }

        var compare = dry.ConfirmedBaseline != null && dry.ConfirmedBaseline.Count > 0
            ? CostRunCompare.Compare(dry.ConfirmedBaseline, items)
            : Array.Empty<CostCompareLine>();

        var receipts = dry.Receipts.Count > 0
            ? ReceiptPricePrep.Build(items, dry.Receipts)
            : Array.Empty<ReceiptPricePrepLine>();

        if (receipts.Count > 0)
            ReceiptReconcile.AssertMatches(items, receipts);

        return new ProductionCostWipWriteResponse
        {
            WipMethod = dry.WipMethod,
            IncompleteMode = dry.Config.Mode,
            Items = items,
            Compare = compare,
            ReceiptPreps = receipts,
            TotalCostPriceSum = items.Sum(i => i.TotalCostPrice),
            InventoryPriceApplied = false
        };
    }

    private static void EnsureAuth(string companyCd)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new InvalidOperationException("COMPANY_CD is required from authentication context.");
    }

    private static void ValidatePeriod(ProductionCostWriteRequest request)
    {
        var err = ProductionCostValidation.ValidateQuery(request.PeriodYm, request.RunId, request.RunVersion);
        if (err != null)
            throw new ArgumentException(err);
    }
}

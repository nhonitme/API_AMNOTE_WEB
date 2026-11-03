using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;
using API_AMNOTE_WEB.Services.ProductionCost.Transfer;
using API_AMNOTE_WEB.Services.ProductionCost.Valuation;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Ghi G-K Bước 2+ . Mặc định bị chặn bởi <see cref="IProductionCostWriteGate"/>.
/// Preview dry-run thuần; Confirm B2 (2C-06B) dùng TX C# khi EnableWrites.
/// </summary>
public sealed class ProductionCostWriteService : IProductionCostWriteService
{
    private readonly IProductionCostWriteGate _gate;
    private readonly IProductionCostWriteRepository _writeRepo;

    public ProductionCostWriteService(
        IProductionCostWriteGate gate,
        IProductionCostWriteRepository writeRepo)
    {
        _gate = gate;
        _writeRepo = writeRepo;
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

    public async Task<ProductionCostAllocationWriteResponse> ConfirmAllocationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);

        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var operationId = ProductionCostWriteContext.RequireOperationId(request.OperationId);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;
        var requestPeriodYm = request.PeriodYm.Trim();

        // Preview-from-DB chưa có: Confirm vẫn phụ thuộc DryRun pool/basis — KHÔNG production-ready.
        // WriteGate mặc định OFF; không coi nguồn tài chính client là authoritative khi gate ON sau này.
        var dryRun = request.DryRun
            ?? throw new InvalidOperationException(
                "Confirm B2 production-ready (load pool/basis từ DB) chưa kích hoạt. " +
                "Preview-from-DB pending — không ghi thực tế chỉ bằng DryRun tài chính client. " +
                "Gửi DryRun chỉ cho unit/DEV gate tạm, hoặc chờ load pool/basis từ DB.");

        return await ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                // 1) Lock RUN — assert VERSION trước mọi thay đổi.
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);
                AllocationRunConfig.AssertPeriodYmMatches(locked, requestPeriodYm);

                // 2) Cấu hình phân bổ từ RUN DB — không tin DryRun client.
                var config = AllocationRunConfig.FromLockedRun(locked);
                AllocationRunConfig.AssertClientDryRunAligned(config, dryRun);

                // 3) Engine: options từ DB; pool/products tạm từ DryRun (pending Preview-from-DB).
                var compute = RunAllocationWithConfig(companyCd, dryRun, config);
                var periodYm = config.PeriodYm;

                var steps = await _writeRepo.GetStepStatusesForUpdateAsync(session, companyCd, runId);
                var persisted = await _writeRepo.GetPersistedHistoryByOperationIdAsync(
                    session, companyCd, operationId);

                var plan = AllocationConfirmPlanner.Build(
                    companyCd,
                    periodYm,
                    runId,
                    runVersion,
                    locked.Status,
                    steps,
                    operationId,
                    compute,
                    config.AllocateType,
                    config.ByDepartment,
                    persisted,
                    userId,
                    clientFingerprint: null,
                    numberDigit: config.NumberDigit,
                    include6212: config.Include6212);

                if (plan.IsNoop)
                {
                    return new ProductionCostAllocationWriteResponse
                    {
                        WriteEnabled = true,
                        Persisted = false,
                        PeriodYm = periodYm,
                        RunId = runId,
                        RunVersion = runVersion,
                        AllocateType = config.AllocateType,
                        ByDepartment = config.ByDepartment,
                        Pool = compute.Pool,
                        Details = compute.Details,
                        Total622 = compute.Total622,
                        Total627 = compute.Total627,
                        IdempotencyAction = plan.IdempotencyAction,
                        Message = plan.Message
                    };
                }

                // Persist UPSERT trước SP. Fail bất kỳ bước → ROLLBACK cả TX.
                await _writeRepo.ReplaceAllocPoolAsync(
                    session, companyCd, runId, runVersion, compute.Pool, userId,
                    locked.Status, locked.RunVersion);
                await _writeRepo.ReplaceAllocDetailAsync(
                    session, companyCd, runId, runVersion, compute.Details, userId,
                    locked.Status, locked.RunVersion);

                var spResult = await _writeRepo.CallAllocConfirmAsync(
                    session, companyCd, runId, runVersion, userId);

                if (!string.Equals(spResult, "ALLOC_CONFIRMED", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(spResult, "NOOP_ALREADY_CONFIRMED", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"sp_pc_alloc_confirm RESULT bất thường: {spResult}");
                }

                if (!AllocationConfirmPlanner.ShouldCommitHistory(detailsPersisted: true, spSucceeded: true))
                    throw new InvalidOperationException("Không ghi history khi persist/SP thất bại.");

                var historyInsert = ProductionCostWriteContext.FromHistoryPlan(plan.History!, companyCd, periodYm);
                historyInsert.ContentFingerprint = plan.ContentFingerprint;
                historyInsert.RequireContentFingerprint = true;
                historyInsert.ResultAfter =
                    $"{spResult}; fp={plan.ContentFingerprint}; voucher={plan.VoucherStableKey}";

                var hist = await _writeRepo.TryInsertHistoryAsync(session, historyInsert, databaseName!);
                if (!hist.Inserted
                    && !string.Equals(hist.IdempotencyAction, "NoopAlreadyWritten", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(hist.Message ?? "History Confirm B2 thất bại.");
                }

                return new ProductionCostAllocationWriteResponse
                {
                    WriteEnabled = true,
                    Persisted = true,
                    PeriodYm = periodYm,
                    RunId = runId,
                    RunVersion = runVersion,
                    AllocateType = config.AllocateType,
                    ByDepartment = config.ByDepartment,
                    Pool = compute.Pool,
                    Details = compute.Details,
                    Total622 = compute.Total622,
                    Total627 = compute.Total627,
                    IdempotencyAction = hist.IdempotencyAction,
                    Message =
                        $"Confirm B2 OK — RUN={GkRunStatus.AllocConfirmed}; SP={spResult}; " +
                        "không tạo N154; không CONFIRMED toàn kỳ. " +
                        "NOT production-ready: pool/basis vẫn từ DryRun (Preview-from-DB pending)."
                };
            });
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

    public Task<ProductionCostValuationWriteResponse> PreviewValuationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);

        if (request.ValuationDryRun == null)
        {
            _gate.EnsureWritable();
            throw new InvalidOperationException(
                "Preview VALUATION từ DB chưa kích hoạt trong 2C-04. Gửi ValuationDryRun payload.");
        }

        var response = RunDryValuation(companyCd, request);
        response.WriteEnabled = _gate.IsWriteEnabled;
        response.Persisted = false;
        response.PeriodYm = request.PeriodYm;
        response.RunId = request.RunId;
        response.RunVersion = request.RunVersion;
        response.InventoryPriceApplied = false;
        response.CogsVoucherPosted = false;
        response.StepStatusesAreClientSimulated = true;
        response.Message =
            "Preview VALUATION dry-run — không ghi kho / không post 632. " +
            "Trạng thái B2/B3/B4 trong DryRun chỉ mô phỏng client; Confirm phải đọc lại từ DB.";
        return Task.FromResult(response);
    }

    public Task<ProductionCostValuationWriteResponse> ConfirmValuationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();

        // Skeleton (TX owner = C#):
        // 1) ĐỌC LẠI từ DB: B2/B3/B4 DONE + RUN_ID+VERSION + COMPANY_CD — không tin DryRun client
        // 2) ReadyToApply reconcile → apply giá INPUT_ID (COMPANY_CD) theo nhóm PRODUCT+STORE
        // 3) Tính lại xuất theo nhóm + UPSERT VALUATION_COGS (STABLE_KEY+COMPANY) — atomic với (2)
        // 4) VALUATION=DONE; không tự RUN.STATUS=CONFIRMED toàn kỳ
        // 5) RollbackRestoreLines nếu fail giữa chừng
        throw new InvalidOperationException(
            "Confirm VALUATION DB path đã có gate nhưng chưa kích hoạt thực thi trong 2C-04. " +
            "Confirm bắt buộc kiểm tra lại trạng thái bước từ database.");
    }

    /// <summary>Tính phân bổ thuần — dùng unit test / dry-run API.</summary>
    public static AllocationComputeResult RunDryAllocation(string companyCd, AllocationDryRunPayload dryRun)
    {
        if (dryRun.AllocateType is not (0 or 1))
            throw new ArgumentException("AllocateType must be 0 or 1.");

        return RunAllocationWithConfig(
            companyCd,
            dryRun,
            new AllocationRunConfig
            {
                PeriodYm = "000000",
                AllocateType = dryRun.AllocateType,
                ByDepartment = dryRun.ByDepartment,
                NumberDigit = dryRun.NumberDigit,
                Include6212 = dryRun.Include6212
            });
    }

    /// <summary>
    /// AllocationEngine với cấu hình authoritative (Confirm = từ RUN DB).
    /// Không đổi công thức phân bổ.
    /// </summary>
    public static AllocationComputeResult RunAllocationWithConfig(
        string companyCd,
        AllocationDryRunPayload source,
        AllocationRunConfig config)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(config);
        if (config.AllocateType is not (0 or 1))
            throw new ArgumentException("AllocateType must be 0 or 1.");

        var pool = source.Pool
            .Where(p => PoolAccountRules.IsAllocatableAccount(p.AccountCd, config.Include6212))
            .ToList();

        return AllocationEngine.Run(
            pool,
            source.Products.ToList(),
            new AllocationEngineOptions
            {
                AllocateType = config.AllocateType,
                ByDepartment = config.ByDepartment,
                NumberDigit = config.NumberDigit,
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
            ReceiptReconcile.AssertMatches(items, receipts, ReceiptReconcileMode.Preview);

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

    /// <summary>Preview VALUATION thuần — unit test / dry-run API.</summary>
    public static ProductionCostValuationWriteResponse RunDryValuation(
        string companyCd,
        ProductionCostWriteRequest request)
    {
        var dry = request.ValuationDryRun
            ?? throw new ArgumentException("ValuationDryRun is required.");

        ValuationPrerequisite.AssertReady(
            companyCd,
            request.PeriodYm,
            request.RunId,
            request.RunVersion,
            dry.RunStatus,
            dry.AllocationStepStatus,
            dry.ExpTo154StepStatus,
            dry.CostCalcStepStatus);

        if (dry.ConfirmedWip.Count == 0)
            throw new ArgumentException("ValuationDryRun.ConfirmedWip không được rỗng.");

        var receiptPreps = ReceiptPricePrep.Build(dry.ConfirmedWip, dry.Receipts, companyCd);
        var reconcile = ReceiptReconcile.Check(
            dry.ConfirmedWip, receiptPreps, ReceiptReconcileMode.Preview);

        if (!reconcile.Ok)
            throw new InvalidOperationException(reconcile.Errors[0]);

        IReadOnlyList<ValuationAverageResult> beforeGroups = Array.Empty<ValuationAverageResult>();
        IReadOnlyList<ValuationAverageResult> afterGroups = Array.Empty<ValuationAverageResult>();
        IReadOnlyList<Cogs632PreviewLine> cogs = Array.Empty<Cogs632PreviewLine>();

        if (dry.Movements.Count > 0)
        {
            beforeGroups = ValuationAverageCost.ComputeByGroup(dry.Movements, dry.ValuationMethod, companyCd);
            var afterMovements = ValuationAverageCost.ApplyReceiptPrices(
                dry.Movements, receiptPreps, companyCd, dry.ConfirmedWip);
            afterGroups = ValuationAverageCost.ComputeByGroup(afterMovements, dry.ValuationMethod, companyCd);
            cogs = Cogs632PreviewRules.BuildPreviewByGroup(beforeGroups, afterGroups);
        }

        var plan = ValuationApplyPlan.Build(
            companyCd,
            request.PeriodYm,
            receiptPreps,
            dry.ExistingValuationChitId,
            writeEnabled: false,
            dry.ExistingValuationChitCompanyCd,
            dry.ExistingValuationStableKey);

        return new ProductionCostValuationWriteResponse
        {
            StepCode = ProductionCostStepCode.Valuation,
            ValuationMethod = dry.ValuationMethod,
            ReceiptPreps = receiptPreps,
            ReconcileWarnings = reconcile.Warnings,
            ReconcileErrors = reconcile.Errors,
            ReadyToApply = reconcile.ReadyToApply,
            AverageBeforeGroups = beforeGroups,
            AverageAfterGroups = afterGroups,
            Cogs632Preview = cogs,
            TransactionPlan = plan,
            InventoryPriceApplied = false,
            CogsVoucherPosted = false,
            StepStatusesAreClientSimulated = true
        };
    }

    public Task<ProductionCostLifecycleWriteResponse> PreviewReopenAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        if (request.ReopenDryRun == null)
        {
            _gate.EnsureWritable();
            throw new InvalidOperationException(
                "Preview REOPEN từ DB chưa kích hoạt trong 2C-05. Gửi ReopenDryRun payload.");
        }

        LifecycleRequestGuard.AssertAlignedWithReopen(request, request.ReopenDryRun);
        var plan = ReopenRules.Build(request.ReopenDryRun, companyCd, userId);
        RunVersionRules.AssertConfirmedSnapshotIntact(
            plan.WouldDeleteConfirmedData,
            wouldOverwriteConfirmedUniqueKey: false);

        return Task.FromResult(new ProductionCostLifecycleWriteResponse
        {
            WriteEnabled = _gate.IsWriteEnabled,
            Persisted = false,
            PeriodYm = request.PeriodYm,
            RunId = plan.RunId,
            RunVersion = plan.NewWorkingRunVersion,
            Action = HistoryActionCode.Reopen,
            Reopen = plan,
            StepStatusesAreClientSimulated = true,
            Message =
                "Preview REOPEN dry-run — không ghi DB. " +
                "Quyền/bước/history trong DryRun chỉ mô phỏng; Confirm đọc OperationId + trạng thái từ DB. " +
                plan.Message
        });
    }

    public Task<ProductionCostLifecycleWriteResponse> ConfirmReopenAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();

        // Skeleton (TX owner = C#):
        // 1) Đọc quyền + RUN FOR UPDATE (COMPANY_CD, RUN_ID, RUN_VERSION) — không tin DryRun
        // 2) Đọc PersistedOperationIds từ production_cost_history — idempotent theo OperationId
        // 3) ReopenRules: chỉ +1 version lần đầu; đã REOPENED thì giữ version
        // 4) SUPERSEDED chỉ khi CreateNewWorkingVersion; không DELETE snapshot cũ
        // 5) Không CONFIRMED toàn kỳ; không apply kho / 632
        throw new InvalidOperationException(
            "Confirm REOPEN DB path đã có gate nhưng chưa kích hoạt thực thi trong 2C-05. " +
            "Confirm bắt buộc đọc lại quyền, RUN_VERSION và OperationId từ database.");
    }

    public Task<ProductionCostLifecycleWriteResponse> PreviewRecalculateAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        if (request.RecalcDryRun == null)
        {
            _gate.EnsureWritable();
            throw new InvalidOperationException(
                "Preview RECALCULATE từ DB chưa kích hoạt trong 2C-05. Gửi RecalcDryRun payload.");
        }

        LifecycleRequestGuard.AssertAlignedWithRecalc(request, request.RecalcDryRun);
        var plan = RecalcRules.Build(request.RecalcDryRun, companyCd, userId);
        return Task.FromResult(new ProductionCostLifecycleWriteResponse
        {
            WriteEnabled = _gate.IsWriteEnabled,
            Persisted = false,
            PeriodYm = request.PeriodYm,
            RunId = plan.RunId,
            RunVersion = plan.WorkingRunVersion,
            Action = HistoryActionCode.Recalculate,
            Recalc = plan,
            StepStatusesAreClientSimulated = true,
            Message =
                "Preview RECALCULATE dry-run — không đổi chứng từ KT / kho. " + plan.Message
        });
    }

    public Task<ProductionCostLifecycleWriteResponse> PreviewCompareAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        if (request.CompareDryRun == null)
        {
            throw new ArgumentException("CompareDryRun is required.");
        }

        var compare = SessionCompare.Compare(request.CompareDryRun);
        return Task.FromResult(new ProductionCostLifecycleWriteResponse
        {
            WriteEnabled = _gate.IsWriteEnabled,
            Persisted = false,
            PeriodYm = request.PeriodYm,
            RunId = request.RunId,
            RunVersion = request.RunVersion,
            Action = HistoryActionCode.Compare,
            Compare = compare,
            StepStatusesAreClientSimulated = true,
            Message = compare.Message
        });
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

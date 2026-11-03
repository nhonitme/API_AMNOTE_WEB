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
/// Preview dry-run thuần; Confirm B2–B5 + REOPEN dùng TX C# khi EnableWrites
/// (B5 INPUT_ID + REOPEN reverse capability-gated).
/// </summary>
public sealed class ProductionCostWriteService : IProductionCostWriteService
{
    private readonly IProductionCostWriteGate _gate;
    private readonly IProductionCostWriteRepository _writeRepo;
    private readonly IProductionCostReopenAuthority _reopenAuthority;
    /// <summary>
    /// Chỉ unit test B2/B4/B5 orchestration: cho phép DryRun / WipDryRun / ValuationDryRun khi Preview-from-DB chưa có.
    /// Production / DI luôn false — source guard chặn ghi thật.
    /// </summary>
    private readonly bool _allowNonAuthoritativeFinancialPersistForTests;

    public ProductionCostWriteService(
        IProductionCostWriteGate gate,
        IProductionCostWriteRepository writeRepo,
        bool allowNonAuthoritativeFinancialPersistForTests = false,
        IProductionCostReopenAuthority? reopenAuthority = null)
    {
        _gate = gate;
        _writeRepo = writeRepo;
        _allowNonAuthoritativeFinancialPersistForTests = allowNonAuthoritativeFinancialPersistForTests;
        // Thiếu authority → chặn Confirm REOPEN (không mặc định mọi user login đều được).
        _reopenAuthority = reopenAuthority ?? new BlockedProductionCostReopenAuthority();
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

        // Guard độc lập EnableWrites: chưa có pool/basis từ DB → không ghi thật.
        // Preview-from-DB pending → poolBasisLoadedFromDb=false (trừ unit-test hook).
        var poolBasisLoadedFromDb = false;
        if (!_allowNonAuthoritativeFinancialPersistForTests)
            AllocationConfirmSourceGuard.EnsureAuthoritativeFinancialSource(poolBasisLoadedFromDb);

        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var operationId = ProductionCostWriteContext.RequireOperationId(request.OperationId);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;
        var requestPeriodYm = request.PeriodYm.Trim();

        // Preview-from-DB chưa có: DryRun pool/products chỉ cho phép khi test hook bật.
        var dryRun = request.DryRun
            ?? throw new InvalidOperationException(
                "Confirm B2 production-ready (load pool/basis từ DB) chưa kích hoạt. " +
                "Preview-from-DB pending — không ghi thực tế chỉ bằng DryRun tài chính client.");

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

    public async Task<ProductionCostTransferWriteResponse> ConfirmTransferAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        TransferConfirmAtomicity.EnsureSharedSessionAvailable();

        ExpTo154Rules.AssertRuleCode(ProductionCostCogsRule.ExpTo154Only);
        ExpTo154Rules.AssertNoDownstreamTransfer(ProductionCostCogsRule.ExpTo154Only);

        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var operationId = ProductionCostWriteContext.RequireOperationId(request.OperationId);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;
        var requestPeriodYm = request.PeriodYm.Trim();

        return await ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);
                AllocationRunConfig.AssertPeriodYmMatches(locked, requestPeriodYm);
                var periodYm = locked.PeriodYm.Trim();

                var period = await _writeRepo.LockPeriodEngineForUpdateAsync(session, companyCd, periodYm)
                    ?? throw new InvalidOperationException("production_cost_period không tồn tại cho PERIOD_YM.");

                // CURRENT_RUN_ID + PERIOD_COST_ID — reject trước mọi SP ghi.
                ProductionCostWriteContext.AssertPeriodCurrentRunMatches(
                    locked, period, companyCd, runId, periodYm);

                var steps = await _writeRepo.GetStepStatusesForUpdateAsync(session, companyCd, runId);
                var allocTotal = await _writeRepo.GetConfirmedAllocTotalAsync(
                    session, companyCd, runId, runVersion);

                // Authoritative B-I COGS_SUMMARY (cùng TX) — khớp sp_period_lock_cogs_validate.
                var biCogsStepStatus = await _writeRepo.GetBiCogsStepStatusForUpdateAsync(
                    session, companyCd, periodYm);

                var persisted = await _writeRepo.GetPersistedHistoryByOperationIdAsync(
                    session, companyCd, operationId);

                var voucherStableKey = ExpTo154Rules.StableKey(companyCd, periodYm);
                var voucherRefExists = await _writeRepo.VoucherRefExistsAsync(
                    session, companyCd, voucherStableKey);
                var hasB3History = await _writeRepo.HasB3ConfirmHistoryAsync(
                    session, companyCd, runId, runVersion);

                var plan = TransferConfirmPlanner.Build(
                    companyCd,
                    periodYm,
                    runId,
                    runVersion,
                    locked.Status,
                    steps,
                    operationId,
                    period.CostEngine,
                    period.CogsRule,
                    biCogsStepStatus,
                    allocTotal,
                    persisted,
                    userId,
                    voucherRefExists: voucherRefExists,
                    hasB3ConfirmHistory: hasB3History);

                if (plan.IsNoop)
                {
                    return new ProductionCostTransferWriteResponse
                    {
                        WriteEnabled = true,
                        Persisted = false,
                        PeriodYm = periodYm,
                        RunId = runId,
                        RunVersion = runVersion,
                        Downstream154To155Or632 = false,
                        StableKey = plan.VoucherStableKey,
                        IdempotencyAction = plan.IdempotencyAction,
                        Message = plan.Message
                    };
                }

                if (!plan.WouldCallPeriodLockCogs)
                    throw new InvalidOperationException("Plan B3 không PeriodLock nhưng không phải Noop.");

                // PeriodLock COGS trên cùng session — không commit riêng.
                await _writeRepo.CallPeriodLockExpTo154Async(session, companyCd, periodYm, userId);

                await _writeRepo.UpsertGkStepStatusAsync(
                    session,
                    companyCd,
                    runId,
                    TransferConfirmPlanner.StepIndex,
                    TransferConfirmPlanner.StepCode,
                    ProductionCostStepStatus.Done,
                    userId);

                await _writeRepo.UpsertVoucherRefAsync(
                    session,
                    companyCd,
                    runId,
                    runVersion,
                    TransferConfirmPlanner.StepIndex,
                    TransferConfirmPlanner.StepCode,
                    "EXP_TO_154",
                    plan.VoucherStableKey,
                    userId);

                if (!TransferConfirmPlanner.ShouldCommitHistory(transferSucceeded: true, stepMarked: true))
                    throw new InvalidOperationException("Không ghi history khi kết chuyển/STEP thất bại.");

                var historyInsert = ProductionCostWriteContext.FromHistoryPlan(plan.History!, companyCd, periodYm);
                historyInsert.ContentFingerprint = plan.ContentFingerprint;
                historyInsert.RequireContentFingerprint = true;
                historyInsert.ResultAfter =
                    $"EXP_TO_154_ONLY; fp={plan.ContentFingerprint}; voucher={plan.VoucherStableKey}";

                var hist = await _writeRepo.TryInsertHistoryAsync(session, historyInsert, databaseName!);
                if (!hist.Inserted
                    && !string.Equals(hist.IdempotencyAction, "NoopAlreadyWritten", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(hist.Message ?? "History Confirm B3 thất bại.");
                }

                // Không đổi RUN → CONFIRMED toàn kỳ.
                return new ProductionCostTransferWriteResponse
                {
                    WriteEnabled = true,
                    Persisted = true,
                    PeriodYm = periodYm,
                    RunId = runId,
                    RunVersion = runVersion,
                    Downstream154To155Or632 = false,
                    StableKey = plan.VoucherStableKey,
                    IdempotencyAction = hist.IdempotencyAction,
                    Message =
                        "Confirm B3 OK — EXP_TO_154_ONLY; STEP DONE; " +
                        "không N155/C154; không N632/C154; không CONFIRMED toàn kỳ; " +
                        "PeriodLock trên cùng TX G-K."
                };
            });
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

    public async Task<ProductionCostWipWriteResponse> ConfirmCostCalcAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);

        // Guard độc lập EnableWrites: chưa load nguồn GT từ DB → không ghi thật.
        // Preview-from-DB / source rebuild pending → costCalcSourceLoadedFromDb=false
        // (trừ unit-test hook dùng WipDryRun).
        var costCalcSourceLoadedFromDb = false;
        if (!_allowNonAuthoritativeFinancialPersistForTests)
            CostCalcConfirmSourceGuard.EnsureAuthoritativeFinancialSource(costCalcSourceLoadedFromDb);

        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var operationId = ProductionCostWriteContext.RequireOperationId(request.OperationId);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;
        var requestPeriodYm = request.PeriodYm.Trim();

        // Nguồn tài chính tạm từ WipDryRun chỉ khi test hook — production bị guard ở trên.
        var dryRun = request.WipDryRun
            ?? throw new InvalidOperationException(
                "Confirm B4 production-ready (load objects/source/receipt từ DB) chưa kích hoạt. " +
                "Preview-from-DB / source rebuild pending — không ghi thực tế chỉ bằng WipDryRun client.");

        if (dryRun.Products.Count == 0)
            throw new ArgumentException("WipDryRun.Products không được rỗng.");

        return await ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);
                AllocationRunConfig.AssertPeriodYmMatches(locked, requestPeriodYm);

                var period = await _writeRepo.LockPeriodEngineForUpdateAsync(session, companyCd, locked.PeriodYm.Trim())
                    ?? throw new InvalidOperationException("production_cost_period không tồn tại cho PERIOD_YM.");
                ProductionCostWriteContext.AssertPeriodCurrentRunMatches(
                    locked, period, companyCd, runId, locked.PeriodYm.Trim());

                // Config WIP từ RUN DB — không tin WipDryRun cho method/mode/reduce.
                var config = WipRunConfig.FromLockedRun(locked);
                WipRunConfig.AssertClientDryRunAligned(config, dryRun);
                var periodYm = config.PeriodYm;

                // Nguồn incurred: Sources client (test) hoặc Amount* trên Products; loại XFER.
                var sources = dryRun.Sources.Count > 0
                    ? CostSourceRules.FilterForIncurred(dryRun.Sources).ToList()
                    : WipUpsertRules.SourcesFromProductAmounts(dryRun.Products).ToList();
                CostSourceRules.AssertNoTransferInIncurred(sources);

                // Products đưa vào engine: Amount*=0 khi có Sources để tránh cộng trùng.
                var productsForEngine = dryRun.Sources.Count > 0
                    ? dryRun.Products.Select(ClearAmounts).ToList()
                    : dryRun.Products.ToList();

                var items = WipCostEngine.Run(
                    productsForEngine, config.WipMethod, config.ToEngineConfig(), sources);
                if (items.Any(i => i.HasErrors))
                {
                    var first = items.First(i => i.HasErrors).Errors[0];
                    throw new InvalidOperationException(first);
                }

                var receiptPrep = ReceiptPricePrep.BuildDetailed(items, dryRun.Receipts, companyCd);
                var receipts = receiptPrep.Lines;
                // Reconcile Confirm — planner cũng assert ReadyToApply.

                var steps = await _writeRepo.GetStepStatusesForUpdateAsync(session, companyCd, runId);
                var persisted = await _writeRepo.GetPersistedHistoryByOperationIdAsync(
                    session, companyCd, operationId);
                var hasB4History = await _writeRepo.HasB4ConfirmHistoryAsync(
                    session, companyCd, runId, runVersion);

                var plan = CostCalcConfirmPlanner.Build(
                    companyCd,
                    periodYm,
                    runId,
                    runVersion,
                    locked.Status,
                    steps,
                    operationId,
                    config,
                    items,
                    receipts,
                    sources,
                    persisted,
                    userId,
                    clientFingerprint: null,
                    hasB4ConfirmHistory: hasB4History);

                if (plan.IsNoop)
                {
                    return new ProductionCostWipWriteResponse
                    {
                        WriteEnabled = true,
                        Persisted = false,
                        PeriodYm = periodYm,
                        RunId = runId,
                        RunVersion = runVersion,
                        WipMethod = config.WipMethod,
                        IncompleteMode = config.IncompleteMode,
                        Items = items,
                        ReceiptPreps = receipts,
                        TotalCostPriceSum = plan.TotalCostPriceSum,
                        InventoryPriceApplied = false,
                        Cogs632Posted = false,
                        IdempotencyAction = plan.IdempotencyAction,
                        Message = plan.Message
                    };
                }

                // Persist UPSERT theo RUN_VERSION — fail bất kỳ bước → ROLLBACK cả TX.
                await _writeRepo.ReplaceCostSourcesAsync(
                    session, companyCd, runId, runVersion, sources, userId,
                    locked.Status, locked.RunVersion);
                await _writeRepo.ReplaceWipAsync(
                    session, companyCd, runId, runVersion, items, userId,
                    locked.Status, locked.RunVersion);
                await _writeRepo.ReplaceReceiptsAsync(
                    session, companyCd, runId, runVersion, receipts, userId,
                    locked.Status, locked.RunVersion);

                // COST_CALC=DONE only — không apply kho; không post 632; không CONFIRMED toàn kỳ.
                await _writeRepo.UpsertGkStepStatusAsync(
                    session,
                    companyCd,
                    runId,
                    CostCalcConfirmPlanner.StepIndex,
                    CostCalcConfirmPlanner.StepCode,
                    ProductionCostStepStatus.Done,
                    userId);

                // Giữ RUN.STATUS theo AfterCostCalcConfirm (ALLOC_CONFIRMED/REOPENED) — không ghi CONFIRMED.
                var expectedStatus = GkRunStatus.AfterCostCalcConfirm(locked.Status);
                if (GkRunStatus.IsFullPeriodConfirmed(expectedStatus)
                    && !GkRunStatus.IsFullPeriodConfirmed(locked.Status))
                {
                    throw new InvalidOperationException("B4 không được chuyển RUN thành CONFIRMED toàn kỳ.");
                }

                if (!CostCalcConfirmPlanner.ShouldCommitHistory(persistSucceeded: true, stepMarked: true))
                    throw new InvalidOperationException("Không ghi history khi persist/STEP thất bại.");

                var historyInsert = ProductionCostWriteContext.FromHistoryPlan(plan.History!, companyCd, periodYm);
                historyInsert.ContentFingerprint = plan.ContentFingerprint;
                historyInsert.RequireContentFingerprint = true;
                historyInsert.ResultAfter =
                    $"COST_CALC=DONE; fp={plan.ContentFingerprint}; inv=0; 632=0; status={expectedStatus}";

                var hist = await _writeRepo.TryInsertHistoryAsync(session, historyInsert, databaseName!);
                if (!hist.Inserted
                    && !string.Equals(hist.IdempotencyAction, "NoopAlreadyWritten", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(hist.Message ?? "History Confirm B4 thất bại.");
                }

                return new ProductionCostWipWriteResponse
                {
                    WriteEnabled = true,
                    Persisted = true,
                    PeriodYm = periodYm,
                    RunId = runId,
                    RunVersion = runVersion,
                    WipMethod = config.WipMethod,
                    IncompleteMode = config.IncompleteMode,
                    Items = items,
                    ReceiptPreps = receipts,
                    TotalCostPriceSum = plan.TotalCostPriceSum,
                    InventoryPriceApplied = false,
                    Cogs632Posted = false,
                    IdempotencyAction = hist.IdempotencyAction,
                    Message =
                        "Confirm B4 OK — COST_CALC=DONE; source/wip/receipt theo RUN_VERSION; " +
                        $"RUN={expectedStatus}; không apply giá kho; không post 632; không CONFIRMED toàn kỳ. " +
                        "NOT production-ready: nguồn tài chính vẫn từ WipDryRun (Preview-from-DB / source rebuild pending)."
                };
            });
    }

    private static WipCalcInput ClearAmounts(WipCalcInput p) =>
        new()
        {
            ProductId = p.ProductId,
            ProductCd = p.ProductCd,
            DepartmentId = p.DepartmentId,
            DepartmentCd = p.DepartmentCd,
            IncompleteQtyOpening = p.IncompleteQtyOpening,
            BeforeAmountResult621 = p.BeforeAmountResult621,
            BeforeAmountResult62N = p.BeforeAmountResult62N,
            Amount621 = 0,
            Amount6212 = 0,
            Amount622 = 0,
            Amount627 = 0,
            Amount154Direct = 0,
            Quantity = p.Quantity,
            PercentCompleted = p.PercentCompleted,
            ClosingIncompleteQty = p.ClosingIncompleteQty,
            ClosingAmountOverride = p.ClosingAmountOverride,
            ProductReduce = p.ProductReduce,
            PreviousClosing154 = p.PreviousClosing154
        };

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

    public async Task<ProductionCostValuationWriteResponse> ConfirmValuationAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);

        // Guard độc lập EnableWrites: chưa load nguồn valuation từ DB → không ghi thật.
        // Preview-from-DB pending → valuationSourceLoadedFromDb=false (trừ unit-test hook).
        var valuationSourceLoadedFromDb = false;
        if (!_allowNonAuthoritativeFinancialPersistForTests)
            ValuationConfirmSourceGuard.EnsureAuthoritativeFinancialSource(valuationSourceLoadedFromDb);

        // Capability: UPDATE INPUT_ID an toàn — production repo = false → Confirm blocked.
        ValuationConfirmSourceGuard.EnsureInputIdSafeApplyReady(
            _writeRepo.SupportsInputIdSafeInventoryUpdate);

        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var operationId = ProductionCostWriteContext.RequireOperationId(request.OperationId);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;
        var requestPeriodYm = request.PeriodYm.Trim();

        // Nguồn tạm từ ValuationDryRun chỉ khi test hook — production bị guard ở trên.
        var dryRun = request.ValuationDryRun
            ?? throw new InvalidOperationException(
                "Confirm B5 production-ready (load receipt/movements/WIP từ DB) chưa kích hoạt. " +
                "Preview-from-DB pending — không ghi thực tế chỉ bằng ValuationDryRun client.");

        if (dryRun.ConfirmedWip.Count == 0)
            throw new ArgumentException("ValuationDryRun.ConfirmedWip không được rỗng.");
        if (dryRun.Receipts.Count == 0)
            throw new ArgumentException("ValuationDryRun.Receipts không được rỗng.");
        if (dryRun.Movements.Count == 0)
            throw new ArgumentException("ValuationDryRun.Movements không được rỗng.");

        return await ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);
                AllocationRunConfig.AssertPeriodYmMatches(locked, requestPeriodYm);

                var period = await _writeRepo.LockPeriodEngineForUpdateAsync(session, companyCd, locked.PeriodYm.Trim())
                    ?? throw new InvalidOperationException("production_cost_period không tồn tại cho PERIOD_YM.");
                ProductionCostWriteContext.AssertPeriodCurrentRunMatches(
                    locked, period, companyCd, runId, locked.PeriodYm.Trim());

                var periodYm = locked.PeriodYm.Trim();
                var valuationMethod = string.IsNullOrWhiteSpace(dryRun.ValuationMethod)
                    ? GkValuationMethod.PeriodEndAverage
                    : dryRun.ValuationMethod.Trim();

                // Receipt từ B4 prep — authoritative khi load DB; test hook dùng DryRun.
                var receiptPreps = ReceiptPricePrep.Build(dryRun.ConfirmedWip, dryRun.Receipts, companyCd);
                ReceiptReconcile.AssertMatches(
                    dryRun.ConfirmedWip, receiptPreps, ReceiptReconcileMode.ReadyToApply);

                // Đối chiếu từng INPUT_ID với dòng nhập kho (COMPANY_CD + PRODUCT + kỳ + đối tượng).
                var inventoryByInput = new Dictionary<long, InventoryInputValuationRow>();
                foreach (var r in receiptPreps)
                {
                    var row = await _writeRepo.GetInventoryInputByInputIdAsync(session, companyCd, r.InputId);
                    inventoryByInput[r.InputId] = row!;
                }

                ValuationInputIdMatch.AssertAllReceipts(companyCd, periodYm, receiptPreps, inventoryByInput);

                // Tính giá xuất trước/sau trên nguồn movements (DB authoritative / test DryRun).
                var beforeGroups = ValuationAverageCost.ComputeByGroup(
                    dryRun.Movements, valuationMethod, companyCd);
                var afterMovements = ValuationAverageCost.ApplyReceiptPrices(
                    dryRun.Movements, receiptPreps, companyCd, dryRun.ConfirmedWip);
                var afterGroups = ValuationAverageCost.ComputeByGroup(
                    afterMovements, valuationMethod, companyCd);
                var cogs = Cogs632PreviewRules.BuildPreviewByGroup(beforeGroups, afterGroups);

                var existingChitId = dryRun.ExistingValuationChitId;
                var existingCompany = dryRun.ExistingValuationChitCompanyCd;
                var existingKey = dryRun.ExistingValuationStableKey;
                // Nếu voucher_ref đã có trên DB → Confirm phải UPDATE, không INSERT thứ hai.
                var cogsStableKey = ValuationApplyPlan.StableKey(companyCd, periodYm);
                if (await _writeRepo.VoucherRefExistsAsync(session, companyCd, cogsStableKey)
                    && existingChitId is null or <= 0)
                {
                    existingChitId = 1; // sentinel: Plan → UpdateExisting; không INSERT trùng
                    existingCompany ??= companyCd;
                    existingKey ??= cogsStableKey;
                }

                var txPlan = ValuationApplyPlan.BuildForConfirm(
                    companyCd,
                    periodYm,
                    receiptPreps,
                    existingChitId,
                    existingCompany,
                    existingKey);

                var steps = await _writeRepo.GetStepStatusesForUpdateAsync(session, companyCd, runId);
                var persisted = await _writeRepo.GetPersistedHistoryByOperationIdAsync(
                    session, companyCd, operationId);
                var hasB5History = await _writeRepo.HasB5ConfirmHistoryAsync(
                    session, companyCd, runId, runVersion);

                var plan = ValuationConfirmPlanner.Build(
                    companyCd,
                    periodYm,
                    runId,
                    runVersion,
                    locked.Status,
                    steps,
                    operationId,
                    valuationMethod,
                    dryRun.ConfirmedWip,
                    receiptPreps,
                    beforeGroups,
                    afterGroups,
                    cogs,
                    txPlan,
                    persisted,
                    userId,
                    clientFingerprint: null,
                    hasB5ConfirmHistory: hasB5History);

                if (plan.IsNoop)
                {
                    return new ProductionCostValuationWriteResponse
                    {
                        WriteEnabled = true,
                        Persisted = false,
                        PeriodYm = periodYm,
                        RunId = runId,
                        RunVersion = runVersion,
                        ValuationMethod = valuationMethod,
                        ReceiptPreps = receiptPreps,
                        ReadyToApply = true,
                        AverageBeforeGroups = beforeGroups,
                        AverageAfterGroups = afterGroups,
                        Cogs632Preview = cogs,
                        TransactionPlan = txPlan,
                        InventoryPriceApplied = false,
                        CogsVoucherPosted = false,
                        StepStatusesAreClientSimulated = false,
                        IdempotencyAction = plan.IdempotencyAction,
                        Message = plan.Message
                    };
                }

                // Persist atomic: INPUT_ID → out price → 632 → IS_APPLIED → VALUATION=DONE → history.
                foreach (var line in txPlan.PriceLines)
                {
                    await _writeRepo.ApplyInventoryInputPriceByInputIdAsync(
                        session, companyCd, line, userId);
                }

                await _writeRepo.ApplyInventoryOutPricesAsync(
                    session, companyCd, periodYm, afterGroups, userId);

                await _writeRepo.UpsertValuationCogsAsync(
                    session, companyCd, periodYm, runId, runVersion, existingChitId, cogs, userId);

                await _writeRepo.UpsertVoucherRefAsync(
                    session,
                    companyCd,
                    runId,
                    runVersion,
                    ValuationConfirmPlanner.StepIndex,
                    ValuationConfirmPlanner.StepCode,
                    ValuationConfirmPlanner.CogsRefRole,
                    cogsStableKey,
                    userId);

                await _writeRepo.MarkReceiptsAppliedAsync(
                    session,
                    companyCd,
                    runId,
                    runVersion,
                    receiptPreps.Select(r => r.InputId).ToList(),
                    userId);

                await _writeRepo.UpsertGkStepStatusAsync(
                    session,
                    companyCd,
                    runId,
                    ValuationConfirmPlanner.StepIndex,
                    ValuationConfirmPlanner.StepCode,
                    ProductionCostStepStatus.Done,
                    userId);

                var expectedStatus = ValuationPrerequisite.AfterValuationConfirm(locked.Status);
                if (GkRunStatus.IsFullPeriodConfirmed(expectedStatus)
                    && !GkRunStatus.IsFullPeriodConfirmed(locked.Status))
                {
                    throw new InvalidOperationException("B5 không được chuyển RUN thành CONFIRMED toàn kỳ.");
                }

                if (!ValuationConfirmPlanner.ShouldCommitHistory(persistSucceeded: true, stepMarked: true))
                    throw new InvalidOperationException("Không ghi history khi persist/STEP thất bại.");

                var historyInsert = ProductionCostWriteContext.FromHistoryPlan(plan.History!, companyCd, periodYm);
                historyInsert.ContentFingerprint = plan.ContentFingerprint;
                historyInsert.RequireContentFingerprint = true;
                historyInsert.ResultAfter =
                    $"VALUATION=DONE; fp={plan.ContentFingerprint}; inv=1; 632=1; status={expectedStatus}";

                var hist = await _writeRepo.TryInsertHistoryAsync(session, historyInsert, databaseName!);
                if (!hist.Inserted
                    && !string.Equals(hist.IdempotencyAction, "NoopAlreadyWritten", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(hist.Message ?? "History Confirm B5 thất bại.");
                }

                return new ProductionCostValuationWriteResponse
                {
                    WriteEnabled = true,
                    Persisted = true,
                    PeriodYm = periodYm,
                    RunId = runId,
                    RunVersion = runVersion,
                    ValuationMethod = valuationMethod,
                    ReceiptPreps = receiptPreps,
                    ReadyToApply = true,
                    AverageBeforeGroups = beforeGroups,
                    AverageAfterGroups = afterGroups,
                    Cogs632Preview = cogs,
                    TransactionPlan = txPlan,
                    InventoryPriceApplied = true,
                    CogsVoucherPosted = true,
                    StepStatusesAreClientSimulated = false,
                    IdempotencyAction = hist.IdempotencyAction,
                    Message =
                        "Confirm B5 OK — VALUATION=DONE; apply INPUT_ID + tính xuất + UPSERT VALUATION_COGS; " +
                        $"RUN={expectedStatus}; không CONFIRMED toàn kỳ. " +
                        "NOT production-ready: nguồn vẫn từ ValuationDryRun; " +
                        "SQL INPUT_ID / post 632 thật pending DEV (SupportsInputIdSafeInventoryUpdate)."
                };
            });
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

    public async Task<ProductionCostLifecycleWriteResponse> ConfirmReopenAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        _gate.EnsureWritable();
        ProductionCostWriteContext.EnsureDatabase(databaseName);

        // Quyền REOPEN thật (AMnote menu EDIT) — không tin ReopenDryRun.HasPermission.
        await _reopenAuthority.AssertCanConfirmReopenAsync(companyCd, userId, databaseName);

        ProductionCostWriteContext.EnsureRunIdentity(request.RunId, request.RunVersion);
        var operationId = ProductionCostWriteContext.RequireOperationId(
            request.OperationId ?? request.ReopenDryRun?.OperationId);
        var runId = request.RunId!.Value;
        var runVersion = request.RunVersion!.Value;
        var requestPeriodYm = request.PeriodYm.Trim();

        // Reason + TargetStep từ request (ReopenDryRun) — trạng thái/version/history từ DB.
        var dryHint = request.ReopenDryRun
            ?? throw new ArgumentException(
                "ReopenDryRun bắt buộc cho Confirm REOPEN (TargetStepCode + Reason). " +
                "STATUS / RUN_VERSION / OperationId history đọc từ DB — không tin client hint.");
        if (string.IsNullOrWhiteSpace(dryHint.TargetStepCode))
            throw new ArgumentException("ReopenDryRun.TargetStepCode bắt buộc.");
        if (string.IsNullOrWhiteSpace(dryHint.Reason) || dryHint.Reason.Trim().Length < 3)
            throw new ArgumentException("ReopenDryRun.Reason bắt buộc (tối thiểu 3 ký tự).");

        LifecycleRequestGuard.AssertAlignedWithReopen(request, dryHint);

        return await ProductionCostWriteSession.ExecuteAsync(
            _writeRepo,
            databaseName!,
            async (session, _) =>
            {
                var locked = await _writeRepo.LockRunForUpdateAsync(session, companyCd, runId)
                    ?? throw new InvalidOperationException("RUN không tồn tại hoặc không thuộc COMPANY_CD.");
                AllocationRunConfig.AssertPeriodYmMatches(locked, requestPeriodYm);

                // History authoritative theo COMPANY_CD + OperationId TRƯỚC AssertLockedRunMatches —
                // cho phép nhận diện retry REOPEN đã commit sau khi RUN_VERSION tăng.
                var persisted = await _writeRepo.GetPersistedHistoryByOperationIdAsync(
                    session, companyCd, operationId);

                var requestFingerprint = ReopenConfirmPlanner.BuildContentFingerprint(
                    companyCd, runId, runVersion, dryHint.TargetStepCode, dryHint.Reason);

                var completedRetry = ReopenIdempotency.TryRecognizeCompleted(
                    companyCd,
                    runId,
                    locked.RunVersion,
                    locked.Status,
                    operationId,
                    persisted,
                    requestFingerprint,
                    runVersion,
                    dryHint.TargetStepCode,
                    dryHint.Reason);

                if (completedRetry != null)
                {
                    return new ProductionCostLifecycleWriteResponse
                    {
                        WriteEnabled = true,
                        Persisted = false,
                        PeriodYm = locked.PeriodYm.Trim(),
                        RunId = runId,
                        RunVersion = locked.RunVersion,
                        Action = HistoryActionCode.Reopen,
                        Reopen = completedRetry.Reopen,
                        StepStatusesAreClientSimulated = false,
                        IdempotencyAction = completedRetry.IdempotencyAction,
                        Message = completedRetry.Message
                    };
                }

                // Request mới (hoặc chưa chứng minh Noop) — bắt buộc khớp RUN_VERSION header.
                // Không bỏ qua kiểm tra version cho mọi request; không tăng version lần hai qua nhánh Noop.
                ProductionCostWriteContext.AssertLockedRunMatches(locked, companyCd, runId, runVersion);

                var period = await _writeRepo.LockPeriodEngineForUpdateAsync(
                        session, companyCd, locked.PeriodYm.Trim())
                    ?? throw new InvalidOperationException(
                        "production_cost_period không tồn tại cho PERIOD_YM.");
                ProductionCostWriteContext.AssertPeriodCurrentRunMatches(
                    locked, period, companyCd, runId, locked.PeriodYm.Trim());

                var periodYm = locked.PeriodYm.Trim();
                var steps = await _writeRepo.GetStepStatusesForUpdateAsync(session, companyCd, runId);

                var plan = ReopenConfirmPlanner.BuildFromDb(
                    companyCd,
                    userId,
                    runId,
                    locked.RunVersion,
                    locked.Status,
                    dryHint.TargetStepCode,
                    dryHint.Reason,
                    hasPermission: true,
                    operationId,
                    steps,
                    persisted,
                    clientFingerprint: requestFingerprint,
                    actionAtUtc: dryHint.ActionAtUtc,
                    requestOriginalRunVersion: runVersion);

                if (plan.IsNoop)
                {
                    return new ProductionCostLifecycleWriteResponse
                    {
                        WriteEnabled = true,
                        Persisted = false,
                        PeriodYm = periodYm,
                        RunId = runId,
                        RunVersion = locked.RunVersion,
                        Action = HistoryActionCode.Reopen,
                        Reopen = plan.Reopen,
                        StepStatusesAreClientSimulated = false,
                        IdempotencyAction = plan.IdempotencyAction,
                        Message = plan.Message
                    };
                }

                // Probe B3/B5 side-effects — block nếu chưa có reverse an toàn.
                var effects = await _writeRepo.ProbeReopenSideEffectsAsync(
                    session, companyCd, periodYm, runId, locked.RunVersion);
                ReopenImpactGuard.AssertSafeToPersist(
                    plan.Reopen.CascadeReverse,
                    effects,
                    _writeRepo.SupportsSafeReopenSideEffectReverse);

                await _writeRepo.PersistReopenAsync(
                    session,
                    companyCd,
                    runId,
                    plan.Reopen.PreservedRunVersion,
                    plan.Reopen.NewWorkingRunVersion,
                    plan.Reopen.MarkPriorVersionSuperseded,
                    plan.Reopen.Reason,
                    plan.Reopen.CascadeReverse,
                    userId,
                    plan.Reopen.History?.ActionAtUtc);

                // Đồng bộ in-TX: header đã bump — history RUN_VERSION = working version sau reopen.
                if (!plan.WouldWriteHistory || plan.Reopen.History == null)
                    throw new InvalidOperationException("REOPEN phải ghi history khi persist lần đầu.");

                var historyInsert = ProductionCostWriteContext.FromHistoryPlan(
                    plan.Reopen.History, companyCd, periodYm);
                historyInsert.ContentFingerprint = plan.ContentFingerprint;
                historyInsert.RequireContentFingerprint = true;
                historyInsert.ResultBefore =
                    $"status={locked.Status};v={locked.RunVersion};cas={string.Join('>', plan.Reopen.CascadeReverse)}";
                historyInsert.ResultAfter =
                    $"REOPENED;v={plan.Reopen.NewWorkingRunVersion};fp={plan.ContentFingerprint};" +
                    $"super={plan.Reopen.MarkPriorVersionSuperseded};del=0";

                var hist = await _writeRepo.TryInsertHistoryAsync(session, historyInsert, databaseName!);
                if (!hist.Inserted
                    && !string.Equals(
                        hist.IdempotencyAction,
                        HistoryPlan.WriteAction.NoopAlreadyWritten.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(hist.Message ?? "History REOPEN thất bại.");
                }

                return new ProductionCostLifecycleWriteResponse
                {
                    WriteEnabled = true,
                    Persisted = true,
                    PeriodYm = periodYm,
                    RunId = runId,
                    RunVersion = plan.Reopen.NewWorkingRunVersion,
                    Action = HistoryActionCode.Reopen,
                    Reopen = plan.Reopen,
                    StepStatusesAreClientSimulated = false,
                    IdempotencyAction = hist.IdempotencyAction,
                    Message =
                        "Confirm REOPEN OK — snapshot version cũ bất biến; " +
                        $"working v{plan.Reopen.NewWorkingRunVersion}; STATUS=REOPENED. " +
                        plan.Reopen.Message
                };
            });
    }

    public Task<ProductionCostLifecycleWriteResponse> PreviewRecalculateAsync(
        string companyCd,
        string? userId,
        ProductionCostWriteRequest request,
        string? databaseName = null)
    {
        EnsureAuth(companyCd);
        ValidatePeriod(request);
        RecalcPersistGuard.AssertNoConfirmRecalculateEndpoint();

        if (request.RecalcDryRun == null)
        {
            _gate.EnsureWritable();
            throw new InvalidOperationException(
                "Preview RECALCULATE từ DB chưa kích hoạt. Gửi RecalcDryRun payload. " +
                "Persist thật = Confirm từng bước B2–B5 trên working version sau REOPEN — " +
                "không ConfirmRecalculate / không ghi DryRun tài chính.");
        }

        LifecycleRequestGuard.AssertAlignedWithRecalc(request, request.RecalcDryRun);
        RecalcPersistGuard.AssertWritableWorkingVersion(
            companyCd,
            request.RecalcDryRun.RunId,
            request.RecalcDryRun.WorkingRunVersion,
            request.RecalcDryRun.HeaderRunVersion,
            request.RecalcDryRun.RunStatus);

        var plan = RecalcRules.Build(request.RecalcDryRun, companyCd, userId);
        RecalcPersistGuard.AssertPreviewOnly(plan);

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
            IdempotencyAction = plan.History?.IdempotencyAction,
            Message =
                "Preview RECALCULATE dry-run — không Confirm B2–B5 tự động; " +
                "không đổi chứng từ KT / kho; không overwrite CONFIRMED. " + plan.Message
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

    /// <summary>
    /// Fallback an toàn khi DI thiếu authority — Confirm REOPEN luôn bị chặn
    /// (không mặc định user login = có quyền REOPEN).
    /// </summary>
    private sealed class BlockedProductionCostReopenAuthority : IProductionCostReopenAuthority
    {
        public Task AssertCanConfirmReopenAsync(string companyCd, string? userId, string? databaseName)
        {
            throw new InvalidOperationException(
                "Confirm REOPEN bị chặn: thiếu IProductionCostReopenAuthority trong DI " +
                "(không mặc định mọi user đăng nhập đều có quyền REOPEN).");
        }
    }
}

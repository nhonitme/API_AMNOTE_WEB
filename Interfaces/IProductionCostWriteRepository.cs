using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;
using API_AMNOTE_WEB.Services.ProductionCost.Valuation;

namespace API_AMNOTE_WEB.Interfaces;

/// <summary>
/// Ghi G-K trong transaction do C# sở hữu (<see cref="DapperSession"/>).
/// SP không START/COMMIT. Confirm B2–B5 gọi qua service (B5 capability-gated).
/// </summary>
public interface IProductionCostWriteRepository
{
    /// <summary>
    /// true nếu repo hỗ trợ UPDATE <c>chit_inventory_input</c> theo INPUT_ID+COMPANY_CD
    /// trong cùng <see cref="DapperSession"/> (không dùng SP PRODUCT_CD).
    /// Production hiện <c>false</c> — Confirm B5 bị chặn đến khi DEV bàn giao SQL.
    /// </summary>
    bool SupportsInputIdSafeInventoryUpdate { get; }

    /// <summary>
    /// true nếu có đảo/khôi phục an toàn EXP_TO_154 / IS_APPLIED / giá kho / VALUATION_COGS
    /// trong cùng TX khi REOPEN cascade. Production = <c>false</c> → REOPEN bị block khi còn side-effect.
    /// </summary>
    bool SupportsSafeReopenSideEffectReverse { get; }

    /// <summary>
    /// Mở kết nối DB công ty + BEGIN TRANSACTION.
    /// Caller phải Commit hoặc Rollback (hoặc dùng <c>ProductionCostWriteSession</c>).
    /// </summary>
    Task<DapperSession> BeginWriteSessionAsync(string databaseName);

    /// <summary>Kiểm tra cột/index history bắt buộc trước khi ghi.</summary>
    Task<ProductionCostHistoryWriteSchemaProbe> ProbeHistoryWriteSchemaAsync(string databaseName);

    /// <summary>
    /// SELECT production_cost_run … FOR UPDATE theo COMPANY_CD + RUN_ID.
    /// </summary>
    Task<ProductionCostRunDto?> LockRunForUpdateAsync(
        DapperSession session,
        string companyCd,
        long runId);

    /// <summary>Đọc STATUS bước G-K trong TX (FOR UPDATE).</summary>
    Task<IReadOnlyDictionary<string, string>> GetStepStatusesForUpdateAsync(
        DapperSession session,
        string companyCd,
        long runId);

    /// <summary>OperationId đã persist (ISDEL=0) theo RUN — tiện cho dry-run.</summary>
    Task<IReadOnlyList<string>> GetPersistedOperationIdsAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int? runVersion = null);

    /// <summary>
    /// Hàng history theo COMPANY_CD + OPERATION_ID (phạm vi idempotency).
    /// </summary>
    Task<PersistedHistoryRef?> GetPersistedHistoryByOperationIdAsync(
        DapperSession session,
        string companyCd,
        string operationId);

    Task<bool> HistoryStableKeyExistsAsync(
        DapperSession session,
        string companyCd,
        string stableKey);

    /// <summary>
    /// INSERT history idempotent: Noop cùng OpId+StableKey; Conflict nếu khác;
    /// race UNIQUE → đọc lại DB (không chỉ SELECT trước INSERT).
    /// Từ chối nếu schema history chưa READY. Không Commit.
    /// </summary>
    Task<ProductionCostHistoryWriteResult> TryInsertHistoryAsync(
        DapperSession session,
        ProductionCostHistoryInsert insert,
        string databaseName);

    // ---- 2C-06B Allocation Confirm (B2) ----

    /// <summary>
    /// UPSERT pool theo UNIQUE (RUN_ID, RUN_VERSION, ACCOUNT_CD, DEPARTMENT_ID).
    /// Soft-delete khóa không còn trong kết quả mới. Không ghi đè RUN_VERSION khác.
    /// Chặn khi RUN.STATUS=ALLOC_CONFIRMED/CONFIRMED. Không Commit.
    /// </summary>
    Task ReplaceAllocPoolAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<AllocationPoolInput> pool,
        string? userId,
        string? runStatus,
        int lockedRunVersion);

    /// <summary>
    /// UPSERT alloc_detail theo UNIQUE nghiệp vụ. Soft-delete khóa thiếu.
    /// Chặn khi RUN.STATUS=ALLOC_CONFIRMED/CONFIRMED. Không Commit.
    /// </summary>
    Task ReplaceAllocDetailAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<AllocationLineResult> details,
        string? userId,
        string? runStatus,
        int lockedRunVersion);

    /// <summary>
    /// CALL sp_pc_alloc_confirm — SP không START/COMMIT; không ghi history (C# sở hữu history).
    /// </summary>
    Task<string> CallAllocConfirmAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        string? userId);

    // ---- 2C-06C Transfer Confirm (B3 EXP_TO_154_ONLY) ----

    /// <summary>SELECT production_cost_period … FOR UPDATE (COST_ENGINE / COGS_RULE).</summary>
    Task<ProductionCostPeriodEngineSnapshot?> LockPeriodEngineForUpdateAsync(
        DapperSession session,
        string companyCd,
        string periodYm);

    /// <summary>
    /// Đọc <c>acc_period_lock_step.COGS_SUMMARY.STATUS</c> FOR UPDATE (cùng TX).
    /// null = chưa có hàng (tương đương OPEN cho require_open).
    /// </summary>
    Task<string?> GetBiCogsStepStatusForUpdateAsync(
        DapperSession session,
        string companyCd,
        string periodYm);

    /// <summary>Σ ALLOCATED_AMOUNT 622/627 ISDEL=0 theo RUN_ID+VERSION.</summary>
    Task<decimal> GetConfirmedAllocTotalAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion);

    /// <summary>true nếu voucher_ref ISDEL=0 theo COMPANY_CD + STABLE_KEY.</summary>
    Task<bool> VoucherRefExistsAsync(
        DapperSession session,
        string companyCd,
        string stableKey);

    /// <summary>
    /// true nếu đã có history CONFIRM B3 (STEP_CODE=EXP_TO_154_ONLY) ISDEL=0 cho RUN+VERSION.
    /// </summary>
    Task<bool> HasB3ConfirmHistoryAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion);

    /// <summary>
    /// CALL PeriodLock COGS validate/calculate/create_voucher trên <b>cùng</b> session G-K.
    /// Không commit riêng. Rule = EXP_TO_154_ONLY only.
    /// </summary>
    Task CallPeriodLockExpTo154Async(
        DapperSession session,
        string companyCd,
        string periodYm,
        string? userId);

    /// <summary>UPSERT production_cost_step STATUS (B3 DONE). Không Commit.</summary>
    Task UpsertGkStepStatusAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int stepIndex,
        string stepCode,
        string status,
        string? userId);

    /// <summary>UPSERT production_cost_voucher_ref theo STABLE_KEY. Không Commit.</summary>
    Task UpsertVoucherRefAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        int stepIndex,
        string stepCode,
        string refRole,
        string stableKey,
        string? userId);

    // ---- 2C-06D Cost Calc Confirm (B4) ----

    /// <summary>
    /// true nếu đã có history CONFIRM B4 (STEP_CODE=COST_CALC) ISDEL=0 cho RUN+VERSION.
    /// </summary>
    Task<bool> HasB4ConfirmHistoryAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion);

    /// <summary>
    /// UPSERT production_cost_source theo UNIQUE (RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID, SOURCE_KIND).
    /// Soft-delete khóa thiếu. Không ghi XFER. Không Commit.
    /// </summary>
    Task ReplaceCostSourcesAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<CostSourceLine> sources,
        string? userId,
        string? runStatus,
        int lockedRunVersion);

    /// <summary>
    /// UPSERT production_cost_wip theo UNIQUE (RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID).
    /// Soft-delete khóa thiếu. Không đụng RUN_VERSION khác. Không Commit.
    /// </summary>
    Task ReplaceWipAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<WipCalcResult> wip,
        string? userId,
        string? runStatus,
        int lockedRunVersion);

    /// <summary>
    /// UPSERT production_cost_receipt theo UNIQUE (RUN_ID, RUN_VERSION, INPUT_ID).
    /// IS_APPLIED luôn '0' ở B4. Không UPDATE chit_inventory_input. Không Commit.
    /// </summary>
    Task ReplaceReceiptsAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<ReceiptPricePrepLine> receipts,
        string? userId,
        string? runStatus,
        int lockedRunVersion);

    // ---- 2C-06E Valuation Confirm (B5) ----

    /// <summary>
    /// true nếu đã có history CONFIRM B5 (STEP_CODE=VALUATION) ISDEL=0 cho RUN+VERSION.
    /// </summary>
    Task<bool> HasB5ConfirmHistoryAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion);

    /// <summary>
    /// Đọc dòng nhập kho theo INPUT_ID + COMPANY_CD trong TX (FOR UPDATE khi READY).
    /// </summary>
    Task<InventoryInputValuationRow?> GetInventoryInputByInputIdAsync(
        DapperSession session,
        string companyCd,
        long inputId);

    /// <summary>
    /// UPDATE giá nhập theo INPUT_ID + COMPANY_CD trên cùng session. Không Commit.
    /// Production: throw nếu !SupportsInputIdSafeInventoryUpdate.
    /// </summary>
    Task ApplyInventoryInputPriceByInputIdAsync(
        DapperSession session,
        string companyCd,
        ValuationPriceApplyPlanLine line,
        string? userId);

    /// <summary>
    /// Áp kết quả giá xuất (InventoryOutPriceEngine) theo nhóm trên cùng session. Không Commit.
    /// Production: throw nếu !SupportsInputIdSafeInventoryUpdate (cùng capability gate).
    /// </summary>
    Task ApplyInventoryOutPricesAsync(
        DapperSession session,
        string companyCd,
        string periodYm,
        IReadOnlyList<ValuationAverageResult> afterGroups,
        string? userId);

    /// <summary>
    /// UPSERT chứng từ VALUATION_COGS theo STABLE_KEY — InsertOnce hoặc UpdateExisting.
    /// Không INSERT chứng từ thứ hai. Không Commit.
    /// </summary>
    Task UpsertValuationCogsAsync(
        DapperSession session,
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        long? existingChitId,
        IReadOnlyList<Cogs632PreviewLine> cogs,
        string? userId);

    /// <summary>
    /// Đánh IS_APPLIED='1' trên production_cost_receipt theo RUN+VERSION+INPUT_ID. Không Commit.
    /// </summary>
    Task MarkReceiptsAppliedAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<long> inputIds,
        string? userId);

    // ---- 2C-06F REOPEN / History ----

    /// <summary>
    /// Probe tác động B3/B5 trước REOPEN (voucher EXP_TO_154, VALUATION_COGS, IS_APPLIED, claim kỳ).
    /// Không ghi. Không Commit.
    /// </summary>
    Task<ReopenSideEffectSnapshot> ProbeReopenSideEffectsAsync(
        DapperSession session,
        string companyCd,
        string periodYm,
        long runId,
        int runVersion);

    /// <summary>
    /// Persist REOPEN: bump/giữ RUN_VERSION, STATUS=REOPENED, REOPEN_REASON/BY/AT,
    /// cascade STEP→REOPENED. Không DELETE/overwrite snapshot version cũ. Không Commit.
    /// Không đảo chứng từ/kho (gọi sau <see cref="ReopenImpactGuard"/>).
    /// </summary>
    Task PersistReopenAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int preservedRunVersion,
        int newWorkingRunVersion,
        bool markPriorVersionSuperseded,
        string reason,
        IReadOnlyList<string> cascadeReverse,
        string? userId,
        DateTime? actionAtUtc = null);
}

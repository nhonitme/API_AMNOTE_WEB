using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces;

/// <summary>
/// Ghi G-K trong transaction do C# sở hữu (<see cref="DapperSession"/>).
/// SP không START/COMMIT. Confirm B2/B3 gọi qua service; B4–B5 chưa.
/// </summary>
public interface IProductionCostWriteRepository
{
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
}

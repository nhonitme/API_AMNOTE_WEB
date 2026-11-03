using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces;

/// <summary>
/// Ghi G-K trong transaction do C# sở hữu (<see cref="DapperSession"/>).
/// SP không START/COMMIT. Confirm B2 (2C-06B) gọi qua service; B3–B5 chưa.
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

    /// <summary>Ghi đè pool phiên hiện tại (soft-delete cũ + INSERT). Không Commit.</summary>
    Task ReplaceAllocPoolAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<AllocationPoolInput> pool,
        string? userId);

    /// <summary>Ghi đè alloc_detail theo UNIQUE key. Không Commit.</summary>
    Task ReplaceAllocDetailAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<AllocationLineResult> details,
        string? userId);

    /// <summary>
    /// CALL sp_pc_alloc_confirm — SP không START/COMMIT; không ghi history (C# sở hữu history).
    /// </summary>
    Task<string> CallAllocConfirmAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        string? userId);
}

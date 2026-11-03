using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

namespace API_AMNOTE_WEB.Repositories;

/// <summary>
/// Write path G-K — TX owner = caller C# qua <see cref="DapperSession"/>.
/// 2C-06A: session, lock run, history idempotency. Confirm B2–B5 = 2C-06B+.
/// </summary>
public sealed class ProductionCostWriteRepository : IProductionCostWriteRepository
{
    private readonly DapperExecutor _db;

    public ProductionCostWriteRepository(DapperExecutor db)
    {
        _db = db;
    }

    public async Task<DapperSession> BeginWriteSessionAsync(string databaseName)
    {
        ProductionCostWriteContext.EnsureDatabase(databaseName);
        return await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);
    }

    public async Task<ProductionCostHistoryWriteSchemaProbe> ProbeHistoryWriteSchemaAsync(string databaseName)
    {
        ProductionCostWriteContext.EnsureDatabase(databaseName);

        const string colSql = @"
SELECT COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND COLUMN_NAME IN ('OPERATION_ID', 'STABLE_KEY', 'STEP_CODE')";

        const string idxSql = @"
SELECT DISTINCT INDEX_NAME
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND INDEX_NAME IN ('uk_pc_hist_stable', 'idx_pc_hist_operation')";

        var cols = (await _db.QueryAsync<string>(Net_DB.Net_DB_Company, colSql, new { }, databaseName))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var idxs = (await _db.QueryAsync<string>(Net_DB.Net_DB_Company, idxSql, new { }, databaseName))
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingCols = ProductionCostWriteContext.RequiredHistoryWriteColumns
            .Where(c => !cols.Contains(c))
            .ToList();
        var missingIdx = ProductionCostWriteContext.RequiredHistoryWriteIndexes
            .Where(i => !idxs.Contains(i))
            .ToList();

        var ready = missingCols.Count == 0 && missingIdx.Count == 0;
        return new ProductionCostHistoryWriteSchemaProbe
        {
            Ready = ready,
            MissingColumns = missingCols,
            MissingIndexes = missingIdx,
            Message = ready
                ? "History write schema READY."
                : "History write schema INCOMPLETE — SOURCE 20261103_2c06a_ddl_history_operation.sql."
        };
    }

    public async Task<ProductionCostRunDto?> LockRunForUpdateAsync(
        DapperSession session,
        string companyCd,
        long runId)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (runId <= 0)
            throw new ArgumentException("RUN_ID phải > 0.");
        ArgumentNullException.ThrowIfNull(session);

        const string sql = @"
SELECT
    RUN_ID AS RunId,
    PERIOD_COST_ID AS PeriodCostId,
    COMPANY_CD AS CompanyCd,
    PERIOD_YM AS PeriodYm,
    RUN_VERSION AS RunVersion,
    STATUS AS Status,
    WIP_METHOD AS WipMethod,
    ALLOCATE_TYPE AS AllocateType,
    IS_622_627_BY_DEPARTMENT AS Is622627ByDepartment,
    IS_6212 AS Is6212,
    ALLOC_NUMBER_DIGIT AS AllocNumberDigit,
    INCOMPLETE_MODE AS IncompleteMode,
    INCOMPLETE_PERCENT AS IncompletePercent,
    ENABLE_PRODUCT_REDUCE AS EnableProductReduce,
    CONFIRMED_BY AS ConfirmedBy,
    CONFIRMED_AT AS ConfirmedAt,
    REOPEN_REASON AS ReopenReason,
    REOPENED_AT AS ReopenedAt
FROM production_cost_run
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND ISDEL = '0'
LIMIT 1
FOR UPDATE";

        return await session.QueryFirstOrDefaultAsync<ProductionCostRunDto>(
            sql,
            new { CompanyCd = companyCd.Trim(), RunId = runId });
    }

    public async Task<IReadOnlyDictionary<string, string>> GetStepStatusesForUpdateAsync(
        DapperSession session,
        string companyCd,
        long runId)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (runId <= 0)
            throw new ArgumentException("RUN_ID phải > 0.");
        ArgumentNullException.ThrowIfNull(session);

        const string sql = @"
SELECT STEP_CODE AS StepCode, STATUS AS Status
FROM production_cost_step
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND ISDEL = '0'
FOR UPDATE";

        var rows = await session.QueryAsync<StepStatusRow>(
            sql,
            new { CompanyCd = companyCd.Trim(), RunId = runId });

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.StepCode))
                continue;
            map[row.StepCode.Trim()] = (row.Status ?? string.Empty).Trim();
        }

        return map;
    }

    public async Task<IReadOnlyList<string>> GetPersistedOperationIdsAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int? runVersion = null)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (runId <= 0)
            throw new ArgumentException("RUN_ID phải > 0.");
        ArgumentNullException.ThrowIfNull(session);

        const string sql = @"
SELECT OPERATION_ID
FROM production_cost_history
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND ISDEL = '0'
  AND OPERATION_ID IS NOT NULL
  AND TRIM(OPERATION_ID) <> ''
  AND (@RunVersion IS NULL OR RUN_VERSION = @RunVersion)";

        var rows = await session.QueryAsync<string>(
            sql,
            new
            {
                CompanyCd = companyCd.Trim(),
                RunId = runId,
                RunVersion = runVersion
            });

        return rows
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> HistoryStableKeyExistsAsync(
        DapperSession session,
        string companyCd,
        string stableKey)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("History STABLE_KEY bắt buộc.");
        ArgumentNullException.ThrowIfNull(session);

        const string sql = @"
SELECT 1
FROM production_cost_history
WHERE COMPANY_CD = @CompanyCd
  AND STABLE_KEY = @StableKey
  AND ISDEL = '0'
LIMIT 1";

        var hit = await session.QueryFirstOrDefaultAsync<int?>(
            sql,
            new { CompanyCd = companyCd.Trim(), StableKey = stableKey.Trim() });
        return hit.HasValue;
    }

    public async Task<ProductionCostHistoryWriteResult> TryInsertHistoryAsync(
        DapperSession session,
        ProductionCostHistoryInsert insert)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(insert);

        ProductionCostWriteContext.EnsureCompany(insert.CompanyCd);
        ProductionCostWriteContext.EnsureRunIdentity(insert.RunId, insert.RunVersion ?? 0);
        if (string.IsNullOrWhiteSpace(insert.PeriodYm) || insert.PeriodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");
        if (string.IsNullOrWhiteSpace(insert.ActionCode))
            throw new ArgumentException("ACTION_CODE bắt buộc.");

        var operationId = ProductionCostWriteContext.RequireOperationId(insert.OperationId);
        if (string.IsNullOrWhiteSpace(insert.StableKey))
            throw new ArgumentException("History STABLE_KEY bắt buộc.");

        var companyCd = insert.CompanyCd.Trim();
        var stableKey = insert.StableKey.Trim();

        // Idempotency: ưu tiên UNIQUE STABLE_KEY đã persist trong DB (không tin client).
        if (await HistoryStableKeyExistsAsync(session, companyCd, stableKey))
        {
            return new ProductionCostHistoryWriteResult
            {
                Inserted = false,
                IdempotencyAction = HistoryPlan.WriteAction.NoopAlreadyWritten.ToString(),
                OperationId = operationId,
                StableKey = stableKey,
                Message = "History STABLE_KEY đã tồn tại — Noop."
            };
        }

        var persistedOps = await GetPersistedOperationIdsAsync(
            session, companyCd, insert.RunId, insert.RunVersion);
        var plan = HistoryPlan.PlanWrite(stableKey, operationId, persistedOps);
        if (!plan.WritesHistory)
        {
            return new ProductionCostHistoryWriteResult
            {
                Inserted = false,
                IdempotencyAction = plan.Action.ToString(),
                OperationId = operationId,
                StableKey = stableKey,
                Message = "OperationId đã persist — Noop."
            };
        }

        const string insertSql = @"
INSERT INTO production_cost_history (
    RUN_ID, COMPANY_CD, PERIOD_YM, STEP_INDEX, STEP_CODE, ACTION_CODE,
    OPERATION_ID, STABLE_KEY, STATUS_AFTER, RUN_VERSION,
    RESULT_BEFORE, RESULT_AFTER, DIFFERENCE_TEXT, REASON,
    USER_NAME, ACTION_AT, ISDEL
) VALUES (
    @RunId, @CompanyCd, @PeriodYm, @StepIndex, @StepCode, @ActionCode,
    @OperationId, @StableKey, @StatusAfter, @RunVersion,
    @ResultBefore, @ResultAfter, @DifferenceText, @Reason,
    @UserName, @ActionAt, '0'
)";

        await session.ExecuteAsync(insertSql, new
        {
            insert.RunId,
            CompanyCd = companyCd,
            PeriodYm = insert.PeriodYm.Trim(),
            insert.StepIndex,
            StepCode = string.IsNullOrWhiteSpace(insert.StepCode) ? null : insert.StepCode.Trim(),
            ActionCode = insert.ActionCode.Trim(),
            OperationId = operationId,
            StableKey = stableKey,
            insert.StatusAfter,
            insert.RunVersion,
            insert.ResultBefore,
            insert.ResultAfter,
            insert.DifferenceText,
            insert.Reason,
            insert.UserName,
            ActionAt = insert.ActionAt ?? DateTime.UtcNow
        });

        var historyId = await session.QueryFirstOrDefaultAsync<long?>("SELECT LAST_INSERT_ID()");

        return new ProductionCostHistoryWriteResult
        {
            Inserted = true,
            IdempotencyAction = HistoryPlan.WriteAction.InsertOnce.ToString(),
            HistoryId = historyId,
            OperationId = operationId,
            StableKey = stableKey,
            Message = "History inserted."
        };
    }

    private sealed class StepStatusRow
    {
        public string? StepCode { get; set; }
        public string? Status { get; set; }
    }
}

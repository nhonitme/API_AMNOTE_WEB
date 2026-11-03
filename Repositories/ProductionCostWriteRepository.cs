using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
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
  AND COLUMN_NAME IN ('OPERATION_ID', 'STABLE_KEY', 'STEP_CODE', 'CONTENT_FINGERPRINT')";

        const string idxSql = @"
SELECT DISTINCT INDEX_NAME
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'production_cost_history'
  AND INDEX_NAME IN ('uk_pc_hist_stable', 'uk_pc_hist_operation')";

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

    public async Task<PersistedHistoryRef?> GetPersistedHistoryByOperationIdAsync(
        DapperSession session,
        string companyCd,
        string operationId)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        var op = HistoryPlan.RequireOperationId(operationId);
        ArgumentNullException.ThrowIfNull(session);

        const string sql = @"
SELECT
    OPERATION_ID AS OperationId,
    STABLE_KEY AS StableKey,
    CONTENT_FINGERPRINT AS ContentFingerprint
FROM production_cost_history
WHERE COMPANY_CD = @CompanyCd
  AND OPERATION_ID = @OperationId
  AND ISDEL = '0'
LIMIT 1";

        return await session.QueryFirstOrDefaultAsync<PersistedHistoryRef>(
            sql,
            new { CompanyCd = companyCd.Trim(), OperationId = op });
    }

    public async Task<bool> HistoryStableKeyExistsAsync(
        DapperSession session,
        string companyCd,
        string stableKey)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        HistoryPlan.EnsureStableKeyLength(stableKey);
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
        ProductionCostHistoryInsert insert,
        string databaseName)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(insert);

        var probe = await ProbeHistoryWriteSchemaAsync(databaseName);
        ProductionCostWriteContext.AssertHistoryWriteSchemaReady(probe);

        ProductionCostWriteContext.EnsureCompany(insert.CompanyCd);
        ProductionCostWriteContext.EnsureRunIdentity(insert.RunId, insert.RunVersion ?? 0);
        if (string.IsNullOrWhiteSpace(insert.PeriodYm) || insert.PeriodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");
        if (string.IsNullOrWhiteSpace(insert.ActionCode))
            throw new ArgumentException("ACTION_CODE bắt buộc.");

        var operationId = ProductionCostWriteContext.RequireOperationId(insert.OperationId);
        HistoryPlan.EnsureStableKeyLength(insert.StableKey);

        var companyCd = insert.CompanyCd.Trim();
        var stableKey = insert.StableKey.Trim();
        // Confirm (RequireContentFingerprint) hoặc request đã gửi fingerprint → so khớp nghiêm;
        // thiếu persisted fingerprint ≠ khớp. Reopen không fingerprint → soft.
        var fingerprint = HistoryPlan.NormalizeContentFingerprint(
            insert.ContentFingerprint, required: insert.RequireContentFingerprint);
        var requireFp = insert.RequireContentFingerprint || fingerprint != null;

        // 1) Đọc authoritative theo COMPANY_CD + OPERATION_ID (không tin client).
        var existing = await GetPersistedHistoryByOperationIdAsync(session, companyCd, operationId);
        var plan = HistoryPlan.PlanWrite(
            stableKey,
            operationId,
            existing == null ? Array.Empty<PersistedHistoryRef>() : new[] { existing },
            fingerprint,
            requireContentFingerprint: requireFp);

        if (!plan.WritesHistory)
            return HistoryPlan.ToWriteResult(plan);

        const string insertSql = @"
INSERT INTO production_cost_history (
    RUN_ID, COMPANY_CD, PERIOD_YM, STEP_INDEX, STEP_CODE, ACTION_CODE,
    OPERATION_ID, STABLE_KEY, CONTENT_FINGERPRINT, STATUS_AFTER, RUN_VERSION,
    RESULT_BEFORE, RESULT_AFTER, DIFFERENCE_TEXT, REASON,
    USER_NAME, ACTION_AT, ISDEL
) VALUES (
    @RunId, @CompanyCd, @PeriodYm, @StepIndex, @StepCode, @ActionCode,
    @OperationId, @StableKey, @ContentFingerprint, @StatusAfter, @RunVersion,
    @ResultBefore, @ResultAfter, @DifferenceText, @Reason,
    @UserName, @ActionAt, '0'
)";

        try
        {
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
                ContentFingerprint = fingerprint,
                insert.StatusAfter,
                insert.RunVersion,
                insert.ResultBefore,
                insert.ResultAfter,
                insert.DifferenceText,
                insert.Reason,
                insert.UserName,
                ActionAt = insert.ActionAt ?? DateTime.UtcNow
            });
        }
        catch (Exception ex) when (HistoryPlan.IsDuplicateKeyException(ex))
        {
            // 2) Race 1062: TX khác đã commit — đọc lại CONTENT_FINGERPRINT thật, không INSERT lần 2.
            var again = await GetPersistedHistoryByOperationIdAsync(session, companyCd, operationId);
            var resolved = HistoryPlan.ResolveAfterDuplicateKey(
                stableKey, operationId, again, fingerprint, requireContentFingerprint: requireFp);
            return HistoryPlan.ToWriteResult(resolved);
        }

        var historyId = await session.QueryFirstOrDefaultAsync<long?>("SELECT LAST_INSERT_ID()");
        return HistoryPlan.ToWriteResult(plan, historyId);
    }

    public async Task ReplaceAllocPoolAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<AllocationPoolInput> pool,
        string? userId,
        string? runStatus,
        int lockedRunVersion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(pool);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        AllocUpsertRules.EnsureCanPersist(
            companyCd, runId, runVersion, lockedRunVersion, runStatus);

        // UPSERT theo UNIQUE (không gồm ISDEL) — tránh duplicate khi hàng ISDEL='1' đã tồn tại.
        const string upsertSql = @"
INSERT INTO production_cost_alloc_pool (
    RUN_ID, RUN_VERSION, COMPANY_CD, ACCOUNT_CD, DEPARTMENT_ID, DEPARTMENT_CD,
    TOTAL_AMOUNT, CREDIT_AMOUNT, DEBIT_AMOUNT_154, REDUCTION, ALLOCATION_COSTS,
    ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
    @RunId, @RunVersion, @CompanyCd, @AccountCd, @DepartmentId, @DepartmentCd,
    @TotalAmount, @CreditAmount, @DebitAmount154, @Reduction, @AllocationCosts,
    '0', @UserId, UTC_TIMESTAMP(), @UserId, UTC_TIMESTAMP()
)
ON DUPLICATE KEY UPDATE
    COMPANY_CD = VALUES(COMPANY_CD),
    DEPARTMENT_CD = VALUES(DEPARTMENT_CD),
    TOTAL_AMOUNT = VALUES(TOTAL_AMOUNT),
    CREDIT_AMOUNT = VALUES(CREDIT_AMOUNT),
    DEBIT_AMOUNT_154 = VALUES(DEBIT_AMOUNT_154),
    REDUCTION = VALUES(REDUCTION),
    ALLOCATION_COSTS = VALUES(ALLOCATION_COSTS),
    ISDEL = '0',
    UPDATE_BY = VALUES(UPDATE_BY),
    UPDATE_AT = UTC_TIMESTAMP()";

        var keepKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in pool)
        {
            var accountCd = (row.AccountCd ?? string.Empty).Trim();
            await session.ExecuteAsync(upsertSql, new
            {
                RunId = runId,
                RunVersion = runVersion,
                CompanyCd = companyCd.Trim(),
                AccountCd = accountCd,
                DepartmentId = row.DepartmentId,
                row.DepartmentCd,
                row.TotalAmount,
                row.CreditAmount,
                row.DebitAmount154,
                row.Reduction,
                row.AllocationCosts,
                UserId = userId
            });
            keepKeys.Add($"{accountCd}\u0001{row.DepartmentId}");
        }

        // Soft-delete khóa không còn trong kết quả mới — chỉ đúng RUN_VERSION đang ghi.
        const string softDeleteSql = @"
UPDATE production_cost_alloc_pool
SET ISDEL = '1', UPDATE_BY = @UserId, UPDATE_AT = UTC_TIMESTAMP()
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
  AND CONCAT(ACCOUNT_CD, CHAR(1), DEPARTMENT_ID) NOT IN @KeepKeys";

        if (keepKeys.Count == 0)
        {
            await session.ExecuteAsync(@"
UPDATE production_cost_alloc_pool
SET ISDEL = '1', UPDATE_BY = @UserId, UPDATE_AT = UTC_TIMESTAMP()
WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0'",
                new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion, UserId = userId });
        }
        else
        {
            await session.ExecuteAsync(
                softDeleteSql,
                new
                {
                    CompanyCd = companyCd.Trim(),
                    RunId = runId,
                    RunVersion = runVersion,
                    UserId = userId,
                    KeepKeys = keepKeys.ToArray()
                });
        }
    }

    public async Task ReplaceAllocDetailAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<AllocationLineResult> details,
        string? userId,
        string? runStatus,
        int lockedRunVersion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(details);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        AllocUpsertRules.EnsureCanPersist(
            companyCd, runId, runVersion, lockedRunVersion, runStatus);

        AllocDetailKey.AssertUnique(details.Select(d =>
            AllocDetailKey.Of(d.ProductId, d.DepartmentId, d.AccountCd, d.PoolDepartmentId)));

        const string upsertSql = @"
INSERT INTO production_cost_alloc_detail (
    RUN_ID, RUN_VERSION, COMPANY_CD, PRODUCT_ID, DEPARTMENT_ID, POOL_DEPARTMENT_ID,
    PRODUCT_CD, DEPARTMENT_CD, AMOUNT_QUANTITY, QUANTITY, AMOUNT_6211, AMOUNT_6212,
    ACCOUNT_CD, ALLOCATED_AMOUNT, IS_LAST_ROW, AMOUNT_622_SUM, AMOUNT_627_SUM,
    ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
    @RunId, @RunVersion, @CompanyCd, @ProductId, @DepartmentId, @PoolDepartmentId,
    @ProductCd, @DepartmentCd, @AmountQuantity, @Quantity, @Amount6211, @Amount6212,
    @AccountCd, @AllocatedAmount, @IsLastRow, @Amount622Sum, @Amount627Sum,
    '0', @UserId, UTC_TIMESTAMP(), @UserId, UTC_TIMESTAMP()
)
ON DUPLICATE KEY UPDATE
    COMPANY_CD = VALUES(COMPANY_CD),
    PRODUCT_CD = VALUES(PRODUCT_CD),
    DEPARTMENT_CD = VALUES(DEPARTMENT_CD),
    AMOUNT_QUANTITY = VALUES(AMOUNT_QUANTITY),
    QUANTITY = VALUES(QUANTITY),
    AMOUNT_6211 = VALUES(AMOUNT_6211),
    AMOUNT_6212 = VALUES(AMOUNT_6212),
    ALLOCATED_AMOUNT = VALUES(ALLOCATED_AMOUNT),
    IS_LAST_ROW = VALUES(IS_LAST_ROW),
    AMOUNT_622_SUM = VALUES(AMOUNT_622_SUM),
    AMOUNT_627_SUM = VALUES(AMOUNT_627_SUM),
    ISDEL = '0',
    UPDATE_BY = VALUES(UPDATE_BY),
    UPDATE_AT = UTC_TIMESTAMP()";

        var keepKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in details)
        {
            var accountCd = (line.AccountCd ?? string.Empty).Trim();
            await session.ExecuteAsync(upsertSql, new
            {
                RunId = runId,
                RunVersion = runVersion,
                CompanyCd = companyCd.Trim(),
                line.ProductId,
                line.DepartmentId,
                line.PoolDepartmentId,
                line.ProductCd,
                line.DepartmentCd,
                line.AmountQuantity,
                line.Quantity,
                line.Amount6211,
                line.Amount6212,
                AccountCd = accountCd,
                line.AllocatedAmount,
                IsLastRow = line.IsLastRow ? "1" : "0",
                line.Amount622Sum,
                line.Amount627Sum,
                UserId = userId
            });
            keepKeys.Add($"{line.ProductId}\u0001{line.DepartmentId}\u0001{accountCd}\u0001{line.PoolDepartmentId}");
        }

        const string softDeleteSql = @"
UPDATE production_cost_alloc_detail
SET ISDEL = '1', UPDATE_BY = @UserId, UPDATE_AT = UTC_TIMESTAMP()
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
  AND CONCAT(PRODUCT_ID, CHAR(1), DEPARTMENT_ID, CHAR(1), ACCOUNT_CD, CHAR(1), POOL_DEPARTMENT_ID) NOT IN @KeepKeys";

        if (keepKeys.Count == 0)
        {
            await session.ExecuteAsync(@"
UPDATE production_cost_alloc_detail
SET ISDEL = '1', UPDATE_BY = @UserId, UPDATE_AT = UTC_TIMESTAMP()
WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0'",
                new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion, UserId = userId });
        }
        else
        {
            await session.ExecuteAsync(
                softDeleteSql,
                new
                {
                    CompanyCd = companyCd.Trim(),
                    RunId = runId,
                    RunVersion = runVersion,
                    UserId = userId,
                    KeepKeys = keepKeys.ToArray()
                });
        }
    }

    public async Task<string> CallAllocConfirmAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);

        // SP không START/COMMIT; bản 2C-06B không INSERT history (C# sở hữu).
        var rows = await session.QueryAsync<AllocConfirmSpRow>(
            "CALL sp_pc_alloc_confirm(@p_COMPANY_CD, @p_RUN_ID, @p_RUN_VERSION, @p_USER_ID)",
            new
            {
                p_COMPANY_CD = companyCd.Trim(),
                p_RUN_ID = runId,
                p_RUN_VERSION = runVersion,
                p_USER_ID = userId ?? string.Empty
            });

        var result = rows.FirstOrDefault()?.RESULT;
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("sp_pc_alloc_confirm không trả RESULT.");
        return result.Trim();
    }

    public async Task<ProductionCostPeriodEngineSnapshot?> LockPeriodEngineForUpdateAsync(
        DapperSession session,
        string companyCd,
        string periodYm)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        const string sql = @"
SELECT
    PERIOD_COST_ID AS PeriodCostId,
    COMPANY_CD AS CompanyCd,
    PERIOD_YM AS PeriodYm,
    CURRENT_RUN_ID AS CurrentRunId,
    COST_ENGINE AS CostEngine,
    COGS_RULE AS CogsRule,
    STATUS AS Status
FROM production_cost_period
WHERE COMPANY_CD = @CompanyCd
  AND PERIOD_YM = @PeriodYm
  AND ISDEL = '0'
LIMIT 1
FOR UPDATE";

        return await session.QueryFirstOrDefaultAsync<ProductionCostPeriodEngineSnapshot>(
            sql,
            new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm.Trim() });
    }

    public async Task<decimal> GetConfirmedAllocTotalAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);

        const string sql = @"
SELECT IFNULL(SUM(ALLOCATED_AMOUNT), 0)
FROM production_cost_alloc_detail
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
  AND (ACCOUNT_CD LIKE '622%' OR ACCOUNT_CD LIKE '627%')";

        return await session.QueryFirstOrDefaultAsync<decimal>(
            sql,
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion });
    }

    public async Task CallPeriodLockExpTo154Async(
        DapperSession session,
        string companyCd,
        string periodYm,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        // Cùng DapperSession G-K — SP PeriodLock không START/COMMIT.
        var rule = ProductionCostCogsRule.ExpTo154Only;
        var uid = userId ?? string.Empty;
        var p = new
        {
            p_COMPANY_CD = companyCd.Trim(),
            p_PERIOD_YM = periodYm.Trim(),
            p_RULE_CODE = rule,
            p_USER_ID = uid
        };

        await session.ExecuteAsync(
            "CALL sp_period_lock_cogs_validate(@p_COMPANY_CD, @p_PERIOD_YM, @p_RULE_CODE, @p_USER_ID)",
            p);
        await session.ExecuteAsync(
            "CALL sp_period_lock_cogs_calculate(@p_COMPANY_CD, @p_PERIOD_YM, @p_RULE_CODE, @p_USER_ID)",
            p);
        // create_voucher: claim + build + post EXP_TO_154 only (không 154→155/632 khi rule EXP_TO_154_ONLY).
        await session.ExecuteAsync(
            "CALL sp_period_lock_cogs_create_voucher(@p_COMPANY_CD, @p_PERIOD_YM, @p_RULE_CODE, @p_USER_ID)",
            p);
    }

    public async Task UpsertGkStepStatusAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int stepIndex,
        string stepCode,
        string status,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (runId <= 0)
            throw new ArgumentException("RUN_ID phải > 0.");
        if (string.IsNullOrWhiteSpace(stepCode))
            throw new ArgumentException("STEP_CODE bắt buộc.");
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("STATUS bắt buộc.");

        const string sql = @"
INSERT INTO production_cost_step (
    RUN_ID, COMPANY_CD, STEP_INDEX, STEP_CODE, STATUS,
    ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
    @RunId, @CompanyCd, @StepIndex, @StepCode, @Status,
    '0', @UserId, UTC_TIMESTAMP(), @UserId, UTC_TIMESTAMP()
)
ON DUPLICATE KEY UPDATE
    STEP_CODE = VALUES(STEP_CODE),
    STATUS = VALUES(STATUS),
    ISDEL = '0',
    UPDATE_BY = VALUES(UPDATE_BY),
    UPDATE_AT = UTC_TIMESTAMP()";

        await session.ExecuteAsync(sql, new
        {
            RunId = runId,
            CompanyCd = companyCd.Trim(),
            StepIndex = stepIndex,
            StepCode = stepCode.Trim(),
            Status = status.Trim(),
            UserId = userId
        });
    }

    public async Task UpsertVoucherRefAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        int stepIndex,
        string stepCode,
        string refRole,
        string stableKey,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("STABLE_KEY bắt buộc.");

        const string sql = @"
INSERT INTO production_cost_voucher_ref (
    RUN_ID, COMPANY_CD, STEP_INDEX, STEP_CODE, REF_ROLE, STABLE_KEY,
    CHIT_ID, RUN_VERSION, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
    @RunId, @CompanyCd, @StepIndex, @StepCode, @RefRole, @StableKey,
    NULL, @RunVersion, '0', @UserId, UTC_TIMESTAMP(), @UserId, UTC_TIMESTAMP()
)
ON DUPLICATE KEY UPDATE
    RUN_ID = VALUES(RUN_ID),
    RUN_VERSION = VALUES(RUN_VERSION),
    STEP_INDEX = VALUES(STEP_INDEX),
    STEP_CODE = VALUES(STEP_CODE),
    REF_ROLE = VALUES(REF_ROLE),
    UPDATE_BY = VALUES(UPDATE_BY),
    UPDATE_AT = UTC_TIMESTAMP(),
    ISDEL = '0'";

        await session.ExecuteAsync(sql, new
        {
            RunId = runId,
            CompanyCd = companyCd.Trim(),
            StepIndex = stepIndex,
            StepCode = stepCode.Trim(),
            RefRole = refRole.Trim(),
            StableKey = stableKey.Trim(),
            RunVersion = runVersion,
            UserId = userId
        });
    }

    private sealed class StepStatusRow
    {
        public string? StepCode { get; set; }
        public string? Status { get; set; }
    }

    private sealed class AllocConfirmSpRow
    {
        public string? RESULT { get; set; }
        public long? RUN_ID { get; set; }
        public int? RUN_VERSION { get; set; }
    }
}

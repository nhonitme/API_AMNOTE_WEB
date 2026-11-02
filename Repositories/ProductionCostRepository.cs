using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories;

/// <summary>
/// Repository đọc G-K — chỉ SELECT parameterized; JOIN theo *_ID + COMPANY_CD.
/// Không CALL stored procedure ghi.
/// </summary>
public sealed class ProductionCostRepository : IProductionCostRepository
{
    private static readonly string[] RequiredTables =
    {
        "production_cost_period",
        "production_cost_run",
        "production_cost_step",
        "production_cost_object",
        "production_cost_alloc_pool",
        "production_cost_alloc_detail",
        "production_cost_nvl_basis",
        "production_cost_nvl_basis_line",
        "production_cost_nvl_orphan",
        "production_cost_source",
        "production_cost_wip",
        "production_cost_receipt",
        "production_cost_voucher_ref",
        "production_cost_history"
    };

    private readonly DapperExecutor _db;

    public ProductionCostRepository(DapperExecutor db)
    {
        _db = db;
    }

    public async Task<ProductionCostSchemaProbe> ProbeSchemaAsync(string? databaseName = null)
    {
        EnsureDatabase(databaseName);

        const string sql = @"
SELECT TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'production_cost_%'";

        var existing = (await _db.QueryAsync<string>(Net_DB.Net_DB_Company, sql, null, databaseName))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (existing.Count == 0)
        {
            return new ProductionCostSchemaProbe
            {
                Status = ProductionCostSchemaStatus.Missing,
                MissingTables = RequiredTables
            };
        }

        var missing = RequiredTables
            .Where(t => !existing.Contains(t))
            .ToList();

        if (missing.Count > 0)
        {
            return new ProductionCostSchemaProbe
            {
                Status = ProductionCostSchemaStatus.Incomplete,
                MissingTables = missing
            };
        }

        return new ProductionCostSchemaProbe
        {
            Status = ProductionCostSchemaStatus.Ready,
            MissingTables = Array.Empty<string>()
        };
    }

    private static void EnsureDatabase(string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("Database name is required; refusing silent fallback to an unknown company database.");
    }

    public async Task<ProductionCostPeriodDto?> GetPeriodRowAsync(string companyCd, string periodYm, string? databaseName = null)
    {
        EnsureDatabase(databaseName);
        const string sql = @"
SELECT
    PERIOD_COST_ID AS PeriodCostId,
    COMPANY_CD AS CompanyCd,
    PERIOD_YM AS PeriodYm,
    FROM_YMD AS FromYmd,
    TO_YMD AS ToYmd,
    CURRENT_RUN_ID AS CurrentRunId,
    STATUS AS Status,
    COST_ENGINE AS CostEngine,
    COGS_RULE AS CogsRule
FROM production_cost_period
WHERE COMPANY_CD = @CompanyCd
  AND PERIOD_YM = @PeriodYm
  AND ISDEL = '0'
LIMIT 1";

        return await _db.QueryFirstOrDefaultAsync<ProductionCostPeriodDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, PeriodYm = periodYm },
            databaseName);
    }

    public async Task<ProductionCostRunDto?> GetRunAsync(string companyCd, long runId, string? databaseName = null)
    {
        EnsureDatabase(databaseName);
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
LIMIT 1";

        return await _db.QueryFirstOrDefaultAsync<ProductionCostRunDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId },
            databaseName);
    }

    public async Task<ProductionCostRunDto?> GetCurrentRunAsync(string companyCd, string periodYm, string? databaseName = null)
    {
        EnsureDatabase(databaseName);
        const string sql = @"
SELECT
    r.RUN_ID AS RunId,
    r.PERIOD_COST_ID AS PeriodCostId,
    r.COMPANY_CD AS CompanyCd,
    r.PERIOD_YM AS PeriodYm,
    r.RUN_VERSION AS RunVersion,
    r.STATUS AS Status,
    r.WIP_METHOD AS WipMethod,
    r.ALLOCATE_TYPE AS AllocateType,
    r.IS_622_627_BY_DEPARTMENT AS Is622627ByDepartment,
    r.IS_6212 AS Is6212,
    r.ALLOC_NUMBER_DIGIT AS AllocNumberDigit,
    r.INCOMPLETE_MODE AS IncompleteMode,
    r.INCOMPLETE_PERCENT AS IncompletePercent,
    r.ENABLE_PRODUCT_REDUCE AS EnableProductReduce,
    r.CONFIRMED_BY AS ConfirmedBy,
    r.CONFIRMED_AT AS ConfirmedAt,
    r.REOPEN_REASON AS ReopenReason,
    r.REOPENED_AT AS ReopenedAt
FROM production_cost_period p
INNER JOIN production_cost_run r
    ON r.RUN_ID = p.CURRENT_RUN_ID
   AND r.COMPANY_CD = p.COMPANY_CD
   AND r.ISDEL = '0'
WHERE p.COMPANY_CD = @CompanyCd
  AND p.PERIOD_YM = @PeriodYm
  AND p.ISDEL = '0'
LIMIT 1";

        return await _db.QueryFirstOrDefaultAsync<ProductionCostRunDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, PeriodYm = periodYm },
            databaseName);
    }

    public async Task<ProductionCostRunDto?> ResolveRunAsync(
        string companyCd,
        string periodYm,
        long? runId,
        int? runVersion,
        string? databaseName = null)
    {
        EnsureDatabase(databaseName);
        if (runId.HasValue && runId.Value > 0)
        {
            // Bắt buộc thuộc đúng COMPANY_CD (filter GetRun) + PERIOD_YM.
            var byId = await GetRunAsync(companyCd, runId.Value, databaseName);
            if (byId == null)
                return null;
            if (!string.Equals(byId.PeriodYm, periodYm, StringComparison.Ordinal))
                return null;
            // Header giữ RUN_VERSION hiện tại; version cũ đọc từ bảng con — trả header cho caller.
            _ = runVersion;
            return byId;
        }

        return await GetCurrentRunAsync(companyCd, periodYm, databaseName);
    }

    public async Task<bool> HasRunVersionDetailAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        EnsureDatabase(databaseName);
        const string sql = @"
SELECT (
    EXISTS(SELECT 1 FROM production_cost_alloc_pool
           WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0')
    OR EXISTS(SELECT 1 FROM production_cost_alloc_detail
              WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0')
    OR EXISTS(SELECT 1 FROM production_cost_wip
              WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0')
    OR EXISTS(SELECT 1 FROM production_cost_source
              WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0')
    OR EXISTS(SELECT 1 FROM production_cost_receipt
              WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0')
    OR EXISTS(SELECT 1 FROM production_cost_nvl_basis
              WHERE COMPANY_CD = @CompanyCd AND RUN_ID = @RunId AND RUN_VERSION = @RunVersion AND ISDEL = '0')
) AS HasDetail";

        var flag = await _db.QueryFirstAsync<int>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return flag != 0;
    }

    public async Task<IReadOnlyList<ProductionCostStepDto>> GetGkStepsAsync(string companyCd, long runId, string? databaseName = null)
    {
        EnsureDatabase(databaseName);
        const string sql = @"
SELECT
    STEP_INDEX AS StepIndex,
    STEP_CODE AS StepCode,
    ENGINE AS Engine,
    STATUS AS Status,
    BI_STEP_CODE AS BiStepCode,
    RESULT_SUMMARY AS ResultSummary,
    COMPLETED_BY AS CompletedBy,
    COMPLETED_AT AS CompletedAt
FROM production_cost_step
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND ISDEL = '0'
ORDER BY STEP_INDEX";

        var rows = await _db.QueryAsync<ProductionCostStepDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetBiStepStatusesAsync(string companyCd, string periodYm, string? databaseName = null)
    {
        EnsureDatabase(databaseName);
        const string sql = @"
SELECT STEP_CODE AS StepCode, STATUS AS Status
FROM acc_period_lock_step
WHERE COMPANY_CD = @CompanyCd
  AND PERIOD_YM = @PeriodYm";

        var rows = await _db.QueryAsync<BiStepStatusRow>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, PeriodYm = periodYm },
            databaseName);

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.StepCode))
            .GroupBy(x => x.StepCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Status ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> GetPreviousConfirmedSummaryAsync(string companyCd, long runId, int stepIndex, string? databaseName = null)
    {
        const string sql = @"
SELECT RESULT_BEFORE
FROM production_cost_history
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND STEP_INDEX = @StepIndex
  AND ACTION_CODE = 'REOPEN'
  AND ISDEL = '0'
ORDER BY ACTION_AT DESC, HISTORY_ID DESC
LIMIT 1";

        return await _db.QueryFirstOrDefaultAsync<string>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, StepIndex = stepIndex },
            databaseName);
    }

    public async Task<IReadOnlyList<ProductionCostObjectDto>> GetObjectsAsync(string companyCd, long runId, string? databaseName = null)
    {
        const string sql = @"
SELECT
    o.OBJECT_SEL_ID AS ObjectSelId,
    o.RUN_ID AS RunId,
    o.DEPARTMENT_ID AS DepartmentId,
    o.PRODUCT_ID AS ProductId,
    o.DEPARTMENT_CD AS DepartmentCd,
    o.PRODUCT_CD AS ProductCd,
    o.IS_SELECTED AS IsSelected,
    o.WARNING_TEXT AS WarningText
FROM production_cost_object o
WHERE o.COMPANY_CD = @CompanyCd
  AND o.RUN_ID = @RunId
  AND o.ISDEL = '0'
ORDER BY o.DEPARTMENT_ID, o.PRODUCT_ID";

        var rows = await _db.QueryAsync<ProductionCostObjectDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostAllocPoolDto>> GetAllocPoolAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    POOL_ID AS PoolId,
    RUN_ID AS RunId,
    RUN_VERSION AS RunVersion,
    ACCOUNT_CD AS AccountCd,
    CREDIT_SAMPLE AS CreditSample,
    DEPARTMENT_ID AS DepartmentId,
    DEPARTMENT_CD AS DepartmentCd,
    TOTAL_AMOUNT AS TotalAmount,
    FC_AMOUNT AS FcAmount,
    CREDIT_AMOUNT AS CreditAmount,
    DEBIT_AMOUNT_154 AS DebitAmount154,
    REDUCTION AS Reduction,
    ALLOCATION_COSTS AS AllocationCosts
FROM production_cost_alloc_pool
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY ACCOUNT_CD, DEPARTMENT_ID";

        var rows = await _db.QueryAsync<ProductionCostAllocPoolDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostNvlBasisDto>> GetNvlBasisAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    NVL_BASIS_ID AS NvlBasisId,
    RUN_ID AS RunId,
    RUN_VERSION AS RunVersion,
    PRODUCT_ID AS ProductId,
    AMOUNT_QUANTITY AS AmountQuantity,
    LINE_COUNT AS LineCount
FROM production_cost_nvl_basis
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY PRODUCT_ID";

        var rows = await _db.QueryAsync<ProductionCostNvlBasisDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostNvlBasisLineDto>> GetNvlBasisLinesAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    NVL_LINE_ID AS NvlLineId,
    RUN_ID AS RunId,
    RUN_VERSION AS RunVersion,
    PRODUCT_ID AS ProductId,
    INVENTORY_ID AS InventoryId,
    FINISHED_LINE_ID AS FinishedLineId,
    BOM_MATERIAL_ID AS BomMaterialId,
    OUTPUT_ID AS OutputId,
    CHITDETAIL_ID AS ChitdetailId,
    MATERIAL_PRODUCT_ID AS MaterialProductId,
    CONFIRMED_QUANTITY AS ConfirmedQuantity,
    SOURCE_AMOUNT AS SourceAmount,
    ALLOCATED_AMOUNT AS AllocatedAmount,
    SPLIT_RATIO AS SplitRatio
FROM production_cost_nvl_basis_line
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY PRODUCT_ID, OUTPUT_ID, NVL_LINE_ID";

        var rows = await _db.QueryAsync<ProductionCostNvlBasisLineDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostNvlOrphanDto>> GetNvlOrphansAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    ORPHAN_ID AS OrphanId,
    OUTPUT_ID AS OutputId,
    CHITDETAIL_ID AS ChitdetailId,
    AMOUNT AS Amount,
    REASON_CODE AS ReasonCode,
    WARNING_TEXT AS WarningText
FROM production_cost_nvl_orphan
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY ORPHAN_ID";

        var rows = await _db.QueryAsync<ProductionCostNvlOrphanDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostAllocDetailDto>> GetAllocDetailAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    ALLOC_DETAIL_ID AS AllocDetailId,
    RUN_ID AS RunId,
    RUN_VERSION AS RunVersion,
    PRODUCT_ID AS ProductId,
    DEPARTMENT_ID AS DepartmentId,
    PRODUCT_CD AS ProductCd,
    DEPARTMENT_CD AS DepartmentCd,
    AMOUNT_QUANTITY AS AmountQuantity,
    QUANTITY AS Quantity,
    AMOUNT_6211 AS Amount6211,
    AMOUNT_6212 AS Amount6212,
    ACCOUNT_CD AS AccountCd,
    ALLOCATED_AMOUNT AS AllocatedAmount,
    IS_LAST_ROW AS IsLastRow,
    AMOUNT_622_SUM AS Amount622Sum,
    AMOUNT_627_SUM AS Amount627Sum
FROM production_cost_alloc_detail
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY PRODUCT_ID, ACCOUNT_CD";

        var rows = await _db.QueryAsync<ProductionCostAllocDetailDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostWipDto>> GetWipAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    WIP_ID AS WipId,
    RUN_ID AS RunId,
    RUN_VERSION AS RunVersion,
    PRODUCT_ID AS ProductId,
    DEPARTMENT_ID AS DepartmentId,
    PRODUCT_CD AS ProductCd,
    DEPARTMENT_CD AS DepartmentCd,
    INCOMPLETE_QTY_OPENING AS IncompleteQtyOpening,
    BEFORE_AMOUNT_RESULT_621 AS BeforeAmountResult621,
    BEFORE_AMOUNT_RESULT_62N AS BeforeAmountResult62N,
    BEFORE_AMOUNT_154 AS BeforeAmount154,
    AMOUNT_621 AS Amount621,
    AMOUNT_6212 AS Amount6212,
    AMOUNT_622 AS Amount622,
    AMOUNT_627 AS Amount627,
    AMOUNT_154_DIRECT AS Amount154Direct,
    TOTAL_AMOUNT_INCURRED AS TotalAmountIncurred,
    QUANTITY AS Quantity,
    PERCENT_COMPLETED AS PercentCompleted,
    CLOSING_INCOMPLETE_QUANTITY AS ClosingIncompleteQuantity,
    AMOUNT_RESULT_621 AS AmountResult621,
    AMOUNT_RESULT_62N AS AmountResult62N,
    CLOSING_INCOMPLETE_AMOUNT_154 AS ClosingIncompleteAmount154,
    PRODUCT_REDUCE AS ProductReduce,
    TOTAL_COST_PRICE AS TotalCostPrice,
    UNIT_COST AS UnitCost,
    Q_HT AS QHt
FROM production_cost_wip
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY PRODUCT_ID";

        var rows = await _db.QueryAsync<ProductionCostWipDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostSourceDto>> GetSourcesAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    SOURCE_ID AS SourceId,
    PRODUCT_ID AS ProductId,
    DEPARTMENT_ID AS DepartmentId,
    SOURCE_KIND AS SourceKind,
    AMOUNT AS Amount,
    NOTE AS Note
FROM production_cost_source
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY PRODUCT_ID, SOURCE_KIND";

        var rows = await _db.QueryAsync<ProductionCostSourceDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostReceiptDto>> GetReceiptsAsync(string companyCd, long runId, int runVersion, string? databaseName = null)
    {
        const string sql = @"
SELECT
    RECEIPT_COST_ID AS ReceiptCostId,
    RUN_ID AS RunId,
    RUN_VERSION AS RunVersion,
    PRODUCT_ID AS ProductId,
    INPUT_ID AS InputId,
    INVENTORY_ID AS InventoryId,
    PRODUCT_CD AS ProductCd,
    INVENTORY_NO AS InventoryNo,
    QTY AS Qty,
    OLD_UNIT_PRICE AS OldUnitPrice,
    NEW_UNIT_PRICE AS NewUnitPrice,
    OLD_AMOUNT AS OldAmount,
    NEW_AMOUNT AS NewAmount,
    DELTA_AMOUNT AS DeltaAmount,
    IS_APPLIED AS IsApplied
FROM production_cost_receipt
WHERE COMPANY_CD = @CompanyCd
  AND RUN_ID = @RunId
  AND RUN_VERSION = @RunVersion
  AND ISDEL = '0'
ORDER BY PRODUCT_ID, INPUT_ID";

        var rows = await _db.QueryAsync<ProductionCostReceiptDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, RunId = runId, RunVersion = runVersion },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostHistoryDto>> GetHistoryAsync(string companyCd, string periodYm, long? runId, string? databaseName = null)
    {
        const string sql = @"
SELECT
    HISTORY_ID AS HistoryId,
    RUN_ID AS RunId,
    PERIOD_YM AS PeriodYm,
    STEP_INDEX AS StepIndex,
    ACTION_CODE AS ActionCode,
    STATUS_AFTER AS StatusAfter,
    RUN_VERSION AS RunVersion,
    RESULT_BEFORE AS ResultBefore,
    RESULT_AFTER AS ResultAfter,
    DIFFERENCE_TEXT AS DifferenceText,
    REASON AS Reason,
    USER_NAME AS UserName,
    ACTION_AT AS ActionAt
FROM production_cost_history
WHERE COMPANY_CD = @CompanyCd
  AND PERIOD_YM = @PeriodYm
  AND ISDEL = '0'
  AND (@RunId IS NULL OR RUN_ID = @RunId)
ORDER BY ACTION_AT DESC, HISTORY_ID DESC";

        var rows = await _db.QueryAsync<ProductionCostHistoryDto>(
            Net_DB.Net_DB_Company,
            sql,
            new { CompanyCd = companyCd, PeriodYm = periodYm, RunId = runId },
            databaseName);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ProductionCostComparisonLineDto>> GetComparisonLinesAsync(
        string companyCd,
        long baseRunId,
        int baseRunVersion,
        long compareRunId,
        int compareRunVersion,
        string? databaseName = null)
    {
        const string sql = @"
SELECT
    COALESCE(b.PRODUCT_ID, c.PRODUCT_ID) AS ProductId,
    COALESCE(b.PRODUCT_CD, c.PRODUCT_CD) AS ProductCd,
    COALESCE(b.DEPARTMENT_ID, c.DEPARTMENT_ID) AS DepartmentId,
    IFNULL(b.TOTAL_COST_PRICE, 0) AS BaseTotalCostPrice,
    IFNULL(c.TOTAL_COST_PRICE, 0) AS CompareTotalCostPrice,
    IFNULL(c.TOTAL_COST_PRICE, 0) - IFNULL(b.TOTAL_COST_PRICE, 0) AS DeltaTotalCostPrice,
    IFNULL(b.UNIT_COST, 0) AS BaseUnitCost,
    IFNULL(c.UNIT_COST, 0) AS CompareUnitCost,
    IFNULL(c.UNIT_COST, 0) - IFNULL(b.UNIT_COST, 0) AS DeltaUnitCost,
    IFNULL(b.AMOUNT_622, 0) AS BaseAmount622,
    IFNULL(c.AMOUNT_622, 0) AS CompareAmount622,
    IFNULL(c.AMOUNT_622, 0) - IFNULL(b.AMOUNT_622, 0) AS DeltaAmount622,
    IFNULL(b.AMOUNT_627, 0) AS BaseAmount627,
    IFNULL(c.AMOUNT_627, 0) AS CompareAmount627,
    IFNULL(c.AMOUNT_627, 0) - IFNULL(b.AMOUNT_627, 0) AS DeltaAmount627
FROM (
    SELECT *
    FROM production_cost_wip
    WHERE COMPANY_CD = @CompanyCd
      AND RUN_ID = @BaseRunId
      AND RUN_VERSION = @BaseRunVersion
      AND ISDEL = '0'
) b
LEFT JOIN (
    SELECT *
    FROM production_cost_wip
    WHERE COMPANY_CD = @CompanyCd
      AND RUN_ID = @CompareRunId
      AND RUN_VERSION = @CompareRunVersion
      AND ISDEL = '0'
) c ON c.PRODUCT_ID = b.PRODUCT_ID AND c.COMPANY_CD = b.COMPANY_CD
UNION
SELECT
    COALESCE(b.PRODUCT_ID, c.PRODUCT_ID) AS ProductId,
    COALESCE(b.PRODUCT_CD, c.PRODUCT_CD) AS ProductCd,
    COALESCE(b.DEPARTMENT_ID, c.DEPARTMENT_ID) AS DepartmentId,
    IFNULL(b.TOTAL_COST_PRICE, 0) AS BaseTotalCostPrice,
    IFNULL(c.TOTAL_COST_PRICE, 0) AS CompareTotalCostPrice,
    IFNULL(c.TOTAL_COST_PRICE, 0) - IFNULL(b.TOTAL_COST_PRICE, 0) AS DeltaTotalCostPrice,
    IFNULL(b.UNIT_COST, 0) AS BaseUnitCost,
    IFNULL(c.UNIT_COST, 0) AS CompareUnitCost,
    IFNULL(c.UNIT_COST, 0) - IFNULL(b.UNIT_COST, 0) AS DeltaUnitCost,
    IFNULL(b.AMOUNT_622, 0) AS BaseAmount622,
    IFNULL(c.AMOUNT_622, 0) AS CompareAmount622,
    IFNULL(c.AMOUNT_622, 0) - IFNULL(b.AMOUNT_622, 0) AS DeltaAmount622,
    IFNULL(b.AMOUNT_627, 0) AS BaseAmount627,
    IFNULL(c.AMOUNT_627, 0) AS CompareAmount627,
    IFNULL(c.AMOUNT_627, 0) - IFNULL(b.AMOUNT_627, 0) AS DeltaAmount627
FROM (
    SELECT *
    FROM production_cost_wip
    WHERE COMPANY_CD = @CompanyCd
      AND RUN_ID = @CompareRunId
      AND RUN_VERSION = @CompareRunVersion
      AND ISDEL = '0'
) c
LEFT JOIN (
    SELECT *
    FROM production_cost_wip
    WHERE COMPANY_CD = @CompanyCd
      AND RUN_ID = @BaseRunId
      AND RUN_VERSION = @BaseRunVersion
      AND ISDEL = '0'
) b ON b.PRODUCT_ID = c.PRODUCT_ID AND b.COMPANY_CD = c.COMPANY_CD
WHERE b.PRODUCT_ID IS NULL
ORDER BY ProductId";

        var rows = await _db.QueryAsync<ProductionCostComparisonLineDto>(
            Net_DB.Net_DB_Company,
            sql,
            new
            {
                CompanyCd = companyCd,
                BaseRunId = baseRunId,
                BaseRunVersion = baseRunVersion,
                CompareRunId = compareRunId,
                CompareRunVersion = compareRunVersion
            },
            databaseName);
        return rows.ToList();
    }

    private sealed class BiStepStatusRow
    {
        public string? StepCode { get; set; }
        public string? Status { get; set; }
    }
}

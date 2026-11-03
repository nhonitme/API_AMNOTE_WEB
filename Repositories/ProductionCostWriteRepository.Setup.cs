using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using MySqlConnector;

namespace API_AMNOTE_WEB.Repositories;

public sealed partial class ProductionCostWriteRepository
{
    public async Task<ProductionCostEnsureResult> EnsureDraftRunAsync(
        DapperSession session,
        string companyCd,
        string periodYm,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        var company = companyCd.Trim();
        var ym = periodYm.Trim();
        var from = new DateTime(int.Parse(ym[..4]), int.Parse(ym.Substring(4, 2)), 1);
        var to = from.AddMonths(1).AddDays(-1);
        var user = userId ?? string.Empty;

        try
        {
            var existing = await session.QueryFirstOrDefaultAsync<EnsureRow>(@"
SELECT PERIOD_COST_ID AS PeriodCostId, CURRENT_RUN_ID AS CurrentRunId
FROM production_cost_period
WHERE COMPANY_CD=@Company AND PERIOD_YM=@Ym AND ISDEL='0'
LIMIT 1
FOR UPDATE", new { Company = company, Ym = ym });

            if (existing?.CurrentRunId is > 0)
            {
                var ver = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT RUN_VERSION FROM production_cost_run
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0' LIMIT 1",
                    new { Company = company, RunId = existing.CurrentRunId });
                return new ProductionCostEnsureResult
                {
                    Created = false,
                    PeriodCostId = existing.PeriodCostId,
                    RunId = existing.CurrentRunId,
                    RunVersion = ver,
                    PeriodYm = ym,
                    Message = "Kỳ đã có RUN hiện hành."
                };
            }

            long periodId;
            if (existing == null)
            {
                await session.ExecuteAsync(@"
INSERT INTO production_cost_period (
  COMPANY_CD, PERIOD_YM, FROM_YMD, TO_YMD, STATUS, COST_ENGINE, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
  @Company, @Ym, @FromYmd, @ToYmd, 'OPEN', 'NONE', '0', @User, UTC_TIMESTAMP(), @User, UTC_TIMESTAMP()
)", new { Company = company, Ym = ym, FromYmd = from.ToString("yyyyMMdd"), ToYmd = to.ToString("yyyyMMdd"), User = user });
                periodId = await session.QueryFirstOrDefaultAsync<long>("SELECT LAST_INSERT_ID()");
            }
            else
            {
                periodId = existing.PeriodCostId;
            }

            await session.ExecuteAsync(@"
INSERT INTO production_cost_run (
  PERIOD_COST_ID, COMPANY_CD, PERIOD_YM, RUN_VERSION, STATUS, WIP_METHOD, ALLOCATE_TYPE,
  IS_622_627_BY_DEPARTMENT, IS_6212, ALLOC_NUMBER_DIGIT, INCOMPLETE_MODE, INCOMPLETE_PERCENT,
  ENABLE_PRODUCT_REDUCE, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
  @PeriodId, @Company, @Ym, 1, 'DRAFT', 'PERCENT_COMPLETED', 0,
  '0', '0', 0, 'STANDARD', 0.8000, '0', '0', @User, UTC_TIMESTAMP(), @User, UTC_TIMESTAMP()
)", new { PeriodId = periodId, Company = company, Ym = ym, User = user });
            var runId = await session.QueryFirstOrDefaultAsync<long>("SELECT LAST_INSERT_ID()");

            var linked = await session.ExecuteAsync(@"
UPDATE production_cost_period
SET CURRENT_RUN_ID=@RunId, UPDATE_BY=@User, UPDATE_AT=UTC_TIMESTAMP()
WHERE PERIOD_COST_ID=@PeriodId AND COMPANY_CD=@Company AND ISDEL='0' AND CURRENT_RUN_ID IS NULL",
                new { RunId = runId, User = user, PeriodId = periodId, Company = company });
            if (linked != 1)
                throw new InvalidOperationException("Không gắn được CURRENT_RUN_ID. Không tạo RUN thứ hai.");

            var steps = new (int Index, string Code, string Engine)[]
            {
                (1, ProductionCostStepCode.FaPrepaidLock, "BI"),
                (2, ProductionCostStepCode.CostAllocation, "GK"),
                (3, ProductionCostStepCode.ExpTo154Only, "GK"),
                (4, ProductionCostStepCode.CostCalc, "GK"),
                (5, ProductionCostStepCode.Valuation, "GK"),
                (6, ProductionCostStepCode.ProfitLossReport, "BI")
            };
            foreach (var step in steps)
            {
                await session.ExecuteAsync(@"
INSERT INTO production_cost_step (
  RUN_ID, COMPANY_CD, STEP_INDEX, STEP_CODE, ENGINE, STATUS, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
  @RunId, @Company, @Index, @Code, @Engine, 'NOT_STARTED', '0', @User, UTC_TIMESTAMP(), @User, UTC_TIMESTAMP()
)", new { RunId = runId, Company = company, step.Index, step.Code, step.Engine, User = user });
            }

            return new ProductionCostEnsureResult
            {
                Created = true,
                PeriodCostId = periodId,
                RunId = runId,
                RunVersion = 1,
                PeriodYm = ym,
                Message = "Đã khởi tạo kỳ DRAFT. Chưa ghi chứng từ."
            };
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"Khởi tạo kỳ G-K: {ex.Message}", ex);
        }
    }

    public async Task UpdateRunConfigAsync(
        DapperSession session,
        string companyCd,
        ProductionCostRunConfigUpdate update,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(update);
        var locked = await LockRunForUpdateAsync(session, companyCd, update.RunId)
            ?? throw new InvalidOperationException("RUN không tồn tại.");
        if (locked.RunVersion != update.RunVersion)
            throw new InvalidOperationException("RUN_VERSION không khớp header.");
        if (!GkRunStatus.AllowsPoolRecalc(locked.Status))
            throw new InvalidOperationException("RUN đã khóa phân bổ hoặc đã CONFIRMED. REOPEN trước khi sửa cấu hình.");

        var method = update.ValuationMethod == null
            ? null
            : InventoryValuationMethod.Normalize(update.ValuationMethod);
        var options = method == null ? null : "{\"valuationMethod\":\"" + method + "\"}";

        var affected = await session.ExecuteAsync(@"
UPDATE production_cost_run
SET ALLOCATE_TYPE = COALESCE(@AllocateType, ALLOCATE_TYPE),
    IS_622_627_BY_DEPARTMENT = COALESCE(@ByDept, IS_622_627_BY_DEPARTMENT),
    IS_6212 = COALESCE(@Is6212, IS_6212),
    ALLOC_NUMBER_DIGIT = COALESCE(@Digit, ALLOC_NUMBER_DIGIT),
    WIP_METHOD = COALESCE(@WipMethod, WIP_METHOD),
    INCOMPLETE_MODE = COALESCE(@IncompleteMode, INCOMPLETE_MODE),
    INCOMPLETE_PERCENT = COALESCE(@IncompletePercent, INCOMPLETE_PERCENT),
    ENABLE_PRODUCT_REDUCE = COALESCE(@Reduce, ENABLE_PRODUCT_REDUCE),
    OPTIONS_JSON = COALESCE(@Options, OPTIONS_JSON),
    UPDATE_BY = @UserId,
    UPDATE_AT = UTC_TIMESTAMP()
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'",
            new
            {
                AllocateType = update.AllocateType,
                ByDept = update.ByDepartment.HasValue ? (update.ByDepartment.Value ? "1" : "0") : null,
                Is6212 = update.Include6212.HasValue ? (update.Include6212.Value ? "1" : "0") : null,
                Digit = update.NumberDigit,
                update.WipMethod,
                update.IncompleteMode,
                update.IncompletePercent,
                Reduce = update.EnableProductReduce.HasValue ? (update.EnableProductReduce.Value ? "1" : "0") : null,
                Options = options,
                UserId = userId ?? string.Empty,
                Company = companyCd.Trim(),
                update.RunId,
                update.RunVersion
            });
        if (affected != 1)
            throw new InvalidOperationException("Không cập nhật được cấu hình RUN.");
    }

    public async Task UpdateObjectSelectionAsync(
        DapperSession session,
        string companyCd,
        ProductionCostObjectSelectionUpdate update,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(update);
        var affected = await session.ExecuteAsync(@"
UPDATE production_cost_object
SET IS_SELECTED=@Selected, UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0'
  AND DEPARTMENT_ID=@DepartmentId
  AND (@ProductId = 0 OR PRODUCT_ID=@ProductId)",
            new
            {
                Selected = update.Selected ? "1" : "0",
                UserId = userId ?? string.Empty,
                Company = companyCd.Trim(),
                update.RunId,
                update.DepartmentId,
                update.ProductId
            });
        if (affected == 0)
            throw new InvalidOperationException("Không có đối tượng khớp DEPARTMENT_ID/PRODUCT_ID để cập nhật lựa chọn.");
    }

    public async Task<int> SyncObjectsFromReceiptsAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        string? userId)
    {
        _ = runVersion;
        var locked = await LockRunForUpdateAsync(session, companyCd, runId)
            ?? throw new InvalidOperationException("RUN không tồn tại.");
        var bounds = await session.QueryFirstOrDefaultAsync<BoundRow>(@"
SELECT FROM_YMD AS FromYmd, TO_YMD AS ToYmd
FROM production_cost_period
WHERE COMPANY_CD=@Company AND PERIOD_YM=@Ym AND ISDEL='0' LIMIT 1",
            new { Company = companyCd.Trim(), Ym = locked.PeriodYm.Trim() });
        if (bounds == null)
            throw new InvalidOperationException("Thiếu cận ngày của kỳ.");

        var inserted = await session.ExecuteAsync(@"
INSERT INTO production_cost_object (
  RUN_ID, COMPANY_CD, DEPARTMENT_ID, PRODUCT_ID, DEPARTMENT_CD, PRODUCT_CD, IS_SELECTED, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
)
SELECT @RunId, i.COMPANY_CD, IFNULL(ext.DEPARTMENT_ID, 0), i.PRODUCT_ID,
       MAX(ext.DEPARTMENT_CD), MAX(i.PRODUCT_CD), '1', '0', @User, UTC_TIMESTAMP(), @User, UTC_TIMESTAMP()
FROM chit_inventory_input i
INNER JOIN chitdetailinfo d
  ON d.COMPANY_CD=i.COMPANY_CD AND d.CHITDETAIL_ID=i.CHITDETAIL_ID AND d.ISDEL='0'
INNER JOIN chitinfo c
  ON c.COMPANY_CD=d.COMPANY_CD AND c.CHIT_ID=d.CHIT_ID AND c.ISDEL='0'
LEFT JOIN chitdetailinfo_ext ext
  ON ext.COMPANY_CD=d.COMPANY_CD AND ext.CHITDETAIL_ID=d.CHITDETAIL_ID AND ext.ISDEL='0'
WHERE i.COMPANY_CD=@Company AND i.ISDEL='0' AND i.PRODUCT_ID > 0
  AND c.CHIT_YMD >= @FromYmd AND c.CHIT_YMD <= @ToYmd
  AND IFNULL(c.INPUT_TYPE,'') <> 'LOCK'
  AND TRIM(d.DEBIT) LIKE '155%' AND TRIM(d.CREDIT) LIKE '154%'
GROUP BY i.COMPANY_CD, IFNULL(ext.DEPARTMENT_ID, 0), i.PRODUCT_ID
ON DUPLICATE KEY UPDATE
  IS_SELECTED='1', ISDEL='0', PRODUCT_CD=VALUES(PRODUCT_CD), DEPARTMENT_CD=VALUES(DEPARTMENT_CD),
  UPDATE_BY=VALUES(UPDATE_BY), UPDATE_AT=UTC_TIMESTAMP()",
            new
            {
                RunId = runId,
                User = userId ?? string.Empty,
                Company = companyCd.Trim(),
                bounds.FromYmd,
                bounds.ToYmd
            });
        if (inserted == 0)
        {
            throw new InvalidOperationException(
                "Không có phiếu nhập Nợ 155 / Có 154 trong kỳ để tạo đối tượng. Không tạo dữ liệu mẫu.");
        }

        return inserted;
    }

    public async Task UpdatePoolReductionAsync(
        DapperSession session,
        string companyCd,
        ProductionCostPoolReductionUpdate update,
        string? userId)
    {
        var affected = await session.ExecuteAsync(@"
UPDATE production_cost_alloc_pool
SET REDUCTION=@Reduction,
    ALLOCATION_COSTS=ROUND(TOTAL_AMOUNT - @Reduction, 2),
    UPDATE_BY=@UserId,
    UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'
  AND ACCOUNT_CD=@AccountCd AND DEPARTMENT_ID=@DepartmentId",
            new
            {
                update.Reduction,
                UserId = userId ?? string.Empty,
                Company = companyCd.Trim(),
                update.RunId,
                update.RunVersion,
                update.AccountCd,
                update.DepartmentId
            });
        if (affected != 1)
        {
            throw new InvalidOperationException(
                "Không tìm thấy dòng pool để sửa điều chỉnh giảm. Hãy xem trước phân bổ để nạp pool từ sổ cái trước.");
        }
    }

    public async Task SaveWipAdjustmentAsync(
        DapperSession session,
        string companyCd,
        ProductionCostWipAdjustmentUpdate update,
        string? userId)
    {
        if (update.ProductId <= 0)
            throw new ArgumentException("PRODUCT_ID phải > 0.");
        var affected = await session.ExecuteAsync(@"
INSERT INTO production_cost_wip (
  RUN_ID, RUN_VERSION, COMPANY_CD, PRODUCT_ID, DEPARTMENT_ID,
  PERCENT_COMPLETED, CLOSING_INCOMPLETE_QUANTITY, PRODUCT_REDUCE, INCOMPLETE_QTY_OPENING,
  ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
  @RunId, @RunVersion, @Company, @ProductId, @DepartmentId,
  COALESCE(@Percent, 100), COALESCE(@ClosingQty, 0), COALESCE(@Reduce, 0), COALESCE(@OpeningQty, 0),
  '0', @User, UTC_TIMESTAMP(), @User, UTC_TIMESTAMP()
)
ON DUPLICATE KEY UPDATE
  PERCENT_COMPLETED = COALESCE(@Percent, PERCENT_COMPLETED),
  CLOSING_INCOMPLETE_QUANTITY = COALESCE(@ClosingQty, CLOSING_INCOMPLETE_QUANTITY),
  PRODUCT_REDUCE = COALESCE(@Reduce, PRODUCT_REDUCE),
  INCOMPLETE_QTY_OPENING = COALESCE(@OpeningQty, INCOMPLETE_QTY_OPENING),
  ISDEL='0',
  UPDATE_BY=@User,
  UPDATE_AT=UTC_TIMESTAMP()",
            new
            {
                update.RunId,
                update.RunVersion,
                Company = companyCd.Trim(),
                update.ProductId,
                update.DepartmentId,
                Percent = update.PercentCompleted,
                ClosingQty = update.ClosingIncompleteQty,
                Reduce = update.ProductReduce,
                OpeningQty = update.IncompleteQtyOpening,
                User = userId ?? string.Empty
            });
        if (affected == 0)
            throw new InvalidOperationException("Không lưu được điều chỉnh dở dang.");
    }

    private sealed class EnsureRow
    {
        public long PeriodCostId { get; set; }
        public long? CurrentRunId { get; set; }
    }

    private sealed class BoundRow
    {
        public string FromYmd { get; set; } = string.Empty;
        public string ToYmd { get; set; } = string.Empty;
    }
}

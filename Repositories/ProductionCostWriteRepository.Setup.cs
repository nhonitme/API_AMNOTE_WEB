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
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);

        var locked = await LockRunForUpdateAsync(session, companyCd, runId)
            ?? throw new InvalidOperationException("RUN không tồn tại.");
        if (runVersion <= 0 || locked.RunVersion != runVersion)
            throw new InvalidOperationException("RUN_VERSION đã thay đổi. Tải lại kỳ G-K rồi đồng bộ.");

        // Đồng bộ ở bước chuẩn bị là REPLACE ALL, không phải UPSERT.
        // Không xóa/ghi đè đối tượng của RUN đã vào các bước tính/khóa.
        if (!string.Equals(locked.Status, GkRunStatus.Draft, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(locked.Status, GkRunStatus.Previewed, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Chỉ khởi tạo lại đối tượng khi RUN còn DRAFT/PREVIEWED, trước khi xác nhận giá thành.");

        var statuses = await GetStepStatusesForUpdateAsync(session, companyCd, runId);
        if (statuses.Any(x =>
            !string.Equals(x.Value, "NOT_STARTED", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x.Value, "OPEN", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "RUN đã có bước xử lý hoặc khóa. Không được xóa lại danh sách đối tượng; kiểm tra/mở khóa trước.");

        // Không làm mất dữ liệu đã tính, kể cả khi trạng thái RUN chưa cập nhật kịp.
        var hasDerivedRows = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT (
    EXISTS(SELECT 1 FROM production_cost_alloc_pool
      WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0')
    OR EXISTS(SELECT 1 FROM production_cost_alloc_detail
      WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0')
    OR EXISTS(SELECT 1 FROM production_cost_source
      WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0')
    OR EXISTS(SELECT 1 FROM production_cost_wip
      WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0')
    OR EXISTS(SELECT 1 FROM production_cost_receipt
      WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0')
    OR EXISTS(SELECT 1 FROM production_cost_nvl_basis
      WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0')
    OR EXISTS(SELECT 1 FROM production_cost_voucher_ref
      WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0')
) AS HasDerivedRows",
            new { Company = companyCd.Trim(), RunId = runId });
        if (hasDerivedRows != 0)
            throw new InvalidOperationException(
                "RUN đã có dữ liệu tính giá thành. Không được khởi tạo lại danh sách đối tượng.");

        var bounds = await session.QueryFirstOrDefaultAsync<BoundRow>(@"
SELECT FROM_YMD AS FromYmd, TO_YMD AS ToYmd
FROM production_cost_period
WHERE COMPANY_CD=@Company AND PERIOD_YM=@Ym AND ISDEL='0' LIMIT 1",
            new { Company = companyCd.Trim(), Ym = locked.PeriodYm.Trim() });
        if (bounds == null)
            throw new InvalidOperationException("Thiếu cận ngày của kỳ.");

        // N155/C154 from the source receipts. Resolve canonical department using
        // product_info.DEPARTMENT_ID when the accounting extension has the same
        // logical department code. A different department code remains a distinct
        // cost object. The old production_cost_object rows are NOT inputs here.
        const string sourceSql = @"
SELECT i.PRODUCT_ID AS ProductId,
       MAX(i.PRODUCT_CD) AS ProductCd,
       IFNULL(ext.DEPARTMENT_ID, 0) AS DepartmentId,
       MAX(ext.DEPARTMENT_CD) AS DepartmentCd,
       MAX(IFNULL(p.DEPARTMENT_ID, 0)) AS ProductDepartmentId,
       MAX(pd.DEPARTMENT_CD) AS ProductDepartmentCd
FROM chit_inventory_input i
INNER JOIN chitdetailinfo d
  ON d.COMPANY_CD=i.COMPANY_CD AND d.CHITDETAIL_ID=i.CHITDETAIL_ID AND d.ISDEL='0'
INNER JOIN chitinfo c
  ON c.COMPANY_CD=d.COMPANY_CD AND c.CHIT_ID=d.CHIT_ID AND c.ISDEL='0'
LEFT JOIN chitdetailinfo_ext ext
  ON ext.COMPANY_CD=d.COMPANY_CD AND ext.CHITDETAIL_ID=d.CHITDETAIL_ID AND ext.ISDEL='0'
LEFT JOIN product_info p
  ON p.COMPANY_CD=i.COMPANY_CD AND p.PRODUCT_ID=i.PRODUCT_ID AND IFNULL(p.ISDEL,'0')='0'
LEFT JOIN department_info pd
  ON pd.COMPANY_CD=p.COMPANY_CD AND pd.DEPARTMENT_ID=p.DEPARTMENT_ID AND IFNULL(pd.ISDEL,'0')='0'
WHERE i.COMPANY_CD=@Company AND i.ISDEL='0' AND i.PRODUCT_ID>0
  AND c.CHIT_YMD BETWEEN @FromYmd AND @ToYmd
  AND IFNULL(c.INPUT_TYPE,'') <> 'LOCK'
  AND TRIM(d.DEBIT) LIKE '155%' AND TRIM(d.CREDIT) LIKE '154%'
GROUP BY i.PRODUCT_ID, IFNULL(ext.DEPARTMENT_ID, 0)";
        var company = companyCd.Trim();
        var sources = (await session.QueryAsync<ObjectSyncSourceRow>(
            sourceSql, new { Company = company, bounds.FromYmd, bounds.ToYmd })).ToList();

        // Validate the new snapshot completely BEFORE deleting the old one.
        if (sources.Count == 0)
            throw new InvalidOperationException(
                "Không có phiếu nhập Nợ 155 / Có 154 trong kỳ. Giữ nguyên danh sách đối tượng cũ.");

        var resolved = new List<ObjectSyncResolvedRow>();
        foreach (var source in sources)
        {
            var sourceCode = (source.DepartmentCd ?? "").Trim();
            var masterCode = (source.ProductDepartmentCd ?? "").Trim();
            var sameLogicalCode = sourceCode.Length > 0 && masterCode.Length > 0
                && string.Equals(sourceCode, masterCode, StringComparison.OrdinalIgnoreCase);
            var useProductDepartment = source.ProductDepartmentId > 0
                && (source.DepartmentId <= 0 || sourceCode.Length == 0 || sameLogicalCode);

            var departmentId = useProductDepartment
                ? source.ProductDepartmentId
                : source.DepartmentId;
            var departmentCd = useProductDepartment ? masterCode : sourceCode;
            if (departmentCd.Length == 0 && masterCode.Length > 0)
                departmentCd = masterCode;

            if (departmentId <= 0 || departmentCd.Length == 0)
                throw new InvalidOperationException(
                    $"Thành phẩm {source.ProductCd} (PRODUCT_ID={source.ProductId}) " +
                    "chưa liên kết được đối tượng hợp lệ. Không xóa dữ liệu cũ.");

            resolved.Add(new ObjectSyncResolvedRow(
                source.ProductId, departmentId, departmentCd, source.ProductCd ?? ""));
        }

        var newObjects = resolved
            .GroupBy(x => (x.ProductId, x.DepartmentId))
            .Select(g => g.First())
            .ToList();

        // Same PRODUCT + same logical code cannot be two separate cost objects.
        // When product master cannot resolve the ID ambiguity, fail explicitly,
        // rather than creating an extra quantity-zero line.
        var ambiguous = newObjects
            .GroupBy(x => (x.ProductId, Code: x.DepartmentCd.ToUpperInvariant()))
            .FirstOrDefault(g => g.Select(x => x.DepartmentId).Distinct().Count() > 1);
        if (ambiguous != null)
            throw new InvalidOperationException(
                $"Mã đối tượng {ambiguous.Key.Code} của PRODUCT_ID={ambiguous.Key.ProductId} " +
                "đang liên kết nhiều DEPARTMENT_ID khác nhau. Cần chuẩn hóa danh mục trước khi đồng bộ.");

        // This operation runs inside ProductionCostWriteSession.ExecuteAsync:
        // DELETE and INSERT belong to the SAME transaction (rollback on error).
        // Limit deletion strictly to the current company + RUN, including old
        // soft-deleted rows that would collide with its unique key.
        await session.ExecuteAsync(@"
DELETE FROM production_cost_object
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId",
            new { Company = company, RunId = runId });

        const string insertSql = @"
INSERT INTO production_cost_object (
  RUN_ID, COMPANY_CD, DEPARTMENT_ID, PRODUCT_ID, DEPARTMENT_CD, PRODUCT_CD,
  IS_SELECTED, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
  @RunId, @Company, @DepartmentId, @ProductId, @DepartmentCd, @ProductCd,
  '1', '0', @User, UTC_TIMESTAMP(), @User, UTC_TIMESTAMP()
)";
        foreach (var row in newObjects)
        {
            await session.ExecuteAsync(insertSql, new
            {
                RunId = runId,
                Company = company,
                row.DepartmentId,
                row.ProductId,
                row.DepartmentCd,
                row.ProductCd,
                User = userId ?? string.Empty
            });
        }

        return newObjects.Count;
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

    private sealed class ObjectSyncSourceRow
    {
        public long ProductId { get; set; }
        public string? ProductCd { get; set; }
        public long DepartmentId { get; set; }
        public string? DepartmentCd { get; set; }
        public long ProductDepartmentId { get; set; }
        public string? ProductDepartmentCd { get; set; }
    }

    private sealed record ObjectSyncResolvedRow(
        long ProductId, long DepartmentId, string DepartmentCd, string ProductCd);

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

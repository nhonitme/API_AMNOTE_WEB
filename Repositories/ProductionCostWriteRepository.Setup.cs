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
            await AssertMonthNotLockedForEnsureAsync(session, company, ym);

            var existing = await session.QueryFirstOrDefaultAsync<EnsureRow>(@"
SELECT PERIOD_COST_ID AS PeriodCostId, CURRENT_RUN_ID AS CurrentRunId
FROM production_cost_period
WHERE COMPANY_CD=@Company AND PERIOD_YM=@Ym AND ISDEL='0'
LIMIT 1
FOR UPDATE", new { Company = company, Ym = ym });

            var resetExisting = false;
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
                if (existing.CurrentRunId is > 0)
                {
                    // Cùng TX: chặn trước mọi DELETE nếu RUN đã có tác động KT/kho không hoàn nguyên an toàn.
                    await AssertCurrentRunSafeToResetAsync(session, company, ym, existing.CurrentRunId.Value);
                    await DeleteGkWorkingDataForRunAsync(session, company, existing.CurrentRunId.Value);
                    await session.ExecuteAsync(@"
UPDATE production_cost_period
SET CURRENT_RUN_ID=NULL,
    STATUS='OPEN',
    COST_ENGINE='NONE',
    COGS_RULE=NULL,
    UPDATE_BY=@User,
    UPDATE_AT=UTC_TIMESTAMP()
WHERE PERIOD_COST_ID=@PeriodId AND COMPANY_CD=@Company AND ISDEL='0'",
                        new { User = user, PeriodId = periodId, Company = company });
                    resetExisting = true;
                }
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
SET CURRENT_RUN_ID=@RunId, STATUS='OPEN', COST_ENGINE='NONE', COGS_RULE=NULL,
    UPDATE_BY=@User, UPDATE_AT=UTC_TIMESTAMP()
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

            // Cùng TX với reset/create: tái sử dụng SyncObjectsFromReceipts (DELETE ALL + INSERT).
            // Không có phiếu nhập hợp lệ → danh sách đối tượng trống, kỳ vẫn khởi tạo thành công.
            var syncedCount = await SyncObjectsFromReceiptsAsync(session, company, runId, 1, user);

            string syncPart;
            if (syncedCount <= 0)
            {
                syncPart = "Chưa có phiếu nhập thành phẩm Nợ 155/Có 154 trong kỳ — danh sách đối tượng trống.";
            }
            else
            {
                syncPart = $"Đã đồng bộ {syncedCount} đối tượng/thành phẩm từ phiếu nhập kho.";
            }

            var basePart = resetExisting
                ? "Đã khởi tạo lại kỳ DRAFT (0/6). Đã xóa dữ liệu G-K cũ; không đụng chứng từ kế toán / phiếu kho nguồn."
                : "Đã khởi tạo kỳ DRAFT (0/6). Chưa ghi chứng từ.";

            return new ProductionCostEnsureResult
            {
                Created = true,
                PeriodCostId = periodId,
                RunId = runId,
                RunVersion = 1,
                PeriodYm = ym,
                SyncedObjectCount = syncedCount,
                Message = $"{basePart} {syncPart}"
            };
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"Khởi tạo kỳ G-K: {ex.Message}", ex);
        }
    }

    private static async Task AssertMonthNotLockedForEnsureAsync(
        DapperSession session, string company, string ym)
    {
        var status = await session.QueryFirstOrDefaultAsync<string>(@"
SELECT STATUS FROM acc_period_lock_month
WHERE COMPANY_CD=@Company AND PERIOD_YM=@Ym AND ISDEL='0' LIMIT 1",
            new { Company = company, Ym = ym });
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "PROCESSING", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Kỳ {ym} đang {status}. Không khởi tạo lại G-K khi kỳ đã khóa.");
        }
    }

    private static async Task AssertCurrentRunSafeToResetAsync(
        DapperSession session, string company, string ym, long runId)
    {
        var locked = await session.QueryFirstOrDefaultAsync<EnsureRunSafetyRow>(@"
SELECT RUN_ID AS RunId, STATUS AS Status, RUN_VERSION AS RunVersion
FROM production_cost_run
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0'
LIMIT 1
FOR UPDATE", new { Company = company, RunId = runId });
        if (locked == null)
            throw new InvalidOperationException("RUN hiện hành của kỳ không tồn tại hoặc đã xóa.");

        if (string.Equals(locked.Status, GkRunStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "RUN đã CONFIRMED toàn kỳ. Không khởi tạo lại — mở lại (REOPEN) các bước có tác động trước.");
        }

        var unsafeFlags = await session.QueryFirstOrDefaultAsync<EnsureUnsafeFlags>(@"
SELECT
  EXISTS(
    SELECT 1 FROM production_cost_voucher_ref
    WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0'
      AND CHIT_ID IS NOT NULL AND CHIT_ID > 0
  ) AS HasPostedVoucher,
  EXISTS(
    SELECT 1 FROM production_cost_receipt
    WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0' AND IS_APPLIED='1'
  ) AS HasAppliedInventory,
  EXISTS(
    SELECT 1 FROM acc_period_lock_step
    WHERE COMPANY_CD=@Company AND PERIOD_YM=@Ym
      AND STEP_CODE IN ('FA_PREPAID_LOCK', 'COGS_SUMMARY', 'PROFIT_LOSS_REPORT')
      AND UPPER(IFNULL(STATUS,'')) = 'DONE'
  ) AS HasBiStepDone
", new { Company = company, RunId = runId, Ym = ym });

        if ((unsafeFlags?.HasPostedVoucher ?? 0) != 0)
        {
            throw new InvalidOperationException(
                "RUN đã sinh chứng từ kế toán (voucher_ref). Không xóa một phần — hãy REOPEN/hoàn nguyên trước khi khởi tạo lại kỳ.");
        }

        if ((unsafeFlags?.HasAppliedInventory ?? 0) != 0)
        {
            throw new InvalidOperationException(
                "RUN đã cập nhật giá kho (IS_APPLIED=1). Không khởi tạo lại khi chưa hoàn nguyên an toàn.");
        }

        if ((unsafeFlags?.HasBiStepDone ?? 0) != 0)
        {
            throw new InvalidOperationException(
                "Kỳ đã có bước B-I khóa (FA_PREPAID_LOCK / COGS_SUMMARY / PROFIT_LOSS) = DONE. Không khởi tạo lại G-K.");
        }

        var priceAuditTable = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT COUNT(*) FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_price_audit'");
        if (priceAuditTable > 0)
        {
            var openAudit = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT EXISTS(
  SELECT 1 FROM production_cost_price_audit
  WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0' AND IFNULL(IS_REVERSED,'0')='0'
)", new { Company = company, RunId = runId });
            if (openAudit != 0)
            {
                throw new InvalidOperationException(
                    "RUN còn snapshot giá kho chưa hoàn (price_audit). Không khởi tạo lại khi chưa hoàn nguyên an toàn.");
            }
        }
    }

    /// <summary>
    /// Xóa dữ liệu làm việc G-K của RUN trong TX hiện tại.
    /// Không đụng chitinfo / phiếu nhập-xuất kho nguồn.
    /// Chỉ gọi sau <see cref="AssertCurrentRunSafeToResetAsync"/>.
    /// </summary>
    private static async Task DeleteGkWorkingDataForRunAsync(
        DapperSession session, string company, long runId)
    {
        var args = new { Company = company, RunId = runId };

        await session.ExecuteAsync(
            "DELETE FROM production_cost_nvl_basis_line WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_nvl_basis WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_nvl_orphan WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_alloc_detail WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_alloc_pool WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_source WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_wip WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_receipt WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_voucher_ref WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_object WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_history WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        await session.ExecuteAsync(
            "DELETE FROM production_cost_step WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);

        var priceAuditTable = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT COUNT(*) FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_price_audit'");
        if (priceAuditTable > 0)
        {
            await session.ExecuteAsync(
                "DELETE FROM production_cost_price_audit WHERE COMPANY_CD=@Company AND RUN_ID=@RunId", args);
        }

        await session.ExecuteAsync(@"
UPDATE production_cost_run
SET ISDEL='1', STATUS=@Superseded, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0'",
            new { Company = company, RunId = runId, Superseded = GkRunStatus.Superseded });
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
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(update);
        ProductionCostWriteContext.EnsureCompany(companyCd);

        var items = ResolveSelectionItems(update);
        if (items.Count == 0)
            throw new ArgumentException("Cần ít nhất một dòng DEPARTMENT_ID + PRODUCT_ID để cập nhật lựa chọn.");

        var locked = await LockRunForUpdateAsync(session, companyCd, update.RunId)
            ?? throw new InvalidOperationException("RUN không tồn tại.");
        if (update.RunVersion <= 0 || locked.RunVersion != update.RunVersion)
            throw new InvalidOperationException("RUN_VERSION đã thay đổi. Tải lại kỳ G-K rồi chọn lại.");
        if (!string.IsNullOrWhiteSpace(update.PeriodYm)
            && !string.Equals(locked.PeriodYm?.Trim(), update.PeriodYm.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("PERIOD_YM không khớp RUN hiện hành.");
        if (!GkRunStatus.AllowsPoolRecalc(locked.Status))
            throw new InvalidOperationException(
                "RUN đã khóa phân bổ hoặc đã CONFIRMED. Không được đổi lựa chọn đối tượng; mở lại (REOPEN) trước.");

        var statuses = await GetStepStatusesForUpdateAsync(session, companyCd, update.RunId);
        if (statuses.Any(x =>
            !string.Equals(x.Value, "NOT_STARTED", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x.Value, "OPEN", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x.Value, "REOPENED", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "RUN đã có bước tính/khóa giá thành. Không được đổi lựa chọn đối tượng trước khi mở lại các bước liên quan.");

        var company = companyCd.Trim();
        var user = userId ?? string.Empty;
        const string updateSql = @"
UPDATE production_cost_object
SET IS_SELECTED=@Selected, UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0'
  AND DEPARTMENT_ID=@DepartmentId
  AND PRODUCT_ID=@ProductId
  AND (@AllowInvalid = 1 OR IFNULL(WARNING_TEXT,'') = '')";

        foreach (var item in items)
        {
            // Chỉ cập nhật đúng cặp DEPARTMENT_ID + PRODUCT_ID — không wildcard ProductId=0.
            if (item.DepartmentId <= 0 || item.ProductId <= 0)
                throw new InvalidOperationException(
                    "Không chọn được dòng thiếu DEPARTMENT_ID hoặc PRODUCT_ID.");

            var affected = await session.ExecuteAsync(
                updateSql,
                new
                {
                    Selected = item.Selected ? "1" : "0",
                    UserId = user,
                    Company = company,
                    update.RunId,
                    item.DepartmentId,
                    item.ProductId,
                    // Khi bỏ chọn vẫn cho phép dòng có WARNING_TEXT; khi chọn thì chặn mapping lỗi.
                    AllowInvalid = item.Selected ? 0 : 1
                });

            if (affected == 0)
            {
                var exists = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT COUNT(1) FROM production_cost_object
WHERE COMPANY_CD=@Company AND RUN_ID=@RunId AND ISDEL='0'
  AND DEPARTMENT_ID=@DepartmentId AND PRODUCT_ID=@ProductId",
                    new { Company = company, update.RunId, item.DepartmentId, item.ProductId });
                if (exists == 0)
                    throw new InvalidOperationException(
                        $"Không có đối tượng khớp DEPARTMENT_ID={item.DepartmentId}/PRODUCT_ID={item.ProductId} trên RUN hiện hành.");
                throw new InvalidOperationException(
                    $"Không được chọn thành phẩm có mapping không hợp lệ (DEPARTMENT_ID={item.DepartmentId}, PRODUCT_ID={item.ProductId}).");
            }

            if (affected != 1)
                throw new InvalidOperationException(
                    $"Cập nhật lựa chọn không đúng 1 dòng (DEPARTMENT_ID={item.DepartmentId}, PRODUCT_ID={item.ProductId}, affected={affected}).");
        }
    }

    private static List<ProductionCostObjectSelectionItem> ResolveSelectionItems(
        ProductionCostObjectSelectionUpdate update)
    {
        if (update.Items is { Count: > 0 })
        {
            // Gộp trùng khóa theo cặp cuối cùng trong payload (tránh cập nhật 2 lần cùng dòng).
            var map = new Dictionary<(long DepartmentId, long ProductId), ProductionCostObjectSelectionItem>();
            foreach (var item in update.Items)
            {
                map[(item.DepartmentId, item.ProductId)] = new ProductionCostObjectSelectionItem
                {
                    DepartmentId = item.DepartmentId,
                    ProductId = item.ProductId,
                    Selected = item.Selected
                };
            }
            return map.Values.ToList();
        }

        if (update.DepartmentId > 0 && update.ProductId > 0)
        {
            return
            [
                new ProductionCostObjectSelectionItem
                {
                    DepartmentId = update.DepartmentId,
                    ProductId = update.ProductId,
                    Selected = update.Selected
                }
            ];
        }

        return [];
    }

    /// <summary>
    /// Làm mới danh sách đối tượng/thành phẩm của RUN hiện hành (không đổi RUN_ID / cấu hình / 6 bước).
    /// DELETE ALL + INSERT NEW trong TX hiện tại, giới hạn COMPANY_CD + RUN_ID.
    /// Không dùng ON DUPLICATE KEY UPDATE để giữ dòng cũ.
    /// </summary>
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
                "Chỉ đồng bộ đối tượng khi RUN còn DRAFT/PREVIEWED, trước khi xác nhận giá thành.");

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

        // An empty source is a valid snapshot: reinitializing a draft RUN
        // then clears all stale cost objects instead of retaining old entries.
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

            // Keep orphan products visible as unselected rows with a warning.
            // The prerequisite panel can then guide the user to fix mappings.
            var valid = departmentId > 0 && departmentCd.Length > 0;
            resolved.Add(new ObjectSyncResolvedRow(
                source.ProductId, departmentId, departmentCd, source.ProductCd ?? "",
                valid,
                valid ? null : "Thành phẩm chưa có DEPARTMENT_ID/DEPARTMENT_CD hợp lệ."));
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
  IS_SELECTED, WARNING_TEXT, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
) VALUES (
  @RunId, @Company, @DepartmentId, @ProductId, @DepartmentCd, @ProductCd,
  @Selected, @WarningText, '0', @User, UTC_TIMESTAMP(), @User, UTC_TIMESTAMP()
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
                Selected = row.IsValid ? "1" : "0",
                row.WarningText,
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
        long ProductId, long DepartmentId, string DepartmentCd, string ProductCd,
        bool IsValid, string? WarningText);

    private sealed class EnsureRow
    {
        public long PeriodCostId { get; set; }
        public long? CurrentRunId { get; set; }
    }

    private sealed class EnsureRunSafetyRow
    {
        public long RunId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RunVersion { get; set; }
    }

    private sealed class EnsureUnsafeFlags
    {
        public int HasPostedVoucher { get; set; }
        public int HasAppliedInventory { get; set; }
        public int HasBiStepDone { get; set; }
    }

    private sealed class BoundRow
    {
        public string FromYmd { get; set; } = string.Empty;
        public string ToYmd { get; set; } = string.Empty;
    }
}

using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;
using API_AMNOTE_WEB.Services.ProductionCost.Transfer;
using API_AMNOTE_WEB.Services.ProductionCost.Valuation;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;
using MySqlConnector;

namespace API_AMNOTE_WEB.Repositories;

public sealed partial class ProductionCostWriteRepository
{
    public async Task<InventoryInputValuationRow?> GetInventoryInputByInputIdAsync(
        DapperSession session,
        string companyCd,
        long inputId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (inputId <= 0)
            throw new ArgumentException("INPUT_ID phải > 0.");

        const string sql = @"
SELECT INPUT_ID AS InputId, COMPANY_CD AS CompanyCd, IFNULL(PRODUCT_ID, 0) AS ProductId,
       PRODUCT_CD AS ProductCd, STORE_ID AS StoreId, STORE_CD AS StoreCd, INVENTORY_ID AS InventoryId,
       CAST(QUANTITY AS DECIMAL(18,6)) AS Qty,
       CAST(UNIT_PRICE_CC AS DECIMAL(18,6)) AS UnitPrice,
       CAST(AMOUNT_CC AS DECIMAL(18,2)) AS Amount,
       DATE_FORMAT(INVENTORY_YMD, '%Y%m') AS PeriodYm
FROM chit_inventory_input
WHERE COMPANY_CD = @CompanyCd AND INPUT_ID = @InputId AND ISDEL = '0'
LIMIT 1
FOR UPDATE";

        try
        {
            return await session.QueryFirstOrDefaultAsync<InventoryInputValuationRow>(
                sql, new { CompanyCd = companyCd.Trim(), InputId = inputId });
        }
        catch (MySqlException ex)
        {
            throw new InvalidOperationException($"Đọc INPUT_ID={inputId}: {ex.Message}", ex);
        }
    }

    public async Task ApplyInventoryInputPriceByInputIdAsync(
        DapperSession session,
        string companyCd,
        ValuationPriceApplyPlanLine line,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(line);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ValuationConfirmSourceGuard.EnsureInputIdSafeApplyReady(SupportsInputIdSafeInventoryUpdate);
        await EnsurePriceAuditTableAsync(session);

        var current = await GetInventoryInputByInputIdAsync(session, companyCd, line.InputId)
            ?? throw new InvalidOperationException($"INPUT_ID={line.InputId} không thuộc COMPANY_CD={companyCd}.");

        if (current.ProductId != line.ProductId)
        {
            throw new InvalidOperationException(
                $"INPUT_ID={line.InputId}: PRODUCT_ID kho ({current.ProductId}) ≠ plan ({line.ProductId}).");
        }

        if (Math.Abs(current.Qty - line.Qty) > 0.000001m)
        {
            throw new InvalidOperationException(
                $"INPUT_ID={line.InputId}: QTY kho ({current.Qty}) ≠ plan ({line.Qty}). Không cập nhật.");
        }

        await AssertPeriodNotLockedAsync(session, companyCd, current.PeriodYm);
        await AssertLinkedVoucherMutableAsync(session, companyCd, line.InputId, isOutput: false);

        if (Math.Abs(current.UnitPrice - line.OldUnitPrice) > 0.0001m
            || Math.Abs(current.Amount - line.OldAmount) > 0.05m)
        {
            throw new InvalidOperationException(
                $"INPUT_ID={line.InputId}: giá kho đã đổi so với plan " +
                $"(đơn giá {current.UnitPrice} / thành tiền {current.Amount}). Confirm bị hủy.");
        }

        await InsertPriceAuditAsync(
            session, companyCd, line.InputId, "INPUT", current.ProductId, current.StoreId, null,
            current.UnitPrice, current.Amount, line.NewUnitPrice, line.NewAmount, userId, current.PeriodYm, null);

        const string updateSql = @"
UPDATE chit_inventory_input
SET UNIT_PRICE_CC = @NewPrice,
    AMOUNT_CC = @NewAmount,
    AMOUNT_TOTAL = @NewAmount,
    UPDATE_BY = @UserId,
    UPDATE_AT = UTC_TIMESTAMP()
WHERE COMPANY_CD = @CompanyCd
  AND INPUT_ID = @InputId
  AND PRODUCT_ID = @ProductId
  AND ISDEL = '0'
  AND ABS(QUANTITY - @Qty) < 0.000001
  AND ABS(UNIT_PRICE_CC - @OldPrice) < 0.0001";

        var affected = await session.ExecuteAsync(updateSql, new
        {
            CompanyCd = companyCd.Trim(),
            line.InputId,
            line.ProductId,
            Qty = line.Qty,
            OldPrice = line.OldUnitPrice,
            NewPrice = line.NewUnitPrice,
            NewAmount = line.NewAmount,
            UserId = userId ?? string.Empty
        });
        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"UPDATE INPUT_ID={line.InputId} ảnh hưởng {affected} dòng (kỳ vọng 1). Transaction sẽ rollback.");
        }

        await RefreshInputAccountingAmountAsync(session, companyCd, line.InputId, userId);
    }

    public async Task ApplyInventoryOutPricesAsync(
        DapperSession session,
        string companyCd,
        string periodYm,
        IReadOnlyList<ValuationAverageResult> afterGroups,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(afterGroups);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ValuationConfirmSourceGuard.EnsureInputIdSafeApplyReady(SupportsInputIdSafeInventoryUpdate);
        await EnsurePriceAuditTableAsync(session);
        await AssertPeriodNotLockedAsync(session, companyCd, periodYm);

        var bounds = await QueryFirstOrDefaultAsync<PeriodBoundLite>(
            session,
            @"SELECT FROM_YMD AS FromYmd, TO_YMD AS ToYmd
              FROM production_cost_period
              WHERE COMPANY_CD=@CompanyCd AND PERIOD_YM=@PeriodYm AND ISDEL='0' LIMIT 1",
            new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm.Trim() },
            "kỳ để cập nhật giá xuất");
        if (bounds == null)
            throw new InvalidOperationException($"Không có cận ngày của kỳ {periodYm}.");

        foreach (var group in afterGroups.Where(g => g.OutputQuantity > 0))
        {
            var rows = (await QueryOutRowsAsync(session, companyCd, bounds, group)).ToList();
            var qty = rows.Sum(r => r.Quantity);
            if (Math.Abs(qty - group.OutputQuantity) > 0.001m)
            {
                throw new InvalidOperationException(
                    $"Nhóm {group.ProductCd}/{group.StoreCd}/{group.ChitType}: " +
                    $"SL xuất kho {qty} ≠ kết quả engine {group.OutputQuantity}. Không cập nhật giá xuất.");
            }

            decimal posted = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var amount = i == rows.Count - 1
                    ? AllocationMath.RoundDigit(group.OutputAmount - posted, 2)
                    : AllocationMath.RoundDigit(row.Quantity * group.OutputAverageUnitCost, 2);
                posted += amount;

                await InsertPriceAuditAsync(
                    session, companyCd, row.OutputId, "OUTPUT", row.ProductId, row.StoreId, row.ChitType,
                    row.UnitPrice, row.Amount, group.OutputAverageUnitCost, amount, userId, periodYm, null);

                var affected = await session.ExecuteAsync(@"
UPDATE chit_inventory_output
SET UNIT_PRICE_CC = @Price, AMOUNT_CC = @Amount, AMOUNT_TOTAL = @Amount,
    UPDATE_BY = @UserId, UPDATE_AT = UTC_TIMESTAMP()
WHERE COMPANY_CD = @CompanyCd AND OUTPUT_ID = @OutputId AND PRODUCT_ID = @ProductId
  AND IFNULL(STORE_ID, 0) = @StoreId AND IFNULL(CHIT_TYPE, '') = @ChitType AND ISDEL = '0'",
                    new
                    {
                        CompanyCd = companyCd.Trim(),
                        row.OutputId,
                        row.ProductId,
                        StoreId = row.StoreId,
                        ChitType = row.ChitType ?? string.Empty,
                        Price = group.OutputAverageUnitCost,
                        Amount = amount,
                        UserId = userId ?? string.Empty
                    });
                if (affected != 1)
                {
                    throw new InvalidOperationException(
                        $"UPDATE OUTPUT_ID={row.OutputId} ảnh hưởng {affected} dòng. Rollback.");
                }
            }
        }
    }

    public async Task UpsertValuationCogsAsync(
        DapperSession session,
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        long? existingChitId,
        IReadOnlyList<Cogs632PreviewLine> cogs,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(cogs);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        ValuationConfirmSourceGuard.EnsureInputIdSafeApplyReady(SupportsInputIdSafeInventoryUpdate);

        var stableKey = ValuationApplyPlan.StableKey(companyCd, periodYm);
        var refRow = await QueryFirstOrDefaultAsync<VoucherChitRow>(
            session,
            @"SELECT CHIT_ID AS ChitId, STABLE_KEY AS StableKey
              FROM production_cost_voucher_ref
              WHERE COMPANY_CD=@CompanyCd AND STABLE_KEY=@StableKey AND ISDEL='0'
              LIMIT 1 FOR UPDATE",
            new { CompanyCd = companyCd.Trim(), StableKey = stableKey },
            "khóa VALUATION_COGS");

        long? chitId = refRow?.ChitId is > 0 ? refRow.ChitId : existingChitId;
        if (chitId is > 0)
        {
            var owner = await QueryFirstOrDefaultAsync<ChitOwnerRow>(
                session,
                @"SELECT CHIT_ID AS ChitId, COMPANY_CD AS CompanyCd, INPUT_TYPE AS InputType, ISDEL AS IsDel
                  FROM chitinfo WHERE CHIT_ID=@ChitId LIMIT 1 FOR UPDATE",
                new { ChitId = chitId },
                "đọc chứng từ VALUATION_COGS");
            if (owner == null
                || !string.Equals(owner.CompanyCd, companyCd.Trim(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(owner.InputType, "GK_VALUATION", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"CHIT_ID={chitId} không phải chứng từ GK_VALUATION của COMPANY_CD={companyCd}. Không ghi đè chứng từ khác.");
            }
        }

        var lines = cogs.Where(c => c.CogsDelta != 0 || c.CogsAmountAfter != 0).ToList();
        var amount = AllocationMath.RoundDigit(lines.Sum(c => c.CogsDelta), 0);
        var ymd = periodYm.Trim() + LastDay(periodYm).ToString("dd");

        if (chitId is not > 0)
        {
            var duplicate = await QueryFirstOrDefaultAsync<long?>(
                session,
                @"SELECT c.CHIT_ID
                  FROM chitinfo c
                  INNER JOIN chitinfo_ext e ON e.COMPANY_CD=c.COMPANY_CD AND e.CHIT_ID=c.CHIT_ID AND e.ISDEL='0'
                  WHERE c.COMPANY_CD=@CompanyCd AND c.ISDEL='0' AND c.INPUT_TYPE='GK_VALUATION' AND e.NOTE=@StableKey
                  LIMIT 1",
                new { CompanyCd = companyCd.Trim(), StableKey = stableKey },
                "kiểm tra trùng VALUATION_COGS");
            if (duplicate is > 0)
            {
                throw new InvalidOperationException(
                    $"Đã có chứng từ GK_VALUATION CHIT_ID={duplicate} theo STABLE_KEY nhưng voucher_ref chưa trỏ. Không INSERT thêm.");
            }

            var chitCd = ("GV" + periodYm + DateTime.UtcNow.ToString("HHmmssfff"));
            if (chitCd.Length > 30)
                chitCd = chitCd[..30];
            await session.ExecuteAsync(@"
INSERT INTO chitinfo (
  COMPANY_CD, CHIT_CD, CHIT_NO, CHIT_YMD, CHIT_TYPE, INPUT_TYPE, LOCK_STEP_CODE, AMOUNT, ISDEL, CREATE_BY, UPDATE_BY
) VALUES (
  @CompanyCd, @ChitCd, @ChitCd, @Ymd, 'OT', 'GK_VALUATION', 'VALUATION_COGS', @Amount, '0', @UserId, @UserId
)", new { CompanyCd = companyCd.Trim(), ChitCd = chitCd, Ymd = ymd, Amount = amount, UserId = userId ?? string.Empty });

            chitId = await session.QueryFirstOrDefaultAsync<long>(
                "SELECT LAST_INSERT_ID()");
            await session.ExecuteAsync(@"
INSERT INTO chitinfo_ext (
  COMPANY_CD, CHIT_ID, CHIT_CD, IS_LOCK, ISEXCEL, IS_CONFIRMED, IS_PAYMENT, NOTE, ISDEL, CREATE_BY, UPDATE_BY
) VALUES (
  @CompanyCd, @ChitId, @ChitCd, '0', '0', '0', '0', @StableKey, '0', @UserId, @UserId
)", new { CompanyCd = companyCd.Trim(), ChitId = chitId, ChitCd = chitCd.Length > 20 ? chitCd[..20] : chitCd, StableKey = stableKey, UserId = userId ?? string.Empty });
        }
        else
        {
            var affected = await session.ExecuteAsync(@"
UPDATE chitinfo
SET AMOUNT=@Amount, UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE CHIT_ID=@ChitId AND COMPANY_CD=@CompanyCd AND INPUT_TYPE='GK_VALUATION' AND ISDEL='0'",
                new { Amount = amount, UserId = userId ?? string.Empty, ChitId = chitId, CompanyCd = companyCd.Trim() });
            if (affected != 1)
                throw new InvalidOperationException($"Không cập nhật được header VALUATION_COGS CHIT_ID={chitId}.");

            await session.ExecuteAsync(@"
UPDATE chitdetailinfo
SET ISDEL='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND CHIT_ID=@ChitId AND ISDEL='0'",
                new { CompanyCd = companyCd.Trim(), ChitId = chitId, UserId = userId ?? string.Empty });
        }

        var sort = 1;
        foreach (var line in lines)
        {
            var detailCd = ("D" + chitId + sort).PadLeft(8, '0');
            if (detailCd.Length > 30)
                detailCd = detailCd[..30];
            await session.ExecuteAsync(@"
INSERT INTO chitdetailinfo (
  COMPANY_CD, CHIT_ID, CHIT_CD, CHITDETAIL_CD, CHIT_YMD, DEBIT, CREDIT, AMOUNT, FC_AMOUNT, FC_RATE, SORT, ISDEL, CREATE_BY, UPDATE_BY
) VALUES (
  @CompanyCd, @ChitId, @ChitCd, @DetailCd, @Ymd, '632', '155', @Amount, 0, 0, @Sort, '0', @UserId, @UserId
)", new
            {
                CompanyCd = companyCd.Trim(),
                ChitId = chitId,
                ChitCd = "GV" + periodYm,
                DetailCd = detailCd,
                Ymd = ymd,
                Amount = line.CogsDelta,
                Sort = sort++,
                UserId = userId ?? string.Empty
            });
        }

        await session.ExecuteAsync(@"
UPDATE production_cost_voucher_ref
SET CHIT_ID=@ChitId, RUN_ID=@RunId, RUN_VERSION=@RunVersion, ISDEL='0', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND STABLE_KEY=@StableKey",
            new
            {
                ChitId = chitId,
                RunId = runId,
                RunVersion = runVersion,
                UserId = userId ?? string.Empty,
                CompanyCd = companyCd.Trim(),
                StableKey = stableKey
            });
    }

    public async Task MarkReceiptsAppliedAsync(
        DapperSession session,
        string companyCd,
        long runId,
        int runVersion,
        IReadOnlyList<long> inputIds,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputIds);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        ValuationConfirmSourceGuard.EnsureInputIdSafeApplyReady(SupportsInputIdSafeInventoryUpdate);
        if (inputIds.Count == 0)
            throw new InvalidOperationException("Không có INPUT_ID để đánh IS_APPLIED.");

        var affected = await session.ExecuteAsync(@"
UPDATE production_cost_receipt
SET IS_APPLIED='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0'
  AND INPUT_ID IN @InputIds",
            new
            {
                CompanyCd = companyCd.Trim(),
                RunId = runId,
                RunVersion = runVersion,
                UserId = userId ?? string.Empty,
                InputIds = inputIds.Distinct().ToArray()
            });
        if (affected != inputIds.Distinct().Count())
        {
            throw new InvalidOperationException(
                $"Đánh IS_APPLIED ảnh hưởng {affected} dòng, kỳ vọng {inputIds.Distinct().Count()}. Rollback.");
        }
    }

    public async Task ReverseReopenSideEffectsAsync(
        DapperSession session,
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        IReadOnlyList<string> cascadeReverse,
        string? userId)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(cascadeReverse);
        ProductionCostWriteContext.EnsureCompany(companyCd);
        if (!SupportsSafeReopenSideEffectReverse)
            throw new InvalidOperationException(ReopenImpactGuard.BlockedReasonPrefix);

        var cascade = cascadeReverse.Select(GkStepOrder.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        await BlockPeriodLockStepsWeCannotReverseAsync(session, companyCd, periodYm, cascade);

        if (cascade.Contains(ProductionCostStepCode.Valuation))
            await ReverseValuationAsync(session, companyCd, periodYm, runId, runVersion, userId);

        if (cascade.Contains(ProductionCostStepCode.ExpTo154Only))
            await ReverseExpTo154Async(session, companyCd, periodYm, userId);
    }

    private async Task ReverseValuationAsync(
        DapperSession session, string companyCd, string periodYm, long runId, int runVersion, string? userId)
    {
        await EnsurePriceAuditTableAsync(session);
        var audits = (await session.QueryAsync<PriceAuditRow>(@"
SELECT AUDIT_ID AS AuditId, TARGET_KIND AS TargetKind, ROW_ID AS RowId,
       OLD_UNIT_PRICE AS OldUnitPrice, OLD_AMOUNT AS OldAmount,
       NEW_UNIT_PRICE AS NewUnitPrice, NEW_AMOUNT AS NewAmount
FROM production_cost_price_audit
WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND IS_REVERSED='0' AND ISDEL='0'
FOR UPDATE",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion })).ToList();

        var applied = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT COUNT(1) FROM production_cost_receipt
WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0' AND IS_APPLIED='1'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion });
        if (applied > 0 && audits.Count == 0)
        {
            throw new InvalidOperationException(
                "REOPEN bị chặn: receipt IS_APPLIED=1 nhưng không có snapshot production_cost_price_audit để hoàn giá.");
        }

        foreach (var audit in audits)
        {
            if (string.Equals(audit.TargetKind, "INPUT", StringComparison.OrdinalIgnoreCase))
            {
                var affected = await session.ExecuteAsync(@"
UPDATE chit_inventory_input
SET UNIT_PRICE_CC=@OldPrice, AMOUNT_CC=@OldAmount, AMOUNT_TOTAL=@OldAmount,
    UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND INPUT_ID=@RowId AND ISDEL='0'
  AND ABS(UNIT_PRICE_CC - @NewPrice) < 0.0001
  AND ABS(AMOUNT_CC - @NewAmount) < 0.05",
                    new
                    {
                        CompanyCd = companyCd.Trim(),
                        audit.RowId,
                        OldPrice = audit.OldUnitPrice,
                        OldAmount = audit.OldAmount,
                        NewPrice = audit.NewUnitPrice,
                        NewAmount = audit.NewAmount,
                        UserId = userId ?? string.Empty
                    });
                if (affected != 1)
                {
                    throw new InvalidOperationException(
                        $"Không hoàn giá INPUT_ID={audit.RowId}: giá hiện tại không khớp snapshot đã áp. REOPEN bị chặn.");
                }
            }
            else if (string.Equals(audit.TargetKind, "OUTPUT", StringComparison.OrdinalIgnoreCase))
            {
                var affected = await session.ExecuteAsync(@"
UPDATE chit_inventory_output
SET UNIT_PRICE_CC=@OldPrice, AMOUNT_CC=@OldAmount, AMOUNT_TOTAL=@OldAmount,
    UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND OUTPUT_ID=@RowId AND ISDEL='0'
  AND ABS(UNIT_PRICE_CC - @NewPrice) < 0.0001
  AND ABS(AMOUNT_CC - @NewAmount) < 0.05",
                    new
                    {
                        CompanyCd = companyCd.Trim(),
                        audit.RowId,
                        OldPrice = audit.OldUnitPrice,
                        OldAmount = audit.OldAmount,
                        NewPrice = audit.NewUnitPrice,
                        NewAmount = audit.NewAmount,
                        UserId = userId ?? string.Empty
                    });
                if (affected != 1)
                {
                    throw new InvalidOperationException(
                        $"Không hoàn giá OUTPUT_ID={audit.RowId}: giá hiện tại không khớp snapshot. REOPEN bị chặn.");
                }
            }
            else
            {
                throw new InvalidOperationException($"TARGET_KIND audit không hỗ trợ: {audit.TargetKind}.");
            }

            await session.ExecuteAsync(@"
UPDATE production_cost_price_audit
SET IS_REVERSED='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE AUDIT_ID=@AuditId AND COMPANY_CD=@CompanyCd AND IS_REVERSED='0'",
                new { audit.AuditId, CompanyCd = companyCd.Trim(), UserId = userId ?? string.Empty });
        }

        await session.ExecuteAsync(@"
UPDATE production_cost_receipt
SET IS_APPLIED='0', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND RUN_ID=@RunId AND RUN_VERSION=@RunVersion AND ISDEL='0' AND IS_APPLIED='1'",
            new { CompanyCd = companyCd.Trim(), RunId = runId, RunVersion = runVersion, UserId = userId ?? string.Empty });

        var stableKey = ValuationApplyPlan.StableKey(companyCd, periodYm);
        var chitId = await session.QueryFirstOrDefaultAsync<long?>(@"
SELECT CHIT_ID FROM production_cost_voucher_ref
WHERE COMPANY_CD=@CompanyCd AND STABLE_KEY=@StableKey AND ISDEL='0' LIMIT 1 FOR UPDATE",
            new { CompanyCd = companyCd.Trim(), StableKey = stableKey });
        if (chitId is > 0)
        {
            var affected = await session.ExecuteAsync(@"
UPDATE chitinfo SET ISDEL='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE CHIT_ID=@ChitId AND COMPANY_CD=@CompanyCd AND INPUT_TYPE='GK_VALUATION' AND ISDEL='0'",
                new { ChitId = chitId, CompanyCd = companyCd.Trim(), UserId = userId ?? string.Empty });
            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Không soft-delete được VALUATION_COGS CHIT_ID={chitId} (không phải GK_VALUATION). REOPEN bị chặn.");
            }

            await session.ExecuteAsync(@"
UPDATE chitdetailinfo SET ISDEL='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND CHIT_ID=@ChitId AND ISDEL='0'",
                new { CompanyCd = companyCd.Trim(), ChitId = chitId, UserId = userId ?? string.Empty });
        }

        await session.ExecuteAsync(@"
UPDATE production_cost_voucher_ref
SET ISDEL='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND STABLE_KEY=@StableKey AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), StableKey = stableKey, UserId = userId ?? string.Empty });
    }

    private async Task ReverseExpTo154Async(DapperSession session, string companyCd, string periodYm, string? userId)
    {
        var stableKey = ExpTo154Rules.StableKey(companyCd, periodYm);
        var chitId = await session.QueryFirstOrDefaultAsync<long?>(@"
SELECT CHIT_ID FROM production_cost_voucher_ref
WHERE COMPANY_CD=@CompanyCd AND STABLE_KEY=@StableKey AND ISDEL='0' LIMIT 1 FOR UPDATE",
            new { CompanyCd = companyCd.Trim(), StableKey = stableKey });
        if (chitId is not > 0)
            return;

        var affected = await session.ExecuteAsync(@"
UPDATE chitinfo SET ISDEL='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE CHIT_ID=@ChitId AND COMPANY_CD=@CompanyCd AND INPUT_TYPE='LOCK' AND LOCK_STEP_CODE='COGS_SUMMARY' AND ISDEL='0'",
            new { ChitId = chitId, CompanyCd = companyCd.Trim(), UserId = userId ?? string.Empty });
        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Không hoàn chứng từ EXP_TO_154 CHIT_ID={chitId}: không còn là phiếu LOCK/COGS_SUMMARY. REOPEN bị chặn.");
        }

        await session.ExecuteAsync(@"
UPDATE chitdetailinfo SET ISDEL='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND CHIT_ID=@ChitId AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), ChitId = chitId, UserId = userId ?? string.Empty });
        await session.ExecuteAsync(@"
UPDATE production_cost_voucher_ref SET ISDEL='1', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND STABLE_KEY=@StableKey AND ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), StableKey = stableKey, UserId = userId ?? string.Empty });
        await session.ExecuteAsync(@"
UPDATE production_cost_period
SET COST_ENGINE='NONE', COGS_RULE=NULL, STATUS='OPEN', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND PERIOD_YM=@PeriodYm AND ISDEL='0'
  AND COST_ENGINE='GK' AND COGS_RULE='EXP_TO_154_ONLY'",
            new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm.Trim(), UserId = userId ?? string.Empty });
        await session.ExecuteAsync(@"
UPDATE acc_period_lock_step
SET STATUS='OPEN', FINISHED_AT=NULL, MESSAGE='G-K REOPEN EXP_TO_154_ONLY', UPDATE_BY=@UserId, UPDATE_AT=UTC_TIMESTAMP()
WHERE COMPANY_CD=@CompanyCd AND PERIOD_YM=@PeriodYm AND STEP_CODE='COGS_SUMMARY'",
            new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm.Trim(), UserId = userId ?? string.Empty });
    }

    private static async Task BlockPeriodLockStepsWeCannotReverseAsync(
        DapperSession session, string companyCd, string periodYm, HashSet<string> cascade)
    {
        if (cascade.Contains(ProductionCostStepCode.FaPrepaidLock))
        {
            var status = await session.QueryFirstOrDefaultAsync<string>(@"
SELECT STATUS FROM acc_period_lock_step
WHERE COMPANY_CD=@CompanyCd AND PERIOD_YM=@PeriodYm AND STEP_CODE='FA_PREPAID_LOCK' LIMIT 1",
                new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm.Trim() });
            if (string.Equals(status, "DONE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "REOPEN bị chặn: FA_PREPAID_LOCK đang DONE trên PeriodLock. " +
                    "Mở sổ bằng chức năng khóa sổ hiện có. G-K không đánh dấu thành công khi chưa hoàn nguyên TSCĐ/CPTT.");
            }
        }

        if (cascade.Contains(ProductionCostStepCode.ProfitLossReport))
        {
            var status = await session.QueryFirstOrDefaultAsync<string>(@"
SELECT STATUS FROM acc_period_lock_step
WHERE COMPANY_CD=@CompanyCd AND PERIOD_YM=@PeriodYm AND STEP_CODE='PROFIT_LOSS_REPORT' LIMIT 1",
                new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm.Trim() });
            if (string.Equals(status, "DONE", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "REOPEN bị chặn: PROFIT_LOSS_REPORT đang DONE trên PeriodLock. " +
                    "Mở sổ bằng chức năng khóa sổ hiện có. G-K không đánh dấu thành công khi chưa hoàn nguyên lãi lỗ.");
            }
        }
    }

    private async Task EnsurePriceAuditTableAsync(DapperSession session)
    {
        var count = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT COUNT(*) FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_cost_price_audit'");
        if (count == 0)
        {
            throw new InvalidOperationException(
                "Thiếu bảng production_cost_price_audit. Chạy migration 20261103_gk_final_price_audit.sql trước khi ghi giá kho. Không cập nhật.");
        }
    }

    private static async Task AssertPeriodNotLockedAsync(DapperSession session, string companyCd, string? periodYm)
    {
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            return;
        var status = await session.QueryFirstOrDefaultAsync<string>(@"
SELECT STATUS FROM acc_period_lock_month
WHERE COMPANY_CD=@CompanyCd AND PERIOD_YM=@PeriodYm AND ISDEL='0' LIMIT 1",
            new { CompanyCd = companyCd.Trim(), PeriodYm = periodYm.Trim() });
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "PROCESSING", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Kỳ {periodYm} đang {status}. Không cập nhật giá kho.");
        }
    }

    private static async Task AssertLinkedVoucherMutableAsync(
        DapperSession session, string companyCd, long inputId, bool isOutput)
    {
        _ = isOutput;
        var locked = await session.QueryFirstOrDefaultAsync<int>(@"
SELECT COUNT(1)
FROM chit_inventory_input i
INNER JOIN chitdetailinfo d ON d.COMPANY_CD=i.COMPANY_CD AND d.CHITDETAIL_ID=i.CHITDETAIL_ID
INNER JOIN chitinfo h ON h.COMPANY_CD=d.COMPANY_CD AND h.CHIT_ID=d.CHIT_ID
LEFT JOIN chitinfo_ext e ON e.COMPANY_CD=h.COMPANY_CD AND e.CHIT_ID=h.CHIT_ID AND e.ISDEL='0'
WHERE i.COMPANY_CD=@CompanyCd AND i.INPUT_ID=@InputId AND i.ISDEL='0'
  AND (IFNULL(d.ISDEL,'0')<>'0' OR IFNULL(h.ISDEL,'0')<>'0' OR IFNULL(e.IS_LOCK,'0')='1')",
            new { CompanyCd = companyCd.Trim(), InputId = inputId });
        if (locked > 0)
            throw new InvalidOperationException($"INPUT_ID={inputId} gắn chứng từ đã xóa hoặc đang khóa. Không cập nhật giá.");
    }

    private async Task RefreshInputAccountingAmountAsync(
        DapperSession session, string companyCd, long inputId, string? userId)
    {
        var detailId = await session.QueryFirstOrDefaultAsync<long?>(@"
SELECT CHITDETAIL_ID FROM chit_inventory_input
WHERE COMPANY_CD=@CompanyCd AND INPUT_ID=@InputId AND ISDEL='0' LIMIT 1",
            new { CompanyCd = companyCd.Trim(), InputId = inputId });
        if (detailId is not > 0)
            return;

        await session.ExecuteAsync(@"
UPDATE chitdetailinfo d
JOIN (
  SELECT CHITDETAIL_ID, ROUND(SUM(AMOUNT_CC), 2) AS NewAmount
  FROM chit_inventory_input
  WHERE COMPANY_CD=@CompanyCd AND CHITDETAIL_ID=@DetailId AND ISDEL='0'
  GROUP BY CHITDETAIL_ID
) s ON s.CHITDETAIL_ID = d.CHITDETAIL_ID
SET d.AMOUNT = s.NewAmount, d.UPDATE_BY=@UserId, d.UPDATE_AT=UTC_TIMESTAMP()
WHERE d.COMPANY_CD=@CompanyCd AND d.ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), DetailId = detailId, UserId = userId ?? string.Empty });

        await session.ExecuteAsync(@"
UPDATE chitinfo h
JOIN (
  SELECT CHIT_ID, ROUND(SUM(AMOUNT), 2) AS NewAmount
  FROM chitdetailinfo
  WHERE COMPANY_CD=@CompanyCd AND CHIT_ID = (
    SELECT CHIT_ID FROM chitdetailinfo WHERE COMPANY_CD=@CompanyCd AND CHITDETAIL_ID=@DetailId LIMIT 1
  ) AND ISDEL='0'
  GROUP BY CHIT_ID
) s ON s.CHIT_ID = h.CHIT_ID
SET h.AMOUNT = s.NewAmount, h.UPDATE_BY=@UserId, h.UPDATE_AT=UTC_TIMESTAMP()
WHERE h.COMPANY_CD=@CompanyCd AND h.ISDEL='0'",
            new { CompanyCd = companyCd.Trim(), DetailId = detailId, UserId = userId ?? string.Empty });
    }

    private async Task InsertPriceAuditAsync(
        DapperSession session,
        string companyCd,
        long rowId,
        string targetKind,
        long productId,
        long? storeId,
        string? chitType,
        decimal oldPrice,
        decimal oldAmount,
        decimal newPrice,
        decimal newAmount,
        string? userId,
        string? periodYm,
        long? runId)
    {
        _ = runId;
        if (string.IsNullOrWhiteSpace(periodYm))
            throw new InvalidOperationException("Không ghi audit giá khi thiếu PERIOD_YM.");

        var inserted = await session.ExecuteAsync(@"
INSERT INTO production_cost_price_audit (
  COMPANY_CD, PERIOD_YM, RUN_ID, RUN_VERSION, TARGET_KIND, ROW_ID, PRODUCT_ID, STORE_ID, CHIT_TYPE,
  OLD_UNIT_PRICE, OLD_AMOUNT, NEW_UNIT_PRICE, NEW_AMOUNT, IS_REVERSED, ISDEL, CREATE_BY, CREATE_AT
)
SELECT @CompanyCd, p.PERIOD_YM, r.RUN_ID, r.RUN_VERSION, @TargetKind, @RowId, @ProductId, @StoreId, @ChitType,
       @OldPrice, @OldAmount, @NewPrice, @NewAmount, '0', '0', @UserId, UTC_TIMESTAMP()
FROM production_cost_run r
INNER JOIN production_cost_period p
  ON p.CURRENT_RUN_ID = r.RUN_ID AND p.COMPANY_CD = r.COMPANY_CD AND p.ISDEL='0'
WHERE r.COMPANY_CD=@CompanyCd AND r.ISDEL='0' AND p.PERIOD_YM=@PeriodYm
LIMIT 1",
            new
            {
                CompanyCd = companyCd.Trim(),
                PeriodYm = periodYm,
                TargetKind = targetKind,
                RowId = rowId,
                ProductId = productId,
                StoreId = storeId,
                ChitType = chitType,
                OldPrice = oldPrice,
                OldAmount = oldAmount,
                NewPrice = newPrice,
                NewAmount = newAmount,
                UserId = userId ?? string.Empty
            });
        if (inserted != 1)
        {
            throw new InvalidOperationException(
                "Không ghi được snapshot production_cost_price_audit cho CURRENT_RUN của kỳ. Không cập nhật giá.");
        }
    }

    private async Task<IEnumerable<OutputPriceRow>> QueryOutRowsAsync(
        DapperSession session, string companyCd, PeriodBoundLite bounds, ValuationAverageResult group)
    {
        return await session.QueryAsync<OutputPriceRow>(@"
SELECT o.OUTPUT_ID AS OutputId, IFNULL(o.PRODUCT_ID, 0) AS ProductId, IFNULL(o.STORE_ID, 0) AS StoreId,
       IFNULL(o.CHIT_TYPE, '') AS ChitType,
       CAST(o.QUANTITY AS DECIMAL(18,6)) AS Quantity,
       CAST(o.UNIT_PRICE_CC AS DECIMAL(18,6)) AS UnitPrice,
       CAST(o.AMOUNT_CC AS DECIMAL(18,2)) AS Amount
FROM chit_inventory_output o
LEFT JOIN chit_inventory_info h
  ON h.COMPANY_CD=o.COMPANY_CD AND h.INVENTORY_ID=o.INVENTORY_ID AND IFNULL(h.ISDEL,'0')='0'
WHERE o.COMPANY_CD=@CompanyCd AND o.ISDEL='0'
  AND o.PRODUCT_ID=@ProductId
  AND IFNULL(o.STORE_ID, 0)=@StoreId
  AND IFNULL(o.CHIT_TYPE, '')=@ChitType
  AND COALESCE(DATE_FORMAT(o.INVENTORY_YMD, '%Y%m%d'), NULLIF(TRIM(h.INVENTORY_YMD), '')) BETWEEN @FromYmd AND @ToYmd
FOR UPDATE",
            new
            {
                CompanyCd = companyCd.Trim(),
                group.ProductId,
                StoreId = group.StoreId,
                ChitType = group.ChitType ?? string.Empty,
                bounds.FromYmd,
                bounds.ToYmd
            });
    }

    private static DateTime LastDay(string periodYm)
    {
        var year = int.Parse(periodYm[..4]);
        var month = int.Parse(periodYm.Substring(4, 2));
        return new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
    }

    private sealed class PeriodBoundLite
    {
        public string FromYmd { get; set; } = string.Empty;
        public string ToYmd { get; set; } = string.Empty;
    }

    private sealed class OutputPriceRow
    {
        public long OutputId { get; set; }
        public long ProductId { get; set; }
        public long StoreId { get; set; }
        public string? ChitType { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    private sealed class VoucherChitRow
    {
        public long? ChitId { get; set; }
        public string? StableKey { get; set; }
    }

    private sealed class ChitOwnerRow
    {
        public long ChitId { get; set; }
        public string? CompanyCd { get; set; }
        public string? InputType { get; set; }
        public string? IsDel { get; set; }
    }

    private sealed class PriceAuditRow
    {
        public long AuditId { get; set; }
        public string TargetKind { get; set; } = string.Empty;
        public long RowId { get; set; }
        public decimal OldUnitPrice { get; set; }
        public decimal OldAmount { get; set; }
        public decimal NewUnitPrice { get; set; }
        public decimal NewAmount { get; set; }
    }
}

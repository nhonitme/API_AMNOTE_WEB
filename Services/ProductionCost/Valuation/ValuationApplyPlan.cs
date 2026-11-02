using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Kế hoạch atomic: cập nhật giá nhập INPUT_ID + tính lại xuất + (tuỳ chọn) 632.
/// 2C-04: WouldApplyInventory=false — chỉ lập plan / rollback map.
/// </summary>
public static class ValuationApplyPlan
{
    public static string StableKey(string companyCd, string periodYm) =>
        $"VALUATION_COGS:{companyCd}:{periodYm}";

    public static ValuationTransactionPlan Build(
        string companyCd,
        string periodYm,
        IEnumerable<ReceiptPricePrepLine> receiptPreps,
        long? existingValuationChitId,
        bool writeEnabled,
        string? existingValuationChitCompanyCd = null,
        string? existingValuationStableKey = null)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new ArgumentException("COMPANY_CD bắt buộc trên plan (không update nhầm công ty).");

        Cogs632PreviewRules.AssertExistingVoucherOwnership(
            companyCd,
            periodYm,
            existingValuationChitId,
            existingValuationChitCompanyCd,
            existingValuationStableKey);

        var lines = receiptPreps.Select(r =>
        {
            if (r.InputId <= 0)
                throw new ArgumentException("INPUT_ID không hợp lệ trong plan cập nhật giá.");
            return new ValuationPriceApplyPlanLine
            {
                ProductId = r.ProductId,
                DepartmentId = r.DepartmentId,
                InputId = r.InputId,
                InventoryId = r.InventoryId,
                CompanyCd = string.IsNullOrWhiteSpace(r.CompanyCd) ? companyCd : r.CompanyCd,
                Qty = r.Qty,
                OldUnitPrice = r.OldUnitPrice,
                NewUnitPrice = r.NewUnitPrice,
                OldAmount = r.OldAmount,
                NewAmount = r.NewAmount,
                DeltaAmount = r.DeltaAmount,
                IsApplied = false
            };
        }).ToList();

        var inputIds = new HashSet<long>();
        foreach (var line in lines)
        {
            if (!inputIds.Add(line.InputId))
                throw new InvalidOperationException($"INPUT_ID trùng trong plan: {line.InputId}.");
            if (!string.Equals(line.CompanyCd, companyCd, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Plan INPUT_ID={line.InputId} COMPANY_CD={line.CompanyCd} ≠ {companyCd}.");
            }
        }

        var idem = ConfirmIdempotency.Plan(StableKey(companyCd, periodYm), existingValuationChitId);
        var hasExisting = existingValuationChitId is > 0;
        Cogs632PreviewRules.AssertNoDuplicateCogsPost(
            alreadyHasValuationCogsVoucher: hasExisting,
            wouldInsertNew: false); // 2C-04 chưa post; Confirm DEV phải UPDATE nếu hasExisting

        var rollback = lines.Select(l => new ValuationPriceApplyPlanLine
        {
            ProductId = l.ProductId,
            DepartmentId = l.DepartmentId,
            InputId = l.InputId,
            InventoryId = l.InventoryId,
            CompanyCd = l.CompanyCd,
            Qty = l.Qty,
            OldUnitPrice = l.NewUnitPrice,
            NewUnitPrice = l.OldUnitPrice,
            OldAmount = l.NewAmount,
            NewAmount = l.OldAmount,
            DeltaAmount = -l.DeltaAmount,
            IsApplied = false
        }).ToList();

        return new ValuationTransactionPlan
        {
            StableKey = StableKey(companyCd, periodYm),
            IdempotencyAction = idem.Action.ToString(),
            AtomicInAndOutRequired = true,
            WouldApplyInventory = false,
            WouldPostCogs632 = false,
            ExistingValuationCogsVoucher = hasExisting,
            PriceLines = lines,
            RollbackRestoreLines = rollback,
            Message = writeEnabled
                ? "Plan sẵn sàng — vẫn chưa apply kho trong 2C-04 skeleton. Confirm DEV phải đọc lại B2/B3/B4 từ DB."
                : "Preview plan — WriteGate OFF; không cập nhật kho / không post 632. " +
                  "Step B2/B3/B4 trong DryRun chỉ là mô phỏng client."
        };
    }
}

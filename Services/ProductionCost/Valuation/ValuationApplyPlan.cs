using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Kế hoạch atomic: cập nhật giá nhập INPUT_ID + tính lại xuất + (tuỳ chọn) 632.
/// Preview: WouldApplyInventory=false. Confirm B5: <see cref="BuildForConfirm"/>.
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
        string? existingValuationStableKey = null) =>
        BuildCore(
            companyCd,
            periodYm,
            receiptPreps,
            existingValuationChitId,
            existingValuationChitCompanyCd,
            existingValuationStableKey,
            forConfirm: false,
            writeEnabled);

    /// <summary>
    /// Plan Confirm B5 — WouldApplyInventory + WouldPostCogs632 = true;
    /// đã có STABLE_KEY → UPDATE (không INSERT trùng).
    /// </summary>
    public static ValuationTransactionPlan BuildForConfirm(
        string companyCd,
        string periodYm,
        IEnumerable<ReceiptPricePrepLine> receiptPreps,
        long? existingValuationChitId,
        string? existingValuationChitCompanyCd = null,
        string? existingValuationStableKey = null) =>
        BuildCore(
            companyCd,
            periodYm,
            receiptPreps,
            existingValuationChitId,
            existingValuationChitCompanyCd,
            existingValuationStableKey,
            forConfirm: true,
            writeEnabled: true);

    private static ValuationTransactionPlan BuildCore(
        string companyCd,
        string periodYm,
        IEnumerable<ReceiptPricePrepLine> receiptPreps,
        long? existingValuationChitId,
        string? existingValuationChitCompanyCd,
        string? existingValuationStableKey,
        bool forConfirm,
        bool writeEnabled)
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
        // Đã có STABLE_KEY → UPDATE only (wouldInsertNew=false). InsertOnce chỉ khi chưa có.
        Cogs632PreviewRules.AssertNoDuplicateCogsPost(
            alreadyHasValuationCogsVoucher: hasExisting,
            wouldInsertNew: false);

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
            WouldApplyInventory = forConfirm,
            WouldPostCogs632 = forConfirm,
            ExistingValuationCogsVoucher = hasExisting,
            PriceLines = lines,
            RollbackRestoreLines = rollback,
            Message = forConfirm
                ? $"Confirm plan — apply INPUT_ID + UPSERT {StableKey(companyCd, periodYm)} " +
                  $"({idem.Action}); RollbackRestoreLines={rollback.Count}."
                : writeEnabled
                    ? "Plan sẵn sàng — Preview chưa apply kho. Confirm phải đọc lại B2/B3/B4 từ DB."
                    : "Preview plan — WriteGate OFF; không cập nhật kho / không post 632. " +
                      "Step B2/B3/B4 trong DryRun chỉ là mô phỏng client."
        };
    }
}

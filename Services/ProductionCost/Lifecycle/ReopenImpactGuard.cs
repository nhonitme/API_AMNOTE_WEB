using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Tác động B3/B5 trước REOPEN — không tự hủy chứng từ / giá kho.
/// Thiếu đảo/khôi phục an toàn trong cùng TX → block.
/// </summary>
public static class ReopenImpactGuard
{
    public const string BlockedReasonPrefix =
        "REOPEN bị chặn: chứng từ/giá kho đã phát sinh chưa có cơ chế đảo/khôi phục an toàn trong cùng transaction.";

    public static void AssertSafeToPersist(
        IReadOnlyList<string> cascadeReverse,
        ReopenSideEffectSnapshot effects,
        bool supportsSafeSideEffectReverse)
    {
        ArgumentNullException.ThrowIfNull(cascadeReverse);
        ArgumentNullException.ThrowIfNull(effects);

        if (supportsSafeSideEffectReverse)
            return;

        var cascade = cascadeReverse
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(GkStepOrder.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var blockers = new List<string>();

        if (cascade.Contains(ProductionCostStepCode.ExpTo154Only)
            && (effects.ExpTo154VoucherExists || effects.PeriodClaimedExpTo154Only))
        {
            blockers.Add(
                "EXP_TO_154_ONLY voucher / PeriodLock claim còn hiệu lực — không tự hủy CT N154.");
        }

        if (cascade.Contains(ProductionCostStepCode.Valuation)
            && (effects.ValuationCogsVoucherExists
                || effects.AppliedReceiptCount > 0
                || effects.InventoryPriceUpdated))
        {
            blockers.Add(
                "VALUATION_COGS / IS_APPLIED / giá nhập-xuất đã cập nhật — không tự ghi đè kho/632.");
        }

        // COST_CALC snapshot nội bộ (IS_APPLIED=0) không chặn CreateNew (version cũ bất biến).
        // Chỉ chặn khi receipt đã apply mà cascade đụng COST_CALC (lệch kho vs snapshot).
        if (cascade.Contains(ProductionCostStepCode.CostCalc)
            && !cascade.Contains(ProductionCostStepCode.Valuation)
            && effects.AppliedReceiptCount > 0)
        {
            blockers.Add(
                "COST_CALC còn receipt IS_APPLIED=1 — không REOPEN B4 khi giá kho đã áp (cần reverse B5 trước).");
        }

        if (blockers.Count == 0)
            return;

        throw new InvalidOperationException(
            BlockedReasonPrefix + " " + string.Join(" ", blockers) +
            " SupportsSafeReopenSideEffectReverse=false.");
    }
}

/// <summary>Kết quả probe DB — nguồn tin cậy trước persist REOPEN.</summary>
public sealed class ReopenSideEffectSnapshot
{
    public bool ExpTo154VoucherExists { get; init; }
    public bool PeriodClaimedExpTo154Only { get; init; }
    public bool ValuationCogsVoucherExists { get; init; }
    public int AppliedReceiptCount { get; init; }
    public bool InventoryPriceUpdated { get; init; }
    public bool HasCostCalcSnapshot { get; init; }

    public bool HasExternalSideEffects =>
        ExpTo154VoucherExists
        || PeriodClaimedExpTo154Only
        || ValuationCogsVoucherExists
        || AppliedReceiptCount > 0
        || InventoryPriceUpdated;
}

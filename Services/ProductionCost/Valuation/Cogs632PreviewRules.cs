using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Transfer;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Preview giá vốn 632 theo từng nhóm tồn — không gộp nhiều sản phẩm;
/// không cộng EXP; phân biệt đơn giá xuất vs tồn cuối.
/// </summary>
public static class Cogs632PreviewRules
{
    public static void AssertMovementsSafeForValuation(IEnumerable<ValuationMovementInput> movements)
    {
        foreach (var m in movements)
        {
            if (m.IsExpTo154Only)
            {
                throw new InvalidOperationException(
                    "Không đưa bút toán EXP_TO_154_ONLY (Bước 3) vào tính giá vốn 632 / bình quân xuất kho.");
            }

            if (Starts632(m.DebitAccountCd) && Starts155(m.CreditAccountCd)
                && !string.Equals(m.FlowType, "OUTPUT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Không dùng bút toán N632/C155 sẵn có (B-I / trùng) làm nguồn movements VALUATION.");
            }

            _ = ExpTo154Rules.IsForbiddenDownstreamLine(m.DebitAccountCd, m.CreditAccountCd);
        }
    }

    public static IReadOnlyList<Cogs632PreviewLine> BuildPreviewByGroup(
        IReadOnlyList<ValuationAverageResult> beforeGroups,
        IReadOnlyList<ValuationAverageResult> afterGroups)
    {
        if (beforeGroups.Any(g => !g.IsValid) || afterGroups.Any(g => !g.IsValid))
        {
            var err = beforeGroups.Concat(afterGroups).SelectMany(g => g.Errors).FirstOrDefault()
                      ?? "Kết quả bình quân nhóm không hợp lệ — không tạo Preview 632.";
            throw new InvalidOperationException(err);
        }

        var afterMap = afterGroups.ToDictionary(g => Key(g));
        var lines = new List<Cogs632PreviewLine>();

        foreach (var before in beforeGroups.OrderBy(g => g.ProductId).ThenBy(g => g.StoreId).ThenBy(g => g.ChitType))
        {
            if (!afterMap.TryGetValue(Key(before), out var after))
            {
                throw new InvalidOperationException(
                    $"Thiếu nhóm after cho PRODUCT={before.ProductId} STORE={before.StoreCd} CHIT={before.ChitType}.");
            }

            if (before.ProductId <= 0 || after.ProductId <= 0)
                throw new InvalidOperationException("Preview 632 yêu cầu ProductId nhóm > 0 — không dùng mặc định 0.");

            lines.Add(new Cogs632PreviewLine
            {
                ProductId = after.ProductId,
                ProductCd = after.ProductCd,
                StoreId = after.StoreId,
                StoreCd = after.StoreCd,
                ChitType = after.ChitType,
                CompanyCd = after.CompanyCd,
                OutputQuantity = after.OutputQuantity,
                OutputUnitCostBefore = before.OutputAverageUnitCost,
                OutputUnitCostAfter = after.OutputAverageUnitCost,
                EndingUnitCostBefore = before.EndingAverageUnitCost,
                EndingUnitCostAfter = after.EndingAverageUnitCost,
                CogsAmountBefore = before.OutputAmount,
                CogsAmountAfter = after.OutputAmount,
                CogsDelta = AllocationMath.RoundDigit(after.OutputAmount - before.OutputAmount, 0),
                DebitAccountHint = "632",
                CreditAccountHint = "155"
            });
        }

        return lines;
    }

    /// <summary>Kiểm tra chứng từ VALUATION_COGS đã tồn tại theo STABLE_KEY + COMPANY_CD.</summary>
    public static void AssertExistingVoucherOwnership(
        string companyCd,
        string periodYm,
        long? existingChitId,
        string? existingChitCompanyCd,
        string? existingStableKey)
    {
        if (existingChitId is null or <= 0)
            return;

        var expectedKey = ValuationApplyPlan.StableKey(companyCd, periodYm);
        if (!string.IsNullOrWhiteSpace(existingStableKey) &&
            !string.Equals(existingStableKey, expectedKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"STABLE_KEY chứng từ hiện có ({existingStableKey}) ≠ {expectedKey}.");
        }

        if (!string.IsNullOrWhiteSpace(existingChitCompanyCd) &&
            !string.Equals(existingChitCompanyCd, companyCd, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"VALUATION_COGS CHIT_ID={existingChitId} thuộc COMPANY_CD={existingChitCompanyCd}, không phải {companyCd}.");
        }
    }

    public static void AssertNoDuplicateCogsPost(bool alreadyHasValuationCogsVoucher, bool wouldInsertNew)
    {
        if (alreadyHasValuationCogsVoucher && wouldInsertNew)
        {
            throw new InvalidOperationException(
                "Đã có VALUATION_COGS — Confirm phải UPDATE theo STABLE_KEY+COMPANY_CD, không INSERT trùng N632/C155.");
        }
    }

    private static (string Company, long ProductId, long StoreId, string StoreCd, string ChitType) Key(
        ValuationAverageResult g) =>
        (g.CompanyCd ?? string.Empty, g.ProductId, g.StoreId, g.StoreCd ?? string.Empty, g.ChitType ?? string.Empty);

    private static bool Starts632(string? acc) =>
        !string.IsNullOrWhiteSpace(acc) && acc.TrimStart().StartsWith("632", StringComparison.Ordinal);

    private static bool Starts155(string? acc) =>
        !string.IsNullOrWhiteSpace(acc) && acc.TrimStart().StartsWith("155", StringComparison.Ordinal);
}

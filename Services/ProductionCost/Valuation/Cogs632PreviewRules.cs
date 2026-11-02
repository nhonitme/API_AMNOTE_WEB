using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Transfer;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Preview giá vốn 632 sau cập nhật giá nhập — không tạo trùng N632/C155 kiểu B-I;
/// không cộng EXP_TO_154_ONLY vào tính giá vốn thêm lần nữa.
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

            if (ExpTo154Rules.IsForbiddenDownstreamLine(m.DebitAccountCd, m.CreditAccountCd)
                && string.Equals(m.FlowType, "OUTPUT", StringComparison.OrdinalIgnoreCase) == false)
            {
                // Forbidden GL lines (N632/C155 as transfer from 154 path) must not appear as source movements
                // except legitimate stock OUTPUT valuation targets prepared separately.
                if (Starts632(m.DebitAccountCd) && Starts155(m.CreditAccountCd))
                {
                    throw new InvalidOperationException(
                        "Không dùng bút toán N632/C155 sẵn có (B-I / trùng) làm nguồn movements VALUATION.");
                }
            }
        }
    }

    public static IReadOnlyList<Cogs632PreviewLine> BuildPreview(
        ValuationAverageResult before,
        ValuationAverageResult after,
        long productId = 0,
        long departmentId = 0)
    {
        var cogsBefore = before.OutputAmount;
        var cogsAfter = after.OutputAmount;
        return
        [
            new Cogs632PreviewLine
            {
                ProductId = productId,
                DepartmentId = departmentId,
                OutputQuantity = after.OutputQuantity,
                UnitCostBefore = before.EndingAverageUnitCost,
                UnitCostAfter = after.EndingAverageUnitCost,
                CogsAmountBefore = cogsBefore,
                CogsAmountAfter = cogsAfter,
                CogsDelta = AllocationMath.RoundDigit(cogsAfter - cogsBefore, 0),
                DebitAccountHint = "632",
                CreditAccountHint = "155"
            }
        ];
    }

    /// <summary>Kế hoạch post 632 chỉ là hint Preview — không tạo CT trùng trong 2C-04.</summary>
    public static void AssertNoDuplicateCogsPost(bool alreadyHasValuationCogsVoucher, bool wouldInsertNew)
    {
        if (alreadyHasValuationCogsVoucher && wouldInsertNew)
        {
            throw new InvalidOperationException(
                "Đã có VALUATION_COGS — Confirm phải UPDATE theo STABLE_KEY, không INSERT trùng N632/C155.");
        }
    }

    private static bool Starts632(string? acc) =>
        !string.IsNullOrWhiteSpace(acc) && acc.TrimStart().StartsWith("632", StringComparison.Ordinal);

    private static bool Starts155(string? acc) =>
        !string.IsNullOrWhiteSpace(acc) && acc.TrimStart().StartsWith("155", StringComparison.Ordinal);
}

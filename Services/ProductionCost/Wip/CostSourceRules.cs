using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Tập hợp nguồn chi phí Bước 4 — không cộng trùng EXP_TO_154_ONLY / XFER.
/// </summary>
public static class CostSourceRules
{
    private static readonly HashSet<string> IncurredKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ProductionCostSourceKind.Src621,
        ProductionCostSourceKind.Src6212,
        ProductionCostSourceKind.Src622Alloc,
        ProductionCostSourceKind.Src627Alloc,
        ProductionCostSourceKind.Src154Direct
    };

    public static bool IsIncurredKind(string? sourceKind) =>
        !string.IsNullOrWhiteSpace(sourceKind) && IncurredKinds.Contains(sourceKind.Trim());

    public static bool IsTransferKind(string? sourceKind) =>
        !string.IsNullOrWhiteSpace(sourceKind)
        && (sourceKind.StartsWith("XFER_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceKind, ProductionCostSourceKind.XferExpTo154, StringComparison.OrdinalIgnoreCase));

    /// <summary>Loại bỏ XFER khỏi danh sách nguồn trước khi tính incurred.</summary>
    public static IReadOnlyList<CostSourceLine> FilterForIncurred(IEnumerable<CostSourceLine> sources) =>
        sources.Where(s => IsIncurredKind(s.SourceKind)).ToList();

    public static void AssertNoTransferInIncurred(IEnumerable<CostSourceLine> sources)
    {
        var bad = sources.FirstOrDefault(s => IsTransferKind(s.SourceKind));
        if (bad != null)
            throw new InvalidOperationException(
                $"Không được cộng {bad.SourceKind} (EXP_TO_154_ONLY) vào TOTAL_AMOUNT_INCURRED.");
    }

    public static decimal SumIncurred(IEnumerable<CostSourceLine> sources, int roundDigit = 0)
    {
        AssertNoTransferInIncurred(sources);
        var sum = FilterForIncurred(sources).Sum(s => s.Amount);
        return AllocationMath.RoundDigit(sum, roundDigit);
    }

    /// <summary>
    /// Gộp nguồn theo PRODUCT (+ optional DEPARTMENT) vào Amount621/6212/622/627/154Direct.
    /// </summary>
    public static WipCalcInput ApplySourcesToProduct(WipCalcInput product, IEnumerable<CostSourceLine> sources)
    {
        var lines = FilterForIncurred(sources)
            .Where(s => s.ProductId == product.ProductId
                        && (s.DepartmentId == 0 || s.DepartmentId == product.DepartmentId))
            .ToList();

        decimal Pick(string kind) => lines
            .Where(s => string.Equals(s.SourceKind, kind, StringComparison.OrdinalIgnoreCase))
            .Sum(s => s.Amount);

        return new WipCalcInput
        {
            ProductId = product.ProductId,
            ProductCd = product.ProductCd,
            DepartmentId = product.DepartmentId,
            DepartmentCd = product.DepartmentCd,
            IncompleteQtyOpening = product.IncompleteQtyOpening,
            BeforeAmountResult621 = product.BeforeAmountResult621,
            BeforeAmountResult62N = product.BeforeAmountResult62N,
            Amount621 = Pick(ProductionCostSourceKind.Src621),
            Amount6212 = Pick(ProductionCostSourceKind.Src6212),
            Amount622 = Pick(ProductionCostSourceKind.Src622Alloc),
            Amount627 = Pick(ProductionCostSourceKind.Src627Alloc),
            Amount154Direct = Pick(ProductionCostSourceKind.Src154Direct),
            Quantity = product.Quantity,
            PercentCompleted = product.PercentCompleted,
            ClosingIncompleteQty = product.ClosingIncompleteQty,
            ClosingAmountOverride = product.ClosingAmountOverride,
            ProductReduce = product.ProductReduce,
            PreviousClosing154 = product.PreviousClosing154
        };
    }
}

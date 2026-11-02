using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Tập hợp nguồn chi phí Bước 4 — không cộng trùng EXP/XFER; nguồn DEPT=0 chỉ dùng một lần.
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

    public sealed class SourceDistributeResult
    {
        public IReadOnlyList<WipCalcInput> Products { get; init; } = Array.Empty<WipCalcInput>();
        public decimal SourceTotalBefore { get; init; }
        public decimal AllocatedTotalAfter { get; init; }
    }

    public static bool IsIncurredKind(string? sourceKind) =>
        !string.IsNullOrWhiteSpace(sourceKind) && IncurredKinds.Contains(sourceKind.Trim());

    public static bool IsTransferKind(string? sourceKind) =>
        !string.IsNullOrWhiteSpace(sourceKind)
        && (sourceKind.StartsWith("XFER_", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceKind, ProductionCostSourceKind.XferExpTo154, StringComparison.OrdinalIgnoreCase));

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
    /// Phân bổ nguồn vào đối tượng: mỗi dòng nguồn dùng đúng một lần.
    /// DEPARTMENT_ID&gt;0 → đúng phòng; =0 → chia theo Quantity giữa đối tượng khớp PRODUCT (hoặc toàn bộ nếu PRODUCT_ID=0).
    /// </summary>
    public static SourceDistributeResult DistributeToProducts(
        IReadOnlyList<WipCalcInput> products,
        IEnumerable<CostSourceLine> sources)
    {
        if (products.Count == 0)
            throw new ArgumentException("Danh sách đối tượng tính GT không được rỗng.");

        AssertNoTransferInIncurred(sources);
        var incurred = FilterForIncurred(sources);
        var before = AllocationMath.RoundDigit(incurred.Sum(s => s.Amount), 0);

        var bags = products.Select(AmountBag.FromProduct).ToList();

        foreach (var src in incurred)
        {
            var targets = bags.Where(b => Matches(b, src)).ToList();
            if (targets.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Nguồn {src.SourceKind} PRODUCT={src.ProductId} DEPT={src.DepartmentId} " +
                    "không khớp đối tượng hợp lệ — không phân bổ.");
            }

            DistributeKind(targets, src.SourceKind, src.Amount);
        }

        var after = AllocationMath.RoundDigit(bags.Sum(b => b.TotalAmounts), 0);
        if (Math.Abs(before - after) > 1m)
        {
            throw new InvalidOperationException(
                $"Tổng nguồn sau phân bổ ({after}) lệch tổng trước ({before}) — kiểm tra làm tròn/chia sẻ.");
        }

        return new SourceDistributeResult
        {
            Products = bags.Select(b => b.ToInput()).ToList(),
            SourceTotalBefore = before,
            AllocatedTotalAfter = after
        };
    }

    /// <summary>Tương thích đơn dòng — vẫn đi qua phân bổ một lần (không nhân DEPT=0).</summary>
    public static WipCalcInput ApplySourcesToProduct(WipCalcInput product, IEnumerable<CostSourceLine> sources) =>
        DistributeToProducts(new[] { product }, sources).Products[0];

    private static bool Matches(AmountBag bag, CostSourceLine src)
    {
        if (src.ProductId > 0 && src.ProductId != bag.ProductId)
            return false;
        if (src.DepartmentId > 0 && src.DepartmentId != bag.DepartmentId)
            return false;
        return true;
    }

    private static void DistributeKind(IReadOnlyList<AmountBag> targets, string sourceKind, decimal amount)
    {
        if (targets.Count == 1)
        {
            targets[0].Add(sourceKind, amount);
            return;
        }

        var basisSum = targets.Sum(t => Math.Max(0m, t.Quantity));
        var shares = new decimal[targets.Count];
        decimal allocated = 0;

        for (var i = 0; i < targets.Count; i++)
        {
            if (i == targets.Count - 1)
            {
                shares[i] = AllocationMath.RoundDigit(amount - allocated, 0);
            }
            else if (basisSum > 0)
            {
                shares[i] = AllocationMath.RoundDigit(amount * targets[i].Quantity / basisSum, 0);
                allocated += shares[i];
            }
            else
            {
                shares[i] = AllocationMath.RoundDigit(amount / targets.Count, 0);
                allocated += shares[i];
            }
        }

        for (var i = 0; i < targets.Count; i++)
            targets[i].Add(sourceKind, shares[i]);
    }

    private sealed class AmountBag
    {
        public long ProductId { get; init; }
        public long DepartmentId { get; init; }
        public string? ProductCd { get; init; }
        public string? DepartmentCd { get; init; }
        public decimal IncompleteQtyOpening { get; init; }
        public decimal BeforeAmountResult621 { get; init; }
        public decimal BeforeAmountResult62N { get; init; }
        public decimal Quantity { get; init; }
        public decimal PercentCompleted { get; init; }
        public decimal ClosingIncompleteQty { get; init; }
        public decimal? ClosingAmountOverride { get; init; }
        public decimal ProductReduce { get; init; }
        public decimal PreviousClosing154 { get; init; }
        public decimal Amount621 { get; private set; }
        public decimal Amount6212 { get; private set; }
        public decimal Amount622 { get; private set; }
        public decimal Amount627 { get; private set; }
        public decimal Amount154Direct { get; private set; }

        public decimal TotalAmounts =>
            Amount621 + Amount6212 + Amount622 + Amount627 + Amount154Direct;

        public static AmountBag FromProduct(WipCalcInput p) => new()
        {
            ProductId = p.ProductId,
            DepartmentId = p.DepartmentId,
            ProductCd = p.ProductCd,
            DepartmentCd = p.DepartmentCd,
            IncompleteQtyOpening = p.IncompleteQtyOpening,
            BeforeAmountResult621 = p.BeforeAmountResult621,
            BeforeAmountResult62N = p.BeforeAmountResult62N,
            Quantity = p.Quantity,
            PercentCompleted = p.PercentCompleted,
            ClosingIncompleteQty = p.ClosingIncompleteQty,
            ClosingAmountOverride = p.ClosingAmountOverride,
            ProductReduce = p.ProductReduce,
            PreviousClosing154 = p.PreviousClosing154
            // Amount* bắt đầu 0 — chỉ nhận từ nguồn (tránh cộng trùng với sẵn có trên input)
        };

        public void Add(string kind, decimal amount)
        {
            if (string.Equals(kind, ProductionCostSourceKind.Src621, StringComparison.OrdinalIgnoreCase))
                Amount621 += amount;
            else if (string.Equals(kind, ProductionCostSourceKind.Src6212, StringComparison.OrdinalIgnoreCase))
                Amount6212 += amount;
            else if (string.Equals(kind, ProductionCostSourceKind.Src622Alloc, StringComparison.OrdinalIgnoreCase))
                Amount622 += amount;
            else if (string.Equals(kind, ProductionCostSourceKind.Src627Alloc, StringComparison.OrdinalIgnoreCase))
                Amount627 += amount;
            else if (string.Equals(kind, ProductionCostSourceKind.Src154Direct, StringComparison.OrdinalIgnoreCase))
                Amount154Direct += amount;
            else
                throw new InvalidOperationException($"SOURCE_KIND không hợp lệ để incurred: {kind}");
        }

        public WipCalcInput ToInput() => new()
        {
            ProductId = ProductId,
            ProductCd = ProductCd,
            DepartmentId = DepartmentId,
            DepartmentCd = DepartmentCd,
            IncompleteQtyOpening = IncompleteQtyOpening,
            BeforeAmountResult621 = BeforeAmountResult621,
            BeforeAmountResult62N = BeforeAmountResult62N,
            Amount621 = Amount621,
            Amount6212 = Amount6212,
            Amount622 = Amount622,
            Amount627 = Amount627,
            Amount154Direct = Amount154Direct,
            Quantity = Quantity,
            PercentCompleted = PercentCompleted,
            ClosingIncompleteQty = ClosingIncompleteQty,
            ClosingAmountOverride = ClosingAmountOverride,
            ProductReduce = ProductReduce,
            PreviousClosing154 = PreviousClosing154
        };
    }
}

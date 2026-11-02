using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Engine phân bổ 622/627 — thuần, không DB.
/// Round(P_A × T_i / ΣT, d); dòng cuối nhận phần dư để Σ = P_A.
/// </summary>
public static class AllocationEngine
{
    public static IReadOnlyList<AllocationPoolInput> ApplyDefaultReductions(
        IEnumerable<AllocationPoolInput> pool,
        string? companyCd = null)
    {
        return pool.Select(row =>
        {
            var reduction = AllocationMath.ComputeReduction(row.CreditAmount, row.DebitAmount154, companyCd);
            return ClonePool(row, reduction, AllocationMath.ComputeAllocationCosts(row.TotalAmount, reduction));
        }).ToList();
    }

    public static IReadOnlyList<AllocationPoolInput> RecalculateFromReduction(IEnumerable<AllocationPoolInput> pool) =>
        pool.Select(row => ClonePool(
            row,
            row.Reduction,
            AllocationMath.ComputeAllocationCosts(row.TotalAmount, row.Reduction))).ToList();

    public static AllocationComputeResult Run(
        IReadOnlyList<AllocationPoolInput> pool,
        IReadOnlyList<AllocationProductInput> products,
        AllocationEngineOptions options)
    {
        if (options.AllocateType is not (0 or 1))
            throw new ArgumentException("AllocateType must be 0 (NVL) or 1 (finished qty).");

        var normalizedPool = RecalculateFromReduction(pool);
        var details = options.ByDepartment
            ? RunByDepartment(normalizedPool, products, options)
            : AllocateGroup(normalizedPool, products, options.AllocateType, options.NumberDigit);

        return new AllocationComputeResult
        {
            Pool = normalizedPool,
            Details = details,
            Total622 = details.Where(d => d.AccountCd.StartsWith("622", StringComparison.Ordinal)).Sum(d => d.AllocatedAmount),
            Total627 = details.Where(d => d.AccountCd.StartsWith("627", StringComparison.Ordinal)).Sum(d => d.AllocatedAmount)
        };
    }

    private static IReadOnlyList<AllocationLineResult> RunByDepartment(
        IReadOnlyList<AllocationPoolInput> pool,
        IReadOnlyList<AllocationProductInput> products,
        AllocationEngineOptions options)
    {
        var outLines = new List<AllocationLineResult>();
        foreach (var deptGroup in products.GroupBy(p => p.DepartmentId))
        {
            var prodGroup = deptGroup.ToList();
            var deptId = deptGroup.Key;
            var poolGroup = pool.Where(r => r.DepartmentId == deptId).ToList();
            if (poolGroup.Count == 0)
                poolGroup = pool.Where(r => r.DepartmentId == 0).ToList();

            outLines.AddRange(AllocateGroup(poolGroup, prodGroup, options.AllocateType, options.NumberDigit));
        }

        // Giữ thứ tự sản phẩm gốc
        var lookup = outLines
            .GroupBy(x => (x.ProductId, x.AccountCd))
            .ToDictionary(g => g.Key, g => g.First());
        var ordered = new List<AllocationLineResult>();
        foreach (var p in products)
        {
            foreach (var acc in pool.Select(x => x.AccountCd).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (lookup.TryGetValue((p.ProductId, acc), out var line))
                    ordered.Add(line);
            }
        }

        return ordered.Count > 0 ? ordered : outLines;
    }

    private static IReadOnlyList<AllocationLineResult> AllocateGroup(
        IReadOnlyList<AllocationPoolInput> pool,
        IReadOnlyList<AllocationProductInput> products,
        int allocateType,
        int numberDigit)
    {
        if (products.Count == 0)
            return Array.Empty<AllocationLineResult>();

        var bases = products.Select(p => BasisOf(p, allocateType)).ToArray();
        var totalBasis = bases.Sum();
        var lines = new List<AllocationLineResult>();

        // Map product → allocated by account
        var allocated = products
            .Select(_ => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var lastFlags = products
            .Select(_ => new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase))
            .ToArray();

        foreach (var poolRow in pool)
        {
            var pA = poolRow.AllocationCosts;
            if (totalBasis == 0m)
            {
                for (var i = 0; i < products.Count; i++)
                {
                    allocated[i][poolRow.AccountCd] = 0m;
                    lastFlags[i][poolRow.AccountCd] = i == products.Count - 1;
                }
                continue;
            }

            decimal allocatedSum = 0m;
            for (var i = 0; i < products.Count; i++)
            {
                if (i == products.Count - 1)
                {
                    var remainder = AllocationMath.RoundDigit(pA - allocatedSum, numberDigit);
                    allocated[i][poolRow.AccountCd] = remainder;
                    lastFlags[i][poolRow.AccountCd] = true;
                }
                else
                {
                    var share = AllocationMath.RoundDigit(pA * bases[i] / totalBasis, numberDigit);
                    allocated[i][poolRow.AccountCd] = share;
                    lastFlags[i][poolRow.AccountCd] = false;
                    allocatedSum += share;
                }
            }
        }

        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            var map = allocated[i];
            var sum622 = AllocationMath.RoundDigit(
                map.Where(kv => kv.Key.StartsWith("622", StringComparison.Ordinal)).Sum(kv => kv.Value),
                numberDigit);
            var sum627 = AllocationMath.RoundDigit(
                map.Where(kv => kv.Key.StartsWith("627", StringComparison.Ordinal)).Sum(kv => kv.Value),
                numberDigit);

            foreach (var poolRow in pool)
            {
                map.TryGetValue(poolRow.AccountCd, out var amt);
                lastFlags[i].TryGetValue(poolRow.AccountCd, out var isLast);
                lines.Add(new AllocationLineResult
                {
                    ProductId = p.ProductId,
                    ProductCd = p.ProductCd,
                    DepartmentId = p.DepartmentId,
                    DepartmentCd = p.DepartmentCd,
                    AmountQuantity = p.AmountQuantity,
                    Quantity = p.Quantity,
                    Amount6211 = p.Amount6211,
                    Amount6212 = p.Amount6212,
                    AccountCd = poolRow.AccountCd,
                    AllocatedAmount = amt,
                    IsLastRow = isLast,
                    Amount622Sum = sum622,
                    Amount627Sum = sum627
                });
            }
        }

        return lines;
    }

    private static decimal BasisOf(AllocationProductInput row, int allocateType) =>
        allocateType == 0 ? row.AmountQuantity : row.Quantity;

    private static AllocationPoolInput ClonePool(AllocationPoolInput row, decimal reduction, decimal allocationCosts) =>
        new()
        {
            AccountCd = row.AccountCd,
            DepartmentId = row.DepartmentId,
            DepartmentCd = row.DepartmentCd,
            TotalAmount = row.TotalAmount,
            CreditAmount = row.CreditAmount,
            DebitAmount154 = row.DebitAmount154,
            Reduction = reduction,
            AllocationCosts = allocationCosts
        };
}

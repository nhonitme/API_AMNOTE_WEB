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
            : AllocateGroup(normalizedPool, products, options.AllocateType, options.NumberDigit, poolDepartmentId: null);

        AssertPoolTotalsMatch(normalizedPool, details, options.NumberDigit);

        return new AllocationComputeResult
        {
            Pool = normalizedPool,
            Details = details,
            Total622 = details.Where(d => d.AccountCd.StartsWith("622", StringComparison.Ordinal)).Sum(d => d.AllocatedAmount),
            Total627 = details.Where(d => d.AccountCd.StartsWith("627", StringComparison.Ordinal)).Sum(d => d.AllocatedAmount)
        };
    }

    /// <summary>
    /// byDepartment:
    /// - Pool DEPARTMENT_ID &gt; 0: phân bổ một lần trong đúng phòng ban đó.
    /// - Pool DEPARTMENT_ID = 0 (dùng chung): phân bổ đúng một lần trên toàn bộ đối tượng.
    /// Không fallback shared-pool theo từng phòng (tránh nhân nhiều lần P_A).
    /// </summary>
    private static IReadOnlyList<AllocationLineResult> RunByDepartment(
        IReadOnlyList<AllocationPoolInput> pool,
        IReadOnlyList<AllocationProductInput> products,
        AllocationEngineOptions options)
    {
        var outLines = new List<AllocationLineResult>();

        var deptPools = pool.Where(r => r.DepartmentId > 0).ToList();
        var sharedPools = pool.Where(r => r.DepartmentId == 0).ToList();

        foreach (var deptPoolGroup in deptPools.GroupBy(r => r.DepartmentId))
        {
            var deptId = deptPoolGroup.Key;
            var prodGroup = products.Where(p => p.DepartmentId == deptId).ToList();
            if (prodGroup.Count == 0)
                throw new InvalidOperationException(
                    $"Pool DEPARTMENT_ID={deptId} không có đối tượng cùng phòng ban để phân bổ.");

            outLines.AddRange(AllocateGroup(
                deptPoolGroup.ToList(),
                prodGroup,
                options.AllocateType,
                options.NumberDigit,
                poolDepartmentId: deptId));
        }

        if (sharedPools.Count > 0)
        {
            if (products.Count == 0)
                throw new InvalidOperationException("Pool dùng chung (DEPARTMENT_ID=0) cần ít nhất một đối tượng.");

            // Đúng một lần — không lặp theo từng phòng ban.
            outLines.AddRange(AllocateGroup(
                sharedPools,
                products.ToList(),
                options.AllocateType,
                options.NumberDigit,
                poolDepartmentId: 0));
        }

        // Giữ thứ tự sản phẩm gốc; khóa (ProductId, DepartmentId, AccountCd, PoolDepartmentId)
        // để không mất dòng khi cùng PRODUCT+ACCOUNT ở nhiều DEPARTMENT.
        var seen = new HashSet<(long ProductId, long DepartmentId, string AccountCd, long PoolDepartmentId)>();
        var ordered = new List<AllocationLineResult>();
        foreach (var p in products)
        {
            foreach (var line in outLines.Where(l => l.ProductId == p.ProductId && l.DepartmentId == p.DepartmentId))
            {
                var key = (line.ProductId, line.DepartmentId, line.AccountCd, line.PoolDepartmentId);
                if (seen.Add(key))
                    ordered.Add(line);
            }
        }

        // Dòng còn lại (nếu có) — không drop
        foreach (var line in outLines)
        {
            var key = (line.ProductId, line.DepartmentId, line.AccountCd, line.PoolDepartmentId);
            if (seen.Add(key))
                ordered.Add(line);
        }

        return ordered;
    }

    private static IReadOnlyList<AllocationLineResult> AllocateGroup(
        IReadOnlyList<AllocationPoolInput> pool,
        IReadOnlyList<AllocationProductInput> products,
        int allocateType,
        int numberDigit,
        long? poolDepartmentId)
    {
        if (products.Count == 0)
            return Array.Empty<AllocationLineResult>();

        var bases = products.Select(p => BasisOf(p, allocateType)).ToArray();
        var totalBasis = bases.Sum();

        // ΣT = 0 nhưng còn chi phí cần phân bổ → lỗi rõ, không âm thầm = 0.
        if (totalBasis == 0m && pool.Any(p => p.AllocationCosts != 0m))
        {
            throw new InvalidOperationException(
                "Tổng tiêu thức phân bổ = 0 trong khi ALLOCATION_COSTS > 0 — không thể phân bổ. " +
                "Kiểm tra basis NVL/SL hoặc đối tượng được chọn.");
        }

        var allocated = products
            .Select(_ => new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var lastFlags = products
            .Select(_ => new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase))
            .ToArray();

        foreach (var poolRow in pool)
        {
            var pA = poolRow.AllocationCosts;
            var key = PoolLineKey(poolRow);
            if (totalBasis == 0m)
            {
                for (var i = 0; i < products.Count; i++)
                {
                    allocated[i][key] = 0m;
                    lastFlags[i][key] = i == products.Count - 1;
                }
                continue;
            }

            decimal allocatedSum = 0m;
            for (var i = 0; i < products.Count; i++)
            {
                if (i == products.Count - 1)
                {
                    var remainder = AllocationMath.RoundDigit(pA - allocatedSum, numberDigit);
                    allocated[i][key] = remainder;
                    lastFlags[i][key] = true;
                }
                else
                {
                    var share = AllocationMath.RoundDigit(pA * bases[i] / totalBasis, numberDigit);
                    allocated[i][key] = share;
                    lastFlags[i][key] = false;
                    allocatedSum += share;
                }
            }
        }

        var lines = new List<AllocationLineResult>();
        for (var i = 0; i < products.Count; i++)
        {
            var p = products[i];
            var map = allocated[i];
            var sum622 = AllocationMath.RoundDigit(
                map.Where(kv => AccountOfKey(kv.Key).StartsWith("622", StringComparison.Ordinal)).Sum(kv => kv.Value),
                numberDigit);
            var sum627 = AllocationMath.RoundDigit(
                map.Where(kv => AccountOfKey(kv.Key).StartsWith("627", StringComparison.Ordinal)).Sum(kv => kv.Value),
                numberDigit);

            foreach (var poolRow in pool)
            {
                var key = PoolLineKey(poolRow);
                map.TryGetValue(key, out var amt);
                lastFlags[i].TryGetValue(key, out var isLast);
                lines.Add(new AllocationLineResult
                {
                    ProductId = p.ProductId,
                    ProductCd = p.ProductCd,
                    DepartmentId = p.DepartmentId,
                    DepartmentCd = p.DepartmentCd,
                    PoolDepartmentId = poolDepartmentId ?? poolRow.DepartmentId,
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

    /// <summary>Mỗi dòng pool: Σ ALLOCATED_AMOUNT (sau làm tròn) = ALLOCATION_COSTS.</summary>
    public static void AssertPoolTotalsMatch(
        IReadOnlyList<AllocationPoolInput> pool,
        IReadOnlyList<AllocationLineResult> details,
        int numberDigit)
    {
        foreach (var poolRow in pool)
        {
            var sum = details
                .Where(d =>
                    string.Equals(d.AccountCd, poolRow.AccountCd, StringComparison.OrdinalIgnoreCase)
                    && d.PoolDepartmentId == poolRow.DepartmentId)
                .Sum(d => d.AllocatedAmount);
            var rounded = AllocationMath.RoundDigit(sum, numberDigit);
            var expected = AllocationMath.RoundDigit(poolRow.AllocationCosts, numberDigit);
            if (rounded != expected)
            {
                throw new InvalidOperationException(
                    $"Tổng phân bổ TK {poolRow.AccountCd} (pool DEPT={poolRow.DepartmentId}) = {rounded} " +
                    $"không khớp ALLOCATION_COSTS={expected}.");
            }
        }
    }

    private static string PoolLineKey(AllocationPoolInput row) =>
        $"{row.AccountCd}#{row.DepartmentId}";

    private static string AccountOfKey(string key)
    {
        var idx = key.IndexOf('#');
        return idx < 0 ? key : key[..idx];
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

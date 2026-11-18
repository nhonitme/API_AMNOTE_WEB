using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

public static class PoolAccountRules
{
    public static bool IsAllocatableAccount(string accountCd, bool include6212)
    {
        if (string.IsNullOrWhiteSpace(accountCd))
            return false;

        var acc = accountCd.Trim();
        if (acc.StartsWith("622", StringComparison.Ordinal) || acc.StartsWith("627", StringComparison.Ordinal))
            return true;

        if (include6212 && acc.StartsWith("6212", StringComparison.Ordinal))
            return true;

        return false;
    }

    public static IReadOnlyList<AllocationPoolInput> AggregatePool(
        IEnumerable<(string AccountCd, long DepartmentId, string? DepartmentCd, long ChitdetailId, decimal Amount, decimal CreditAmount, decimal DebitAmount154)> rows,
        bool include6212,
        bool byDepartment)
    {
        var distinct = rows
            .Where(r => IsAllocatableAccount(r.AccountCd, include6212))
            .GroupBy(r => r.ChitdetailId)
            .Select(g => g.First())
            .ToList();

        var grouped = distinct
            .GroupBy(r => new
            {
                AccountCd = r.AccountCd.Trim(),
                DepartmentId = byDepartment ? r.DepartmentId : 0L
            })
            .Select(g =>
            {
                var total = g.Sum(x => x.Amount);
                var credit = g.Sum(x => x.CreditAmount);
                var debit154 = g.Sum(x => x.DebitAmount154);
                var reduction = AllocationMath.ComputeReduction(credit, debit154);
                return new AllocationPoolInput
                {
                    AccountCd = g.Key.AccountCd,
                    DepartmentId = g.Key.DepartmentId,
                    DepartmentCd = byDepartment ? g.First().DepartmentCd : "(ALL)",
                    TotalAmount = total,
                    CreditAmount = credit,
                    DebitAmount154 = debit154,
                    Reduction = reduction,
                    AllocationCosts = AllocationMath.ComputeAllocationCosts(total, reduction)
                };
            })
            .OrderBy(x => x.AccountCd)
            .ThenBy(x => x.DepartmentId)
            .ToList();

        return grouped;
    }
}

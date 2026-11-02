using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Quy tắc chọn tài khoản vào pool 622/627 (+6212 tùy chọn) — chống cộng trùng 6212.
/// </summary>
public static class PoolAccountRules
{
    public static bool IsAllocatableAccount(string accountCd, bool include6212)
    {
        if (string.IsNullOrWhiteSpace(accountCd))
            return false;

        var acc = accountCd.Trim();
        if (acc.StartsWith("622", StringComparison.Ordinal) || acc.StartsWith("627", StringComparison.Ordinal))
            return true;

        // 6212 chỉ vào pool khi bật cờ — không cộng trùng với tiêu thức NVL 621.
        if (include6212 && acc.StartsWith("6212", StringComparison.Ordinal))
            return true;

        return false;
    }

    /// <summary>
    /// Gộp phát sinh pool theo ACCOUNT_CD (+ DEPARTMENT_ID). Không nhân đôi cùng CHITDETAIL_ID.
    /// </summary>
    public static IReadOnlyList<AllocationPoolInput> AggregatePool(
        IEnumerable<(string AccountCd, long DepartmentId, string? DepartmentCd, long ChitdetailId, decimal Amount, decimal CreditAmount, decimal DebitAmount154)> rows,
        bool include6212,
        bool byDepartment,
        string? companyCd = null)
    {
        // Một CHITDETAIL_ID chỉ cộng 1 lần vào pool (tránh nhân đôi nếu join nhiều OUTPUT).
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
                var reduction = AllocationMath.ComputeReduction(credit, debit154, companyCd);
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

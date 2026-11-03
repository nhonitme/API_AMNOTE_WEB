using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Allocation;

/// <summary>
/// Chứng minh payload B2 vừa được load từ DB — không đọc cờ do client gửi.
/// </summary>
public static class AllocationSourceProvenance
{
    public static bool IsAuthoritative(AllocationDryRunPayload? source)
    {
        if (source == null || source.Pool.Count == 0 || source.Products.Count == 0)
            return false;
        if (source.AllocateType is not (0 or 1))
            return false;
        if (source.Products.Any(p => p.ProductId <= 0))
            return false;

        foreach (var row in source.Pool)
        {
            if (!PoolAccountRules.IsAllocatableAccount(row.AccountCd, source.Include6212))
                return false;
        }

        return true;
    }
}

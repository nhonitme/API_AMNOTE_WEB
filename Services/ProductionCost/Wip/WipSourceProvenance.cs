using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Chứng minh payload B4 vừa được load từ DB — không đọc cờ do client gửi.
/// </summary>
public static class WipSourceProvenance
{
    public static bool IsAuthoritative(WipDryRunPayload? source)
    {
        if (source == null || source.Products.Count == 0)
            return false;
        if (source.Products.Any(p => p.ProductId <= 0))
            return false;

        var incurred = source.Sources.Where(s => CostSourceRules.IsIncurredKind(s.SourceKind)).ToList();
        if (incurred.Count == 0)
            return false;
        if (source.Sources.Any(s => CostSourceRules.IsTransferKind(s.SourceKind)))
            return false;

        return true;
    }
}

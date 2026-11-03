using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Chứng minh payload B5 vừa được load từ DB — không đọc cờ do client gửi.
/// </summary>
public static class ValuationSourceProvenance
{
    public static bool IsAuthoritative(ValuationDryRunPayload? source)
    {
        if (source == null)
            return false;
        if (source.ConfirmedWip.Count == 0 || source.Receipts.Count == 0 || source.Movements.Count == 0)
            return false;
        if (source.Receipts.Any(r => r.InputId <= 0 || r.ProductId <= 0 || r.Qty <= 0))
            return false;
        if (source.Movements.Any(m => m.IsExpTo154Only))
            return false;
        if (string.IsNullOrWhiteSpace(source.ValuationMethod))
            return false;
        return true;
    }
}

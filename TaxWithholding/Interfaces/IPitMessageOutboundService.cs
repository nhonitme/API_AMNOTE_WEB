using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitMessageOutboundService
{
    Task DispatchAsync(string dbName, string companyCd);
}

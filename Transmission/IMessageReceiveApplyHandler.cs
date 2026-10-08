using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Transmission;

public interface IMessageReceiveApplyHandler
{
    bool CanHandle(string targetType);

    Task ApplyAsync(
        EInvoiceMessageWorkQueueItem item,
        IReadOnlyList<ViettelEInvoiceMessageLookupResult> received,
        bool preferSuccessResult);
}

using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IViettelEInvoiceGatewayClient
    {
        Task<IReadOnlyList<ViettelEInvoiceMessageLookupResult>> LookupMessagesAsync(
            string mst,
            string mtdiep,
            CancellationToken cancellationToken = default);

        Task<ViettelEInvoiceSendResult> SendDvgpRequestAsync(
            string requestXml,
            CancellationToken cancellationToken = default);
    }
}

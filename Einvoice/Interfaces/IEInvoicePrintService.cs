using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoicePrintService
    {
        Task<byte[]> ExportPdfAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<byte[]> ExportPdfFromSignedXmlAsync(
            string companyCd,
            long invoiceId,
            CancellationToken cancellationToken = default);

    }
}

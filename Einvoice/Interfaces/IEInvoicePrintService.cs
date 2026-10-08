using API_AMNOTE_WEB.Models;
using DevExpress.XtraReports.UI;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoicePrintService
    {
        Task<string> GetHtmlAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<XtraReport> BuildReportAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<byte[]> ExportPdfAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<byte[]> ExportPdfFromSignedXmlAsync(
            string companyCd,
            long invoiceId,
            CancellationToken cancellationToken = default);

        Task<byte[]> ExportPdfFromSignedXmlContentAsync(
            string companyCd,
            long invoiceId,
            string signedXml,
            CancellationToken cancellationToken = default);
    }
}

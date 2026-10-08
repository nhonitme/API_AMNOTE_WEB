using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceSellerPreviewService
    {
        Task<EInvoiceSellerPreviewDto> GetPreviewAsync(string companyCd, long sellerId, long? xslId = null);

        Task<string> GetHtmlAsync(string companyCd, long sellerId, long? xslId = null, CancellationToken cancellationToken = default);

        Task<string> GetDecimalDemoHtmlAsync(
            string companyCd,
            long sellerId,
            EInvoiceSellerDecimalPreviewRequest request,
            CancellationToken cancellationToken = default);

        Task<string> GetDesignerHtmlAsync(string companyCd, long sellerId, long? xslId = null, long? designId = null, CancellationToken cancellationToken = default);

        Task<string> GetDesignerLiveHtmlAsync(
            string companyCd,
            long sellerId,
            string xslContent,
            long? xslId = null,
            long? designId = null,
            IReadOnlyDictionary<string, string>? extraParameters = null,
            CancellationToken cancellationToken = default);
        Task<byte[]> ExportPdfAsync(string companyCd, long sellerId, long? xslId = null, CancellationToken cancellationToken = default);

        Task<byte[]> ExportDesignerPdfAsync(
            string companyCd,
            long sellerId,
            long? xslId = null,
            long? designId = null,
            string? xslContent = null,
            IReadOnlyDictionary<string, string>? extraParameters = null,
            CancellationToken cancellationToken = default);
    }
}

using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceXmlStorageService
    {
        Task<string> UploadInvoiceXmlAsync(
            string companyCd,
            long invoiceId,
            string xml,
            DateTime? invoiceDate = null,
            CancellationToken cancellationToken = default);

        Task<string> DownloadAsync(string xmlFtpPath, CancellationToken cancellationToken = default);

        Task<string?> TryDownloadAsync(string xmlFtpPath, CancellationToken cancellationToken = default);

        Task<string?> ResolveSignedInvoiceXmlAsync(
            string companyCd,
            EInvoiceInfo invoice,
            CancellationToken cancellationToken = default);

        IReadOnlyList<string> BuildCandidatePaths(string companyCd, EInvoiceInfo invoice);

        bool IsRemotePath(string? path);
    }
}

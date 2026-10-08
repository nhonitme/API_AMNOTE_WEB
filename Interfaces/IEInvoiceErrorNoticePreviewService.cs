namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceErrorNoticePreviewService
    {
        Task<string> GetHtmlAsync(string companyCd, long tbaoId, CancellationToken cancellationToken = default);

        Task<byte[]> ExportPdfAsync(string companyCd, long tbaoId, CancellationToken cancellationToken = default);
    }
}

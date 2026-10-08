namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceMinutePreviewService
    {
        Task<string> GetHtmlAsync(string companyCd, long bbanId, CancellationToken cancellationToken = default);

        Task<byte[]> ExportPdfAsync(string companyCd, long bbanId, CancellationToken cancellationToken = default);
    }
}

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceDeclarationPreviewService
    {
        Task<string> GetHtmlAsync(string companyCd, long tkhaiId, CancellationToken cancellationToken = default);

        Task<byte[]> ExportPdfAsync(string companyCd, long tkhaiId, CancellationToken cancellationToken = default);
    }
}

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceDeclarationPreviewService
    {

        Task<byte[]> ExportPdfAsync(string companyCd, long tkhaiId, CancellationToken cancellationToken = default);
    }
}

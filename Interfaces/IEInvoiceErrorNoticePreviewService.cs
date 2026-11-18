namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceErrorNoticePreviewService
    {

        Task<byte[]> ExportPdfAsync(string companyCd, long tbaoId, CancellationToken cancellationToken = default);
    }
}

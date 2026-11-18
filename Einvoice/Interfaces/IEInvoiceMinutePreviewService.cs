namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceMinutePreviewService
    {

        Task<byte[]> ExportPdfAsync(string companyCd, long bbanId, CancellationToken cancellationToken = default);
    }
}

namespace API_AMNOTE_WEB.Interfaces;

public interface IEInvoiceTransmissionPreviewService
{
    Task<byte[]> ExportPdfAsync(string companyCd, long receiveId, CancellationToken cancellationToken = default);
}

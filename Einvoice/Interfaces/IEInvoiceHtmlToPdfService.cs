namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceHtmlToPdfService
    {
        Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default);
    }
}

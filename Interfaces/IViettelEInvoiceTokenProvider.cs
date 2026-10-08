namespace API_AMNOTE_WEB.Interfaces
{
    public interface IViettelEInvoiceTokenProvider
    {
        bool IsConfigured { get; }

        Task<string?> TryGetAccessTokenAsync(CancellationToken cancellationToken = default);

        Task InvalidateAsync(CancellationToken cancellationToken = default);
    }
}

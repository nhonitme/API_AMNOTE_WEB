using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceEmailHistoryRepository
    {
        Task<IReadOnlyList<EInvoiceEmailHistoryInfo>> GetInvoiceHistoryAsync(string companyCd, long invoiceId, int limit = 200);

        Task InsertAsync(EInvoiceEmailHistoryCreateRequest request);
    }
}

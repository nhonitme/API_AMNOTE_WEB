using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceSellerRepository
    {
        Task<IEnumerable<EInvoiceSellerInfo>> GetSellersAsync(
            string companyCd,
            string? khhdon = null,
            long? sellerId = null,
            long? xslId = null,
            string? keyword = null,
            bool includeInactive = false,
            bool includeXslContent = false,
            bool includeAllTemplates = false);

        Task<EInvoiceSellerInfo?> GetSellerWithXslAsync(
            string companyCd,
            long? sellerId = null,
            long? xslId = null,
            string? khhdon = null,
            bool includeInactive = false);

        Task<EInvoiceSellerInfo?> UpdateSellerInfoAsync(
            string companyCd,
            string userId,
            long sellerId,
            EInvoiceSellerInfoSaveRequest request);

        Task ClearSellersCacheAsync(string companyCd);
    }
}

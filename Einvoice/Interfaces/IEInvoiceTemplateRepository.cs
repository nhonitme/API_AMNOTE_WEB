using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceTemplateRepository
    {
        Task<EInvoiceTemplate?> GetActiveTemplateAsync(string templateType, string templateCd);
    }
}

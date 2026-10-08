using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceSellerXslTemplateRepository
    {
        Task<IReadOnlyList<EInvoiceTemplateDesignerDesign>> GetDesignsAsync(string companyCd, long xslId);

        Task<EInvoiceTemplateDesignerDesign?> GetDesignAsync(string companyCd, long xslId, long designId);

        Task<EInvoiceTemplateDesignerDesign> CloneDesignAsync(string companyCd, string userId, long xslId, long sourceDesignId);

        Task<EInvoiceTemplateDesignerDraft> SaveDraftAsync(
            string companyCd,
            string userId,
            long xslId,
            long designId,
            string xslContent,
            string? logoPath,
            string? invoiceBackgroundPath,
            string? invoiceBorderPath,
            string? backgroundPath);

        Task<EInvoiceSellerXslTemplateContentResult> PublishDesignAsync(string companyCd, string userId, long xslId, long designId);

        Task SetDesignImageAsync(string companyCd, string userId, long xslId, long designId, string imageKind, string path);

        Task<EInvoiceSellerXslTemplateMetaResult> SaveMetaAsync(
            string companyCd,
            string userId,
            EInvoiceSellerXslTemplateMetaSaveRequest request,
            string? templateNm,
            string? thdon,
            string khmsHdon,
            string khhdon,
            string? xslContent);

        Task<long> DeleteAsync(string companyCd, string userId, long xslId);
    }
}

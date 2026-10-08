using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceSellerXslTemplateRepository : IEInvoiceSellerXslTemplateRepository
    {
        private readonly DapperExecutor _db;

        public EInvoiceSellerXslTemplateRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<EInvoiceTemplateDesignerDesign>> GetDesignsAsync(string companyCd, long xslId)
        {
            const string sql = "CALL getEInvoiceSellerXslTemplateDesigns(@p_COMPANY_CD, @p_XSL_ID);";
            var rows = await _db.QueryAsync<EInvoiceTemplateDesignerDesign>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_XSL_ID = xslId
            });
            return rows.ToList();
        }

        public async Task<EInvoiceTemplateDesignerDesign?> GetDesignAsync(string companyCd, long xslId, long designId)
        {
            const string sql = "CALL getEInvoiceSellerXslTemplateDesign(@p_COMPANY_CD, @p_XSL_ID, @p_DESIGN_ID);";
            var rows = await _db.QueryAsync<EInvoiceTemplateDesignerDesign>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_XSL_ID = xslId,
                p_DESIGN_ID = designId
            });
            return rows.FirstOrDefault();
        }

        public Task<EInvoiceTemplateDesignerDesign> CloneDesignAsync(string companyCd, string userId, long xslId, long sourceDesignId)
        {
            const string sql = "CALL cloneEInvoiceSellerXslTemplateDesign(@p_CONTENT_ID, @p_SOURCE_DESIGN_ID, @p_COMPANY_CD, @p_USERID);";
            return _db.QueryFirstAsync<EInvoiceTemplateDesignerDesign>(Net_DB.Net_DB_Company, sql, new
            {
                p_CONTENT_ID = xslId,
                p_SOURCE_DESIGN_ID = sourceDesignId,
                p_COMPANY_CD = companyCd,
                p_USERID = userId
            });
        }

        public Task<EInvoiceTemplateDesignerDraft> SaveDraftAsync(
            string companyCd,
            string userId,
            long xslId,
            long designId,
            string xslContent,
            string? logoPath,
            string? invoiceBackgroundPath,
            string? invoiceBorderPath,
            string? backgroundPath)
        {
            const string sql = "CALL setEInvoiceSellerXslTemplateDraft(@p_CONTENT_ID, @p_DESIGN_ID, @p_COMPANY_CD, @p_XSL_CONTENT, @p_LOGO_PATH, @p_INVOICE_BACKGROUND_PATH, @p_INVOICE_BORDER_PATH, @p_BACKGROUND_PATH, @p_USERID);";
            return _db.QueryFirstAsync<EInvoiceTemplateDesignerDraft>(Net_DB.Net_DB_Company, sql, new
            {
                p_CONTENT_ID = xslId,
                p_DESIGN_ID = designId,
                p_COMPANY_CD = companyCd,
                p_XSL_CONTENT = xslContent,
                p_LOGO_PATH = logoPath,
                p_INVOICE_BACKGROUND_PATH = invoiceBackgroundPath,
                p_INVOICE_BORDER_PATH = invoiceBorderPath,
                p_BACKGROUND_PATH = backgroundPath,
                p_USERID = userId
            });
        }

        public Task<EInvoiceSellerXslTemplateContentResult> PublishDesignAsync(string companyCd, string userId, long xslId, long designId)
        {
            const string sql = "CALL publishEInvoiceSellerXslTemplateDesign(@p_CONTENT_ID, @p_DESIGN_ID, @p_COMPANY_CD, @p_USERID);";
            return _db.QueryFirstAsync<EInvoiceSellerXslTemplateContentResult>(Net_DB.Net_DB_Company, sql, new
            {
                p_CONTENT_ID = xslId,
                p_DESIGN_ID = designId,
                p_COMPANY_CD = companyCd,
                p_USERID = userId
            });
        }

        public async Task SetDesignImageAsync(string companyCd, string userId, long xslId, long designId, string imageKind, string path)
        {
            const string sql = "CALL setEInvoiceSellerXslTemplateDesignImage(@p_COMPANY_CD, @p_XSL_ID, @p_DESIGN_ID, @p_IMAGE_KIND, @p_PATH, @p_USERID);";
            await _db.QueryFirstAsync<EInvoiceTemplateDesignerDesign>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_XSL_ID = xslId,
                p_DESIGN_ID = designId,
                p_IMAGE_KIND = imageKind,
                p_PATH = path,
                p_USERID = userId
            });
        }

        public Task<EInvoiceSellerXslTemplateMetaResult> SaveMetaAsync(
            string companyCd,
            string userId,
            EInvoiceSellerXslTemplateMetaSaveRequest request,
            string? templateNm,
            string? thdon,
            string khmsHdon,
            string khhdon,
            string? xslContent)
        {
            const string sql = """
                CALL setEInvoiceSellerXslTemplateMeta(
                  @p_XSL_ID, @p_COMPANY_CD, @p_TEMPLATE_NM, @p_THDON,
                  @p_KHMSHDON, @p_KHHDON, @p_FROM_SHDON, @p_TO_SHDON,
                  @p_USE_MULTI_TAX_RATE, @p_XSL_CONTENT, @p_USERID);
                """;
            return _db.QueryFirstAsync<EInvoiceSellerXslTemplateMetaResult>(Net_DB.Net_DB_Company, sql, new
            {
                p_XSL_ID = request.XSL_ID,
                p_COMPANY_CD = companyCd,
                p_TEMPLATE_NM = templateNm,
                p_THDON = thdon,
                p_KHMSHDON = khmsHdon,
                p_KHHDON = khhdon,
                p_FROM_SHDON = Common.NormalizeNullableText(request.FROM_SHDON) ?? "1",
                p_TO_SHDON = Common.NormalizeNullableText(request.TO_SHDON),
                p_USE_MULTI_TAX_RATE = request.USE_MULTI_TAX_RATE == 1 ? 1 : 0,
                p_XSL_CONTENT = xslContent,
                p_USERID = userId,
            });
        }

        public Task<long> DeleteAsync(string companyCd, string userId, long xslId)
        {
            const string sql = "CALL delEInvoiceSellerXslTemplate(@p_XSL_ID, @p_COMPANY_CD, @p_USERID);";
            return _db.QueryFirstAsync<long>(Net_DB.Net_DB_Company, sql, new
            {
                p_XSL_ID = xslId,
                p_COMPANY_CD = companyCd,
                p_USERID = userId,
            });
        }
    }
}

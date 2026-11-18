using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceTemplateRepository : IEInvoiceTemplateRepository
    {
        private const string GetTemplateQuery = "CALL getEInvoiceTemplate(@p_TEMPLATE_TYPE, @p_TEMPLATE_CD)";

        private readonly API_AMNOTE_WEB.Services.ElectronicDocumentRenderContext _renderContext;
        private readonly DapperExecutor _db;
        private readonly API_AMNOTE_WEB.Services.ElectronicDocumentStorage _files;

        public EInvoiceTemplateRepository(DapperExecutor db, API_AMNOTE_WEB.Services.ElectronicDocumentStorage files, API_AMNOTE_WEB.Services.ElectronicDocumentRenderContext renderContext)
        {
            _db = db;
            _files = files;
            _renderContext = renderContext;
        }

        public async Task<EInvoiceTemplate?> GetActiveTemplateAsync(string templateType, string templateCd)
        {
            if (templateType == "XSL" && _renderContext.Template is { } snapshot)
                return new EInvoiceTemplate { TEMPLATE_TYPE = "XSL", TEMPLATE_CD = templateCd,
                    XSL_FTP_PATH = snapshot.XslPath, CONTENT = _files.ReadXsl(snapshot.XslPath), IS_ACTIVE = 1 };
            var rows = await _db.QueryAsync<EInvoiceTemplate>(
                Net_DB.Net_DB_Manager,
                GetTemplateQuery,
                new
                {
                    p_TEMPLATE_TYPE = templateType,
                    p_TEMPLATE_CD = templateCd,
                });

            var row = rows.FirstOrDefault();
            if (row?.TEMPLATE_TYPE == "XSL") row.CONTENT = _files.ReadXsl(row.XSL_FTP_PATH);
            return row;
        }
    }
}

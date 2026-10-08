using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceTemplateRepository : IEInvoiceTemplateRepository
    {
        private const string GetTemplateQuery = "CALL getEInvoiceTemplate(@p_TEMPLATE_TYPE, @p_TEMPLATE_CD)";

        private readonly DapperExecutor _db;

        public EInvoiceTemplateRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<EInvoiceTemplate?> GetActiveTemplateAsync(string templateType, string templateCd)
        {
            var rows = await _db.QueryAsync<EInvoiceTemplate>(
                Net_DB.Net_DB_Manager,
                GetTemplateQuery,
                new
                {
                    p_TEMPLATE_TYPE = templateType,
                    p_TEMPLATE_CD = templateCd,
                });

            return rows.FirstOrDefault();
        }
    }
}

using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceEmailHistoryRepository : IEInvoiceEmailHistoryRepository
    {
        private const string GetInvoiceHistoryQuery = "CALL getEInvoiceEmailSendHistory(@p_COMPANY_CD, @p_REF_TYPE, @p_REF_ID, @p_LIMIT)";

        private const string InsertQuery = "CALL setEInvoiceEmailSendHistory(@p_COMPANY_CD, @p_DB_NAME, @p_REF_TYPE, @p_REF_ID, @p_SEND_TYPE, @p_SEND_STATUS, @p_FROM_EMAIL, @p_TO_EMAIL, @p_CC_EMAIL, @p_BCC_EMAIL, @p_MAIL_SUBJECT, @p_MAIL_BODY, @p_ATTACHMENT_INFO, @p_RETRY_COUNT, @p_ERROR_MESSAGE, @p_SEND_DT, @p_CREATE_BY);";

        private readonly DapperExecutor _db;

        public EInvoiceEmailHistoryRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<EInvoiceEmailHistoryInfo>> GetInvoiceHistoryAsync(string companyCd, long invoiceId, int limit = 200)
        {
            if (string.IsNullOrWhiteSpace(companyCd) || invoiceId <= 0)
            {
                return Array.Empty<EInvoiceEmailHistoryInfo>();
            }

            var normalizedLimit = Math.Clamp(limit, 1, 500);
            var rows = await _db.QueryAsync<EInvoiceEmailHistoryInfo>(
                Net_DB.Net_DB_Manager,
                GetInvoiceHistoryQuery,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_REF_TYPE = EInvoiceEmailHistoryRefTypes.Invoice,
                    p_REF_ID = invoiceId,
                    p_LIMIT = normalizedLimit
                });

            return rows.ToList();
        }

        public Task InsertAsync(EInvoiceEmailHistoryCreateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return _db.QueryFirstAsync<long>(
                Net_DB.Net_DB_Manager,
                InsertQuery,
                new
                {
                    p_COMPANY_CD = TrimMax(request.COMPANY_CD, 20),
                    p_DB_NAME = TrimMax(request.DB_NAME, 50),
                    p_REF_TYPE = TrimMax(request.REF_TYPE, 50),
                    p_REF_ID = request.REF_ID,
                    p_SEND_TYPE = TrimMax(request.SEND_TYPE, 30),
                    p_SEND_STATUS = TrimMax(request.SEND_STATUS, 30),
                    p_FROM_EMAIL = TrimMax(request.FROM_EMAIL, 255),
                    p_TO_EMAIL = TrimMax(request.TO_EMAIL, 1000),
                    p_CC_EMAIL = TrimMax(request.CC_EMAIL, 1000),
                    p_BCC_EMAIL = TrimMax(request.BCC_EMAIL, 1000),
                    p_MAIL_SUBJECT = TrimMax(request.MAIL_SUBJECT, 500),
                    p_MAIL_BODY = request.MAIL_BODY,
                    p_ATTACHMENT_INFO = request.ATTACHMENT_INFO,
                    p_RETRY_COUNT = request.RETRY_COUNT,
                    p_ERROR_MESSAGE = TrimMax(request.ERROR_MESSAGE, 2000),
                    p_SEND_DT = request.SEND_DT,
                    p_CREATE_BY = TrimMax(request.CREATE_BY, 50)
                });
        }

        private static string TrimMax(string? value, int maxLength)
        {
            var text = Common.NormalizeNullableText(value) ?? string.Empty;
            return text.Length <= maxLength ? text : text[..maxLength];
        }
    }
}

using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceMinuteRepository : IEInvoiceMinuteRepository
    {
        private const string GetHeadersQuery = "CALL getEInvoiceBbanInfo(@p_COMPANY_CD, @p_BBAN_ID, @p_FROM_DATE, @p_TO_DATE, @p_KEYWORD, @p_IS_SIGNED)";
        private const string GetReasonsQuery = "CALL getEInvoiceBbanReason(@p_COMPANY_CD, @p_BBAN_ID)";
        private const string DeleteReasonsQuery = "CALL delEInvoiceBbanReasonByBban(@p_COMPANY_CD, @p_BBAN_ID)";
        private const string DeleteQuery = "CALL delEInvoiceBban(@p_COMPANY_CD, @p_BBAN_ID, @p_USERID)";
        private const string SetSignatureQuery = "CALL setEInvoiceBbanSignature(@p_COMPANY_CD, @p_BBAN_ID, @p_SIGNED_XML, @p_CHECKSUM, @p_USERID)";
        private const string SetBuyerSignatureQuery = "CALL setEInvoiceBbanBuyerSignature(@p_COMPANY_CD, @p_BBAN_ID, @p_SIGNED_XML, @p_CHECKSUM, @p_USERID)";
        private const string SetMailQuery = "CALL setEInvoiceBbanMail(@p_COMPANY_CD, @p_BBAN_ID, @p_USERID)";
        private const string SetHeaderQuery = @"CALL setEInvoiceBbanInfo(
            @p_BBAN_ID,
            @p_COMPANY_CD,
            @p_SELLER_ID,
            @p_INVOICE_ID,
            @p_REF_INVOICE_ID,
            @p_PBAN,
            @p_TBBAN,
            @p_SBBAN,
            @p_NBBAN,
            @p_TCHDON,
            @p_NBAN,
            @p_MSTNBAN,
            @p_DCNBAN,
            @p_NMUA,
            @p_MSTNMUA,
            @p_DCNMUA,
            @p_KHMSHDON,
            @p_KHHDON,
            @p_SHDON,
            @p_NLAP,
            @p_DVTTE,
            @p_TGIA,
            @p_MTRACUU,
            @p_TTKHAC_XML,
            @p_NDBBAN_XML,
            @p_IS_MAIL,
            @p_CHECKSUM,
            @p_USERID
        )";
        private const string SetReasonQuery = @"CALL setEInvoiceBbanReason(
            @p_REASON_ID,
            @p_BBAN_ID,
            @p_DETAIL_ID,
            @p_LINE_SIDE,
            @p_SORT_ORDER,
            @p_LDO,
            @p_TCHAT,
            @p_STT,
            @p_MHHDVU,
            @p_THHDVU,
            @p_DVTINH,
            @p_SLUONG,
            @p_DGIA,
            @p_TLCKHAU,
            @p_STCKHAU,
            @p_THTIEN,
            @p_TSUAT,
            @p_TTHUE,
            @p_TSAUTHUE,
            @p_EXTRA_JSON
        )";

        private readonly DapperExecutor _db;

        public EInvoiceMinuteRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IEnumerable<EInvoiceMinuteInfo>> GetHeadersAsync(
            string companyCd,
            long? bbanId,
            DateTime? fromDate,
            DateTime? toDate,
            string? keyword,
            int? isSigned,
            string? dbName = null)
        {
            return await _db.QueryAsync<EInvoiceMinuteInfo>(Net_DB.Net_DB_Company, GetHeadersQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_BBAN_ID = bbanId ?? 0,
                p_FROM_DATE = Common.FormatNullableYmd(fromDate),
                p_TO_DATE = Common.FormatNullableYmd(toDate),
                p_KEYWORD = keyword,
                p_IS_SIGNED = isSigned
            }, sDBName: dbName);
        }

        public async Task<IEnumerable<EInvoiceMinuteLine>> GetReasonsAsync(string companyCd, long bbanId, string? dbName = null)
        {
            return await _db.QueryAsync<EInvoiceMinuteLine>(Net_DB.Net_DB_Company, GetReasonsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_BBAN_ID = bbanId
            }, sDBName: dbName);
        }

        public Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceMinuteInfo minute)
        {
            return session.QuerySingleAsync<long>(SetHeaderQuery, new
            {
                p_BBAN_ID = minute.BBAN_ID,
                p_COMPANY_CD = companyCd,
                p_SELLER_ID = minute.SELLER_ID,
                p_INVOICE_ID = minute.INVOICE_ID,
                p_REF_INVOICE_ID = minute.REF_INVOICE_ID,
                p_PBAN = minute.PBAN,
                p_TBBAN = minute.TBBAN,
                p_SBBAN = minute.SBBAN,
                p_NBBAN = minute.NBBAN,
                p_TCHDON = minute.TCHDON,
                p_NBAN = minute.NBAN,
                p_MSTNBAN = minute.MSTNBAN,
                p_DCNBAN = minute.DCNBAN,
                p_NMUA = minute.NMUA,
                p_MSTNMUA = minute.MSTNMUA,
                p_DCNMUA = minute.DCNMUA,
                p_KHMSHDON = minute.KHMSHDON,
                p_KHHDON = minute.KHHDON,
                p_SHDON = minute.SHDON,
                p_NLAP = minute.NLAP,
                p_DVTTE = minute.DVTTE,
                p_TGIA = minute.TGIA,
                p_MTRACUU = minute.MTRACUU,
                p_TTKHAC_XML = minute.TTKHAC_XML,
                p_NDBBAN_XML = minute.NDBBAN_XML,
                p_IS_MAIL = minute.IS_MAIL,
                p_CHECKSUM = minute.CHECKSUM,
                p_USERID = userId
            });
        }

        public Task<long> SetReasonAsync(DapperSession session, string companyCd, string userId, long bbanId, EInvoiceMinuteLine row)
        {
            return session.QuerySingleAsync<long>(SetReasonQuery, new
            {
                p_REASON_ID = row.REASON_ID,
                p_BBAN_ID = bbanId,
                p_DETAIL_ID = row.DETAIL_ID,
                p_LINE_SIDE = row.LINE_SIDE,
                p_SORT_ORDER = row.SORT_ORDER,
                p_LDO = row.LDO,
                p_TCHAT = row.TCHAT,
                p_STT = row.STT,
                p_MHHDVU = row.MHHDVU,
                p_THHDVU = row.THHDVU,
                p_DVTINH = row.DVTINH,
                p_SLUONG = row.SLUONG,
                p_DGIA = row.DGIA,
                p_TLCKHAU = row.TLCKHAU,
                p_STCKHAU = row.STCKHAU,
                p_THTIEN = row.THTIEN,
                p_TSUAT = row.TSUAT,
                p_TTHUE = row.TTHUE,
                p_TSAUTHUE = row.TSAUTHUE,
                p_EXTRA_JSON = row.EXTRA_JSON
            });
        }

        public Task<int> DeleteReasonsByMinuteAsync(DapperSession session, string companyCd, long bbanId)
        {
            return session.ExecuteAsync(DeleteReasonsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_BBAN_ID = bbanId
            });
        }

        public Task<int> SetSignatureAsync(DapperSession session, string companyCd, string userId, long bbanId, string signedXml, string checksum)
        {
            return session.ExecuteAsync(SetSignatureQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_BBAN_ID = bbanId,
                p_SIGNED_XML = signedXml,
                p_CHECKSUM = checksum,
                p_USERID = userId
            });
        }

        public Task<int> SetBuyerSignatureAsync(DapperSession session, string companyCd, string userId, long bbanId, string signedXml, string checksum)
        {
            return session.ExecuteAsync(SetBuyerSignatureQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_BBAN_ID = bbanId,
                p_SIGNED_XML = signedXml,
                p_CHECKSUM = checksum,
                p_USERID = userId
            });
        }

        public Task<int> SetMailSentAsync(DapperSession session, string companyCd, string userId, long bbanId)
        {
            return session.ExecuteAsync(SetMailQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_BBAN_ID = bbanId,
                p_USERID = userId
            });
        }

        public Task<int> DeleteAsync(DapperSession session, string companyCd, long bbanId, string userId)
        {
            return session.ExecuteAsync(DeleteQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_BBAN_ID = bbanId,
                p_USERID = userId
            });
        }
    }
}

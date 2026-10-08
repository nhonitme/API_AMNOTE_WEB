using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceErrorNoticeRepository : IEInvoiceErrorNoticeRepository
    {
        private const string GetHeadersQuery = "CALL getEInvoiceTbaoInfo(@p_COMPANY_CD, @p_TBAO_ID, @p_FROM_DATE, @p_TO_DATE, @p_KEYWORD, @p_IS_SIGNED)";
        private const string GetDetailsQuery = "CALL getEInvoiceTbaoDetail(@p_COMPANY_CD, @p_TBAO_ID)";
        private const string DeleteDetailsQuery = "CALL delEInvoiceTbaoDetailByTbao(@p_COMPANY_CD, @p_TBAO_ID, @p_USERID)";
        private const string DeleteQuery = "CALL delEInvoiceTbao(@p_COMPANY_CD, @p_TBAO_ID, @p_USERID)";
        private const string SetSignatureQuery = "CALL setEInvoiceTbaoSignature(@p_COMPANY_CD, @p_TBAO_ID, @p_XML, @p_IS_SIGNED, @p_ERROR_MESSAGE, @p_USERID)";
        private const string SetMailQuery = "CALL setEInvoiceTbaoMail(@p_COMPANY_CD, @p_TBAO_ID, @p_USERID)";
        private const string SetHeaderQuery = @"CALL setEInvoiceTbaoInfo(
            @p_TBAO_ID,
            @p_COMPANY_CD,
            @p_PBAN,
            @p_MSO,
            @p_TEN,
            @p_LOAI,
            @p_MCQT,
            @p_TCQT,
            @p_SO,
            @p_NTBCCQT,
            @p_MST,
            @p_TNNT,
            @p_DDANH,
            @p_NTBAO,
            @p_MGDDTU,
            @p_MTDIEP,
            @p_ERROR_MESSAGE,
            @p_USERID
        )";
        private const string SetDetailQuery = @"CALL setEInvoiceTbaoDetail(
            @p_DETAIL_ID,
            @p_TBAO_ID,
            @p_COMPANY_CD,
            @p_STT,
            @p_MCCQT,
            @p_KHMSHDON,
            @p_KHHDON,
            @p_SHDON,
            @p_NGAY,
            @p_LADHDDT,
            @p_LDO,
            @p_ISDEL,
            @p_USERID
        )";

        private readonly DapperExecutor _db;

        public EInvoiceErrorNoticeRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IEnumerable<EInvoiceErrorNoticeInfo>> GetHeadersAsync(string companyCd, long? tbaoId, DateTime? fromDate, DateTime? toDate, string? keyword, int? isSigned)
        {
            return await _db.QueryAsync<EInvoiceErrorNoticeInfo>(Net_DB.Net_DB_Company, GetHeadersQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TBAO_ID = tbaoId ?? 0,
                p_FROM_DATE = Common.FormatNullableYmd(fromDate),
                p_TO_DATE = Common.FormatNullableYmd(toDate),
                p_KEYWORD = keyword,
                p_IS_SIGNED = isSigned
            });
        }

        public async Task<IEnumerable<EInvoiceErrorNoticeDetail>> GetDetailsAsync(string companyCd, long tbaoId)
        {
            return await _db.QueryAsync<EInvoiceErrorNoticeDetail>(Net_DB.Net_DB_Company, GetDetailsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TBAO_ID = tbaoId
            });
        }

        public Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceErrorNoticeInfo notice)
        {
            return session.QuerySingleAsync<long>(SetHeaderQuery, new
            {
                p_TBAO_ID = notice.TBAO_ID,
                p_COMPANY_CD = companyCd,
                p_PBAN = notice.PBAN,
                p_MSO = notice.MSO,
                p_TEN = notice.TEN,
                p_LOAI = notice.LOAI,
                p_MCQT = notice.MCQT,
                p_TCQT = notice.TCQT,
                p_SO = notice.SO,
                p_NTBCCQT = notice.NTBCCQT,
                p_MST = notice.MST,
                p_TNNT = notice.TNNT,
                p_DDANH = notice.DDANH,
                p_NTBAO = notice.NTBAO,
                p_MGDDTU = notice.MGDDTU,
                p_MTDIEP = notice.MTDIEP,
                p_ERROR_MESSAGE = notice.ERROR_MESSAGE,
                p_USERID = userId
            });
        }

        public Task<long> SetDetailAsync(DapperSession session, string companyCd, string userId, long tbaoId, EInvoiceErrorNoticeDetail detail)
        {
            return session.QuerySingleAsync<long>(SetDetailQuery, new
            {
                p_DETAIL_ID = detail.DETAIL_ID,
                p_TBAO_ID = tbaoId,
                p_COMPANY_CD = companyCd,
                p_STT = detail.STT,
                p_MCCQT = detail.MCCQT,
                p_KHMSHDON = detail.KHMSHDON,
                p_KHHDON = detail.KHHDON,
                p_SHDON = detail.SHDON,
                p_NGAY = detail.NGAY,
                p_LADHDDT = detail.LADHDDT,
                p_LDO = detail.LDO,
                p_ISDEL = detail.ISDEL,
                p_USERID = userId
            });
        }

        public Task<int> SetSignatureAsync(DapperSession session, string companyCd, string userId, long tbaoId, string signedXml, int isSigned, string? errorMessage)
        {
            return session.ExecuteAsync(SetSignatureQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TBAO_ID = tbaoId,
                p_XML = signedXml,
                p_IS_SIGNED = isSigned,
                p_ERROR_MESSAGE = errorMessage,
                p_USERID = userId
            });
        }

        public Task<int> SetMailSentAsync(DapperSession session, string companyCd, string userId, long tbaoId)
        {
            return session.ExecuteAsync(SetMailQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TBAO_ID = tbaoId,
                p_USERID = userId
            });
        }

        public Task<int> DeleteDetailsByNoticeAsync(DapperSession session, string companyCd, long tbaoId, string userId)
        {
            return session.ExecuteAsync(DeleteDetailsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TBAO_ID = tbaoId,
                p_USERID = userId
            });
        }

        public Task<int> DeleteAsync(DapperSession session, string companyCd, long tbaoId, string userId)
        {
            return session.ExecuteAsync(DeleteQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TBAO_ID = tbaoId,
                p_USERID = userId
            });
        }
    }
}

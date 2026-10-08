using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceDeclarationRepository : IEInvoiceDeclarationRepository
    {
        private const string GetHeadersQuery = "CALL getEInvoiceTkhaiInfo(@p_COMPANY_CD, @p_TKHAI_ID, @p_FROM_DATE, @p_TO_DATE, @p_KEYWORD, @p_IS_SIGNED)";
        private const string GetDetailsQuery = "CALL getEInvoiceTkhaiDetail(@p_COMPANY_CD, @p_TKHAI_ID)";
        private const string DeleteDetailsQuery = "CALL delEInvoiceTkhaiDetailByTkhai(@p_COMPANY_CD, @p_TKHAI_ID, @p_USERID)";
        private const string DeleteQuery = "CALL delEInvoiceTkhai(@p_COMPANY_CD, @p_TKHAI_ID, @p_USERID)";
        private const string SetSignatureQuery = "CALL setEInvoiceTkhaiSignature(@p_COMPANY_CD, @p_TKHAI_ID, @p_XML, @p_IS_SIGNED, @p_ERROR_MESSAGE, @p_USERID)";
        private const string SetHeaderQuery = @"CALL setEInvoiceTkhaiInfo(
            @p_TKHAI_ID,
            @p_COMPANY_CD,
            @p_PBAN,
            @p_MSO,
            @p_TEN,
            @p_HTHUC,
            @p_TNNT,
            @p_MST,
            @p_CQTQLY,
            @p_MCQTQLY,
            @p_TNDDPLUAT,
            @p_DTDDPLUAT,
            @p_CCCDAN,
            @p_SHCHIEU,
            @p_MQTNDDPLUAT,
            @p_QTICH,
            @p_NSDDPLUAT,
            @p_GTINH,
            @p_DCLHE,
            @p_DCTDTU,
            @p_NLHE,
            @p_DTLHE,
            @p_DDANH,
            @p_NLAP,
            @p_CMA,
            @p_CMTMTTIEN,
            @p_KCMTMTTIEN,
            @p_KCMA,
            @p_NNTDBKKHAN,
            @p_NNTKTDNUBND,
            @p_CQXLTSCONG,
            @p_CDLTTDCQT,
            @p_CDLQTCTN,
            @p_TCNNGOAI,
            @p_CDDU,
            @p_CDLTHDTHU,
            @p_CBTHOP,
            @p_CTTCTGDICH,
            @p_HDGTGT,
            @p_HDGTGTTHBLAI,
            @p_HDBHANG,
            @p_HDBHTHBLAI,
            @p_HDTMAI,
            @p_HDNCCNNGOAI,
            @p_HDBTSCONG,
            @p_HDBHDTQGIA,
            @p_HDKHAC,
            @p_CTU,
            @p_MGDDTU,
            @p_MTDIEP,
            @p_ERROR_MESSAGE,
            @p_USERID
        )";
        private const string SetDetailQuery = @"CALL setEInvoiceTkhaiDetail(
            @p_DETAIL_ID,
            @p_TKHAI_ID,
            @p_COMPANY_CD,
            @p_DETAIL_TYPE,
            @p_STT,
            @p_TTCHUC,
            @p_SERI,
            @p_CTS_HTHUC,
            @p_TTCGP,
            @p_MSTTCGP,
            @p_TTCTN,
            @p_MSTTCTN,
            @p_TDVHTPT,
            @p_MSTDVHTPT,
            @p_TDVI,
            @p_MSTDUQ,
            @p_HDBRMVAO,
            @p_TDLHDTNGAY,
            @p_TDLHDDNGAY,
            @p_TGUQTNGAY,
            @p_TGUQDNGAY,
            @p_TLHDON,
            @p_KHMSHDON,
            @p_KHHDON,
            @p_TENDKTH,
            @p_MSTDKTH,
            @p_MDICH,
            @p_TNGAY,
            @p_DNGAY,
            @p_GCHU,
            @p_RAW_DETAIL_XML,
            @p_ISDEL,
            @p_USERID
        )";

        private readonly DapperExecutor _db;

        public EInvoiceDeclarationRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IEnumerable<EInvoiceDeclarationInfo>> GetHeadersAsync(string companyCd, long? tkhaiId, DateTime? fromDate, DateTime? toDate, string? keyword, int? isSigned)
        {
            return await _db.QueryAsync<EInvoiceDeclarationInfo>(Net_DB.Net_DB_Company, GetHeadersQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TKHAI_ID = tkhaiId ?? 0,
                p_FROM_DATE = Common.FormatNullableYmd(fromDate),
                p_TO_DATE = Common.FormatNullableYmd(toDate),
                p_KEYWORD = keyword,
                p_IS_SIGNED = isSigned
            });
        }

        public async Task<IEnumerable<EInvoiceDeclarationDetail>> GetDetailsAsync(string companyCd, long tkhaiId)
        {
            return await _db.QueryAsync<EInvoiceDeclarationDetail>(Net_DB.Net_DB_Company, GetDetailsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TKHAI_ID = tkhaiId
            });
        }

        public Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceDeclarationInfo declaration)
        {
            return session.QuerySingleAsync<long>(SetHeaderQuery, BuildHeaderParams(companyCd, userId, declaration));
        }

        public Task<long> SetDetailAsync(DapperSession session, string companyCd, string userId, long tkhaiId, EInvoiceDeclarationDetail detail)
        {
            return session.QuerySingleAsync<long>(SetDetailQuery, new
            {
                p_DETAIL_ID = detail.DETAIL_ID,
                p_TKHAI_ID = tkhaiId,
                p_COMPANY_CD = companyCd,
                p_DETAIL_TYPE = detail.DETAIL_TYPE,
                p_STT = detail.STT,
                p_TTCHUC = detail.TTCHUC,
                p_SERI = detail.SERI,
                p_CTS_HTHUC = detail.CTS_HTHUC,
                p_TTCGP = detail.TTCGP,
                p_MSTTCGP = detail.MSTTCGP,
                p_TTCTN = detail.TTCTN,
                p_MSTTCTN = detail.MSTTCTN,
                p_TDVHTPT = detail.TDVHTPT,
                p_MSTDVHTPT = detail.MSTDVHTPT,
                p_TDVI = detail.TDVI,
                p_MSTDUQ = detail.MSTDUQ,
                p_HDBRMVAO = detail.HDBRMVAO,
                p_TDLHDTNGAY = detail.TDLHDTNGAY,
                p_TDLHDDNGAY = detail.TDLHDDNGAY,
                p_TGUQTNGAY = detail.TGUQTNGAY,
                p_TGUQDNGAY = detail.TGUQDNGAY,
                p_TLHDON = detail.TLHDON,
                p_KHMSHDON = detail.KHMSHDON,
                p_KHHDON = detail.KHHDON,
                p_TENDKTH = detail.TENDKTH,
                p_MSTDKTH = detail.MSTDKTH,
                p_MDICH = detail.MDICH,
                p_TNGAY = detail.TNGAY,
                p_DNGAY = detail.DNGAY,
                p_GCHU = detail.GCHU,
                p_RAW_DETAIL_XML = detail.RAW_DETAIL_XML,
                p_ISDEL = detail.ISDEL,
                p_USERID = userId
            });
        }

        public Task<int> SetSignatureAsync(DapperSession session, string companyCd, string userId, long tkhaiId, string signedXml, int isSigned, string? errorMessage)
        {
            return session.ExecuteAsync(SetSignatureQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TKHAI_ID = tkhaiId,
                p_XML = signedXml,
                p_IS_SIGNED = isSigned,
                p_ERROR_MESSAGE = errorMessage,
                p_USERID = userId
            });
        }

        public Task<int> DeleteDetailsByDeclarationAsync(DapperSession session, string companyCd, long tkhaiId, string userId)
        {
            return session.ExecuteAsync(DeleteDetailsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TKHAI_ID = tkhaiId,
                p_USERID = userId
            });
        }

        public Task<int> DeleteAsync(DapperSession session, string companyCd, long tkhaiId, string userId)
        {
            return session.ExecuteAsync(DeleteQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_TKHAI_ID = tkhaiId,
                p_USERID = userId
            });
        }

        private static object BuildHeaderParams(string companyCd, string userId, EInvoiceDeclarationInfo declaration)
        {
            return new
            {
                p_TKHAI_ID = declaration.TKHAI_ID,
                p_COMPANY_CD = companyCd,
                p_PBAN = declaration.PBAN,
                p_MSO = declaration.MSO,
                p_TEN = declaration.TEN,
                p_HTHUC = declaration.HTHUC,
                p_TNNT = declaration.TNNT,
                p_MST = declaration.MST,
                p_CQTQLY = declaration.CQTQLY,
                p_MCQTQLY = declaration.MCQTQLY,
                p_TNDDPLUAT = declaration.TNDDPLUAT,
                p_DTDDPLUAT = declaration.DTDDPLUAT,
                p_CCCDAN = declaration.CCCDAN,
                p_SHCHIEU = declaration.SHCHIEU,
                p_MQTNDDPLUAT = declaration.MQTNDDPLUAT,
                p_QTICH = declaration.QTICH,
                p_NSDDPLUAT = declaration.NSDDPLUAT,
                p_GTINH = declaration.GTINH,
                p_DCLHE = declaration.DCLHE,
                p_DCTDTU = declaration.DCTDTU,
                p_NLHE = declaration.NLHE,
                p_DTLHE = declaration.DTLHE,
                p_DDANH = declaration.DDANH,
                p_NLAP = declaration.NLAP,
                p_CMA = declaration.CMA,
                p_CMTMTTIEN = declaration.CMTMTTIEN,
                p_KCMTMTTIEN = declaration.KCMTMTTIEN,
                p_KCMA = declaration.KCMA,
                p_NNTDBKKHAN = declaration.NNTDBKKHAN,
                p_NNTKTDNUBND = declaration.NNTKTDNUBND,
                p_CQXLTSCONG = declaration.CQXLTSCONG,
                p_CDLTTDCQT = declaration.CDLTTDCQT,
                p_CDLQTCTN = declaration.CDLQTCTN,
                p_TCNNGOAI = declaration.TCNNGOAI,
                p_CDDU = declaration.CDDU,
                p_CDLTHDTHU = declaration.CDLTHDTHU,
                p_CBTHOP = declaration.CBTHOP,
                p_CTTCTGDICH = declaration.CTTCTGDICH,
                p_HDGTGT = declaration.HDGTGT,
                p_HDGTGTTHBLAI = declaration.HDGTGTTHBLAI,
                p_HDBHANG = declaration.HDBHANG,
                p_HDBHTHBLAI = declaration.HDBHTHBLAI,
                p_HDTMAI = declaration.HDTMAI,
                p_HDNCCNNGOAI = declaration.HDNCCNNGOAI,
                p_HDBTSCONG = declaration.HDBTSCONG,
                p_HDBHDTQGIA = declaration.HDBHDTQGIA,
                p_HDKHAC = declaration.HDKHAC,
                p_CTU = declaration.CTU,
                p_MGDDTU = declaration.MGDDTU,
                p_MTDIEP = declaration.MTDIEP,
                p_ERROR_MESSAGE = declaration.ERROR_MESSAGE,
                p_USERID = userId
            };
        }
    }
}

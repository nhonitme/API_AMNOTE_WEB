using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceRepository : IEInvoiceRepository
    {
        private const string GetHeadersQuery = "CALL getEInvoiceInfo(@p_COMPANY_CD, @p_INVOICE_ID, @p_FROM_DATE, @p_TO_DATE, @p_KEYWORD, @p_PAGE_NUMBER, @p_PAGE_SIZE, @p_INCLUDE_DETAILS, @p_KHHDON, @p_KHHDON_OP, @p_SHDON_FROM, @p_SHDON_TO, @p_NMUA_TEN, @p_NMUA_TEN_OP, @p_NMUA_MST, @p_NMUA_MST_OP, @p_INVOICE_STATUS, @p_CQT_STATUS, @p_IS_SIGNED, @p_TCHDON, @p_MAIL_STATUS, @p_CASH_REGISTER)";
        private const string GetDetailsBundleQuery = "CALL getEInvoiceDetails(@p_COMPANY_CD, @p_INVOICE_IDS)";
        private const string DeleteDetailsQuery = "CALL delEInvoiceDetailByInvoice(@p_COMPANY_CD, @p_INVOICE_ID, @p_USERID)";
        private const string DeleteDetailSpecialsQuery = "CALL delEInvoiceDetailSpecialInfoByInvoice(@p_COMPANY_CD, @p_INVOICE_ID)";
        private const string DeletePxkQuery = "CALL delEInvoicePxkInfoByInvoice(@p_COMPANY_CD, @p_INVOICE_ID, @p_USERID)";
        private const string DeleteRelatedQuery = "CALL delEInvoiceRelatedInfoByInvoice(@p_COMPANY_CD, @p_INVOICE_ID)";
        private const string GetBkeByInvoiceQuery = "CALL getEInvoiceBkeByInvoice(@p_COMPANY_CD, @p_INVOICE_ID)";
        private const string GetNextBkeNoQuery = "CALL getNextEInvoiceBkeNo(@p_COMPANY_CD, @p_YEAR)";
        private const string DeleteBkeByInvoiceQuery = "CALL delEInvoiceBkeByInvoice(@p_COMPANY_CD, @p_INVOICE_ID, @p_USERID)";
        private const string DeleteBkeChildrenQuery = "CALL delEInvoiceBkeChildren(@p_BKE_ID)";
        private const string SetBkeSignatureQuery = "CALL setEInvoiceBkeSignature(@p_COMPANY_CD, @p_INVOICE_ID, @p_SIGNED_XML, @p_IS_SIGNED, @p_USERID)";
        private const string SetBkeInfoQuery = @"CALL setEInvoiceBkeInfo(
            @p_BKE_ID,
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_SELLER_ID,
            @p_PBAN,
            @p_TBKE,
            @p_KHMBKE,
            @p_SBKE,
            @p_NBKE,
            @p_TCHDON,
            @p_NBAN,
            @p_MSTNBAN,
            @p_DCNBAN,
            @p_TCTCNHANG,
            @p_NMUA,
            @p_MSTNMUA,
            @p_DCNMUA,
            @p_TTKHAC_XML,
            @p_USERID
        )";
        private const string SetBkeReasonQuery = @"CALL setEInvoiceBkeReason(
            @p_REASON_ID,
            @p_BKE_ID,
            @p_SORT_ORDER,
            @p_LDO
        )";
        private const string SetBkeDetailQuery = @"CALL setEInvoiceBkeDetail(
            @p_DETAIL_ID,
            @p_BKE_ID,
            @p_STT,
            @p_REF_INVOICE_ID,
            @p_KHMSHDON,
            @p_KHHDON,
            @p_SHDON,
            @p_THHDVGOC,
            @p_SLGOC,
            @p_DGGOC,
            @p_THTGOC,
            @p_TSGOC,
            @p_TTGOC,
            @p_TGTKGOC,
            @p_TGTSTGOC,
            @p_THHDVTDOI,
            @p_SLTDOI,
            @p_DGTDOI,
            @p_THTTDOI,
            @p_TSTDOI,
            @p_TTTDOI,
            @p_TGTTDOI,
            @p_TGTSTTDOI,
            @p_TGTCTCLECH,
            @p_TGTTCLECH,
            @p_TGTKCLECH,
            @p_TGTTTCLECH,
            @p_EXTRA_JSON
        )";
        private const string GetTchdonQuery = "CALL getEInvoiceTchdon(@p_COMPANY_CD, @p_INVOICE_ID)";
        private const string DeleteQuery = "CALL delEInvoice(@p_COMPANY_CD, @p_INVOICE_ID, @p_USERID)";
        private const string SetSignatureQuery = "CALL setEInvoiceSignature(@p_COMPANY_CD, @p_INVOICE_ID, @p_XML_FTP_PATH, @p_MTDIEP, @p_IS_SIGNED, @p_ERROR_MESSAGE, @p_USERID)";
        private const string GetNextShdonQuery = "CALL getNextEInvoiceShdon(@p_COMPANY_CD, @p_INVOICE_ID, @p_KHMSHDON, @p_KHHDON)";
        private const string SetShdonQuery = "CALL setEInvoiceShdon(@p_COMPANY_CD, @p_INVOICE_ID, @p_SHDON, @p_NLAP, @p_USERID)";
        private const string RevertSigningReservationQuery = "CALL revertEInvoiceSigningReservation(@p_COMPANY_CD, @p_INVOICE_ID, @p_SHDON, @p_NLAP, @p_USERID)";
        private const string SetBuyerEmailQuery = "CALL setEInvoiceBuyerEmail(@p_COMPANY_CD, @p_INVOICE_ID, @p_NMUA_DCTDTU, @p_USERID)";
        private const string SetMailStatusQuery = "CALL setEInvoiceMailStatus(@p_COMPANY_CD, @p_INVOICE_ID, @p_MAIL_STATUS, @p_USERID)";
        private const string SetHeaderQuery = @"CALL setEInvoiceInfo(
            @p_INVOICE_ID,
            @p_COMPANY_CD,
            @p_SELLER_ID,
            @p_XSL_ID,
            @p_PBAN,
            @p_THDON,
            @p_KHMSHDON,
            @p_KHHDON,
            @p_SHDON,
            @p_MHSO,
            @p_NLAP,
            @p_HDCTTCHINH,
            @p_SBKE,
            @p_NBKE,
            @p_DVTTE,
            @p_TGIA,
            @p_HTTTOAN,
            @p_MSTTCGP,
            @p_TCHDON,
            @p_SOURCE_INVOICE_ID,
            @p_NMUA_TEN,
            @p_NMUA_MST,
            @p_NMUA_MDVQHNSACH,
            @p_NMUA_DCHI,
            @p_NMUA_MTINH,
            @p_NMUA_TTINH,
            @p_NMUA_MXA,
            @p_NMUA_TXA,
            @p_NMUA_MKHANG,
            @p_NMUA_SDTHOAI,
            @p_NMUA_CCCDAN,
            @p_NMUA_SHCHIEU,
            @p_NMUA_DCTDTU,
            @p_NMUA_HVTNMHANG,
            @p_NMUA_STKNHANG,
            @p_NMUA_TNHANG,
            @p_TGTCTHUE,
            @p_TGTKCTHUE,
            @p_TGTTTHUE,
            @p_TTCKTMAI,
            @p_CKTMAI_GCHU,
            @p_TGTKHAC,
            @p_TGTTTBSO,
            @p_TGTTTBCHU,
            @p_TGTCTHUE_VND,
            @p_TGTKCTHUE_VND,
            @p_TGTTTHUE_VND,
            @p_TTCKTMAI_VND,
            @p_TGTKHAC_VND,
            @p_TGTTTBSO_VND,
            @p_DLQRCODE,
            @p_MCCQT,
            @p_MTRACUU,
            @p_MTDIEP,
            @p_MGDDTu,
            @p_TAX_SUMMARY_JSON,
            @p_FEE_JSON,
            @p_EXTRA_JSON,
            @p_ERROR_MESSAGE,
            @p_DOC_VERSION,
            @p_USERID
        )";
        private const string SetRelatedQuery = @"CALL setEInvoiceRelatedInfo(
            @p_RELATED_ID,
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_TCHDON,
            @p_IS_EXTERNAL,
            @p_MSTCLQUAN,
            @p_LHDCLQUAN,
            @p_KHMSHDCLQUAN,
            @p_KHHDCLQUAN,
            @p_SHDCLQUAN,
            @p_NLHDCLQUAN,
            @p_LDDCTTHE,
            @p_SBKCLQUAN,
            @p_NBKCLQUAN,
            @p_GCHU,
            @p_USERID
        )";
        private const string SetDetailQuery = @"CALL setEInvoiceDetail(
            @p_DETAIL_ID,
            @p_INVOICE_ID,
            @p_COMPANY_CD,
            @p_TCHAT,
            @p_STT,
            @p_MHHDVU,
            @p_THHDVU,
            @p_DVTINH,
            @p_SLUONG,
            @p_SLTHUCNHAP,
            @p_DGIA,
            @p_TLCKHAU,
            @p_STCKHAU,
            @p_THTIEN,
            @p_TSUAT,
            @p_TTHUE,
            @p_TSAUTHUE,
            @p_DGIA_VND,
            @p_STCKHAU_VND,
            @p_THTIEN_VND,
            @p_TTHUE_VND,
            @p_TSAUTHUE_VND,
            @p_EXTRA_JSON,
            @p_ISDEL,
            @p_USERID
        )";
        private const string SetDetailSpecialQuery = @"CALL setEInvoiceDetailSpecialInfo(
            @p_SPECIAL_ID,
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_DETAIL_ID,
            @p_LHHDTRUNG,
            @p_SKHUNG,
            @p_SMAY,
            @p_BKSPT_VCHUYEN,
            @p_TNG_HANG,
            @p_DCNG_HANG,
            @p_MSTNG_HANG,
            @p_MDDNG_HANG,
            @p_EXTRA_JSON,
            @p_USERID
        )";
        private const string SetPxkQuery = @"CALL setEInvoicePxkInfo(
            @p_PXK_ID,
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_PXK_TYPE,
            @p_NBAN_DCHI,
            @p_LDDNBO,
            @p_HDKTSO,
            @p_HDKTNGAY,
            @p_HVTNXHANG,
            @p_TNVCHUYEN,
            @p_HDSO,
            @p_PTVCHUYEN,
            @p_EXTRA_JSON,
            @p_USERID
        )";

        private readonly DapperExecutor _db;
        private readonly IMasterDataCacheService _cache;

        public EInvoiceRepository(DapperExecutor db, IMasterDataCacheService cache)
        {
            _db = db;
            _cache = cache;
        }

        private sealed class TotalRecordResult
        {
            public int TOTAL_RECORDS { get; set; }
        }

        public async Task<IEnumerable<EInvoiceInfo>> GetHeadersAsync(string companyCd, long? invoiceId, DateTime? fromDate, DateTime? toDate, string? keyword, string? dbName = null)
        {
            var result = await GetHeadersPagedAsync(
                companyCd,
                invoiceId,
                fromDate,
                toDate,
                keyword,
                1,
                int.MaxValue,
                includeDetails: false,
                filters: null,
                dbName: dbName);

            return result.Items;
        }

        public async Task<(IReadOnlyList<EInvoiceInfo> Items, int TotalRecords)> GetHeadersPagedAsync(
            string companyCd,
            long? invoiceId,
            DateTime? fromDate,
            DateTime? toDate,
            string? keyword,
            int pageNumber = 1,
            int pageSize = 20,
            bool includeDetails = false,
            EInvoiceSearchRequest? filters = null,
            string? dbName = null)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, dbName);
            using var grid = await session.QueryMultipleAsync(
                GetHeadersQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INVOICE_ID = invoiceId ?? 0,
                    p_FROM_DATE = Common.FormatNullableYmd(fromDate),
                    p_TO_DATE = Common.FormatNullableYmd(toDate),
                    p_KEYWORD = keyword,
                    p_PAGE_NUMBER = pageNumber,
                    p_PAGE_SIZE = pageSize,
                    p_INCLUDE_DETAILS = includeDetails ? 1 : 0,
                    p_CASH_REGISTER = filters?.CashRegister,
                    p_KHHDON = filters?.Khhdon,
                    p_KHHDON_OP = filters?.KhhdonOp,
                    p_SHDON_FROM = filters?.ShdonFrom,
                    p_SHDON_TO = filters?.ShdonTo,
                    p_NMUA_TEN = filters?.NmuaTen,
                    p_NMUA_TEN_OP = filters?.NmuaTenOp,
                    p_NMUA_MST = filters?.NmuaMst,
                    p_NMUA_MST_OP = filters?.NmuaMstOp,
                    p_INVOICE_STATUS = filters?.InvoiceStatus,
                    p_CQT_STATUS = filters?.CqtStatus,
                    p_IS_SIGNED = filters?.IsSigned,
                    p_TCHDON = filters?.Tchdon,
                    p_MAIL_STATUS = filters?.MailStatus
                });

            var items = (await grid.ReadAsync<EInvoiceInfo>()).ToList();
            var totalRow = (await grid.ReadAsync<TotalRecordResult>()).FirstOrDefault();
            var totalRecords = totalRow?.TOTAL_RECORDS ?? 0;

            if (includeDetails)
            {
                var details = (await grid.ReadAsync<EInvoiceDetail>()).ToList();
                var specials = (await grid.ReadAsync<EInvoiceDetailSpecialInfo>()).ToList();
                var pxkItems = (await grid.ReadAsync<EInvoicePxkInfo>()).ToList();
                var relatedItems = (await grid.ReadAsync<EInvoiceRelatedInfo>()).ToList();

                if (items.Count > 0)
                {
                    AttachDetailsBundle(items, details, specials, pxkItems, relatedItems);
                }
            }

            return (items, totalRecords > 0 ? totalRecords : items.Count);
        }

        public async Task<EInvoiceDetailsBundle> GetDetailsBundleAsync(string companyCd, IEnumerable<long> invoiceIds, string? dbName = null)
        {
            var ids = invoiceIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new EInvoiceDetailsBundle();
            }

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, dbName);
            using var grid = await session.QueryMultipleAsync(
                GetDetailsBundleQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INVOICE_IDS = string.Join(",", ids)
                });

            return new EInvoiceDetailsBundle
            {
                Details = (await grid.ReadAsync<EInvoiceDetail>()).ToList(),
                Specials = (await grid.ReadAsync<EInvoiceDetailSpecialInfo>()).ToList(),
                PxkItems = (await grid.ReadAsync<EInvoicePxkInfo>()).ToList(),
                RelatedItems = (await grid.ReadAsync<EInvoiceRelatedInfo>()).ToList()
            };
        }

        public async Task<IEnumerable<EInvoiceDetail>> GetDetailsAsync(string companyCd, long invoiceId, string? dbName = null)
        {
            var bundle = await GetDetailsBundleAsync(companyCd, new[] { invoiceId }, dbName);
            return bundle.Details;
        }

        public async Task<IEnumerable<EInvoiceDetailSpecialInfo>> GetDetailSpecialsAsync(string companyCd, long invoiceId, string? dbName = null)
        {
            var bundle = await GetDetailsBundleAsync(companyCd, new[] { invoiceId }, dbName);
            return bundle.Specials;
        }

        public async Task<EInvoicePxkInfo?> GetPxkAsync(string companyCd, long invoiceId, string? dbName = null)
        {
            var bundle = await GetDetailsBundleAsync(companyCd, new[] { invoiceId }, dbName);
            return bundle.PxkItems.FirstOrDefault(x => x.INVOICE_ID == invoiceId);
        }

        public async Task<EInvoiceRelatedInfo?> GetRelatedAsync(string companyCd, long invoiceId, string? dbName = null)
        {
            var bundle = await GetDetailsBundleAsync(companyCd, new[] { invoiceId }, dbName);
            return bundle.RelatedItems.FirstOrDefault(x => x.INVOICE_ID == invoiceId);
        }

        public async Task<EInvoiceBkeInfo?> GetBkeByInvoiceAsync(string companyCd, long invoiceId, string? dbName = null)
        {
            if (invoiceId <= 0)
            {
                return null;
            }

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, dbName);
            try
            {
                using var grid = await session.QueryMultipleAsync(
                    GetBkeByInvoiceQuery,
                    new
                    {
                        p_COMPANY_CD = companyCd,
                        p_INVOICE_ID = invoiceId
                    });

                // Một số connector MySQL trả result-set rỗng trước SELECT đầu → bỏ qua đến header thật.
                EInvoiceBkeInfo? header = null;
                while (!grid.IsConsumed && header == null)
                {
                    var rows = (await grid.ReadAsync<EInvoiceBkeInfo>()).ToList();
                    header = rows.FirstOrDefault(x =>
                        x.INVOICE_ID == invoiceId
                        || string.Equals(x.COMPANY_CD, companyCd, StringComparison.OrdinalIgnoreCase)
                        || !string.IsNullOrWhiteSpace(x.SBKE));
                }

                if (header == null)
                {
                    return null;
                }

                header.REASONS = grid.IsConsumed
                    ? new List<EInvoiceBkeReason>()
                    : (await grid.ReadAsync<EInvoiceBkeReason>()).ToList();
                header.DETAILS = grid.IsConsumed
                    ? new List<EInvoiceBkeDetail>()
                    : (await grid.ReadAsync<EInvoiceBkeDetail>()).ToList();
                return header;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"GetBkeByInvoice failed. Company={companyCd}, InvoiceId={invoiceId}. {ex.Message}",
                    ex);
            }
        }

        public async Task<string> GetNextBkeNoAsync(string companyCd, int? year = null, string? dbName = null)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, dbName);
            var result = await session.QuerySingleAsync<NextBkeNoResult>(GetNextBkeNoQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_YEAR = year ?? 0
            });

            var next = Common.NormalizeNullableText(result.NEXT_SBKE);
            if (!HasText(next))
            {
                throw new InvalidOperationException("Failed to generate next bảng kê number");
            }

            return next!;
        }

        private sealed class NextBkeNoResult
        {
            public string? NEXT_SBKE { get; set; }
            public int YEAR_NO { get; set; }
            public long SEQ_NO { get; set; }
        }

        private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

        private static void AttachDetailsBundle(
            IList<EInvoiceInfo> headers,
            IReadOnlyList<EInvoiceDetail> details,
            IReadOnlyList<EInvoiceDetailSpecialInfo> specials,
            IReadOnlyList<EInvoicePxkInfo> pxkItems,
            IReadOnlyList<EInvoiceRelatedInfo> relatedItems)
        {
            var specialByDetailId = specials
                .GroupBy(x => x.DETAIL_ID)
                .ToDictionary(group => group.Key, group => group.First());

            var detailsByInvoiceId = details
                .GroupBy(x => x.INVOICE_ID)
                .ToDictionary(group => group.Key, group => group.ToList());

            var pxkByInvoiceId = pxkItems
                .GroupBy(x => x.INVOICE_ID)
                .ToDictionary(group => group.Key, group => group.First());

            var relatedByInvoiceId = relatedItems
                .GroupBy(x => x.INVOICE_ID)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var header in headers)
            {
                if (detailsByInvoiceId.TryGetValue(header.INVOICE_ID, out var invoiceDetails))
                {
                    foreach (var detail in invoiceDetails)
                    {
                        if (specialByDetailId.TryGetValue(detail.DETAIL_ID, out var special))
                        {
                            detail.SPECIAL = special;
                        }
                    }

                    header.DETAILS = invoiceDetails;
                }
                else
                {
                    header.DETAILS = new List<EInvoiceDetail>();
                }

                header.PXK_INFO = pxkByInvoiceId.TryGetValue(header.INVOICE_ID, out var pxk) ? pxk : null;
                header.RELATED = relatedByInvoiceId.TryGetValue(header.INVOICE_ID, out var related) ? related : null;
            }
        }

        public Task<long> SetHeaderAsync(DapperSession session, string companyCd, string userId, EInvoiceInfo invoice)
        {
            return session.QuerySingleAsync<long>(SetHeaderQuery, BuildHeaderParams(companyCd, userId, invoice));
        }

        public Task<long> SetDetailAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoiceDetail detail)
        {
            return session.QuerySingleAsync<long>(SetDetailQuery, new
            {
                p_DETAIL_ID = detail.DETAIL_ID,
                p_INVOICE_ID = invoiceId,
                p_COMPANY_CD = companyCd,
                p_TCHAT = detail.TCHAT,
                p_STT = detail.STT,
                p_MHHDVU = detail.MHHDVU,
                p_THHDVU = detail.THHDVU,
                p_DVTINH = detail.DVTINH,
                p_SLUONG = detail.SLUONG,
                p_SLTHUCNHAP = detail.SLTHUCNHAP,
                p_DGIA = detail.DGIA,
                p_TLCKHAU = detail.TLCKHAU,
                p_STCKHAU = detail.STCKHAU,
                p_THTIEN = detail.THTIEN,
                p_TSUAT = detail.TSUAT,
                p_TTHUE = detail.TTHUE,
                p_TSAUTHUE = detail.TSAUTHUE,
                p_DGIA_VND = detail.DGIA_VND,
                p_STCKHAU_VND = detail.STCKHAU_VND,
                p_THTIEN_VND = detail.THTIEN_VND,
                p_TTHUE_VND = detail.TTHUE_VND,
                p_TSAUTHUE_VND = detail.TSAUTHUE_VND,
                p_EXTRA_JSON = detail.EXTRA_JSON,
                p_ISDEL = detail.ISDEL,
                p_USERID = userId
            });
        }

        public Task<long> SetDetailSpecialAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            long detailId,
            EInvoiceDetailSpecialInfo special)
        {
            return session.QuerySingleAsync<long>(SetDetailSpecialQuery, new
            {
                p_SPECIAL_ID = special.SPECIAL_ID,
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_DETAIL_ID = detailId,
                p_LHHDTRUNG = special.LHHDTRUNG,
                p_SKHUNG = special.SKHUNG,
                p_SMAY = special.SMAY,
                p_BKSPT_VCHUYEN = special.BKSPT_VCHUYEN,
                p_TNG_HANG = special.TNG_HANG,
                p_DCNG_HANG = special.DCNG_HANG,
                p_MSTNG_HANG = special.MSTNG_HANG,
                p_MDDNG_HANG = special.MDDNG_HANG,
                p_EXTRA_JSON = special.EXTRA_JSON,
                p_USERID = userId
            });
        }

        public Task<int> DeleteDetailSpecialByInvoiceAsync(DapperSession session, string companyCd, long invoiceId)
        {
            return session.ExecuteAsync(DeleteDetailSpecialsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId
            });
        }

        public Task<long> SetPxkAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoicePxkInfo pxk)
        {
            return session.QuerySingleAsync<long>(SetPxkQuery, new
            {
                p_PXK_ID = pxk.PXK_ID,
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_PXK_TYPE = pxk.PXK_TYPE,
                p_NBAN_DCHI = pxk.NBAN_DCHI,
                p_LDDNBO = pxk.LDDNBO,
                p_HDKTSO = pxk.HDKTSO,
                p_HDKTNGAY = pxk.HDKTNGAY,
                p_HVTNXHANG = pxk.HVTNXHANG,
                p_TNVCHUYEN = pxk.TNVCHUYEN,
                p_HDSO = pxk.HDSO,
                p_PTVCHUYEN = pxk.PTVCHUYEN,
                p_EXTRA_JSON = pxk.EXTRA_JSON,
                p_USERID = userId
            });
        }

        public Task<int> DeletePxkByInvoiceAsync(DapperSession session, string companyCd, long invoiceId, string userId)
        {
            return session.ExecuteAsync(DeletePxkQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_USERID = userId
            });
        }

        public Task<long> SetRelatedAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoiceRelatedInfo related)
        {
            return session.QuerySingleAsync<long>(SetRelatedQuery, new
            {
                p_RELATED_ID = related.RELATED_ID,
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_TCHDON = related.TCHDON,
                p_IS_EXTERNAL = related.IS_EXTERNAL,
                p_MSTCLQUAN = related.MSTCLQUAN,
                p_LHDCLQUAN = related.LHDCLQUAN,
                p_KHMSHDCLQUAN = related.KHMSHDCLQUAN,
                p_KHHDCLQUAN = related.KHHDCLQUAN,
                p_SHDCLQUAN = related.SHDCLQUAN,
                p_NLHDCLQUAN = related.NLHDCLQUAN,
                p_LDDCTTHE = related.LDDCTTHE,
                p_SBKCLQUAN = related.SBKCLQUAN,
                p_NBKCLQUAN = related.NBKCLQUAN,
                p_GCHU = related.GCHU,
                p_USERID = userId
            });
        }

        public Task<int> DeleteRelatedByInvoiceAsync(DapperSession session, string companyCd, long invoiceId)
        {
            return session.ExecuteAsync(DeleteRelatedQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId
            });
        }

        public Task<long> SetBkeInfoAsync(DapperSession session, string companyCd, string userId, long invoiceId, EInvoiceBkeInfo bke)
        {
            return session.QuerySingleAsync<long>(SetBkeInfoQuery, new
            {
                p_BKE_ID = bke.BKE_ID,
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_SELLER_ID = bke.SELLER_ID ?? 0,
                p_PBAN = bke.PBAN,
                p_TBKE = bke.TBKE,
                p_KHMBKE = bke.KHMBKE,
                p_SBKE = bke.SBKE,
                p_NBKE = bke.NBKE,
                p_TCHDON = bke.TCHDON,
                p_NBAN = bke.NBAN,
                p_MSTNBAN = bke.MSTNBAN,
                p_DCNBAN = bke.DCNBAN,
                p_TCTCNHANG = bke.TCTCNHANG,
                p_NMUA = bke.NMUA,
                p_MSTNMUA = bke.MSTNMUA,
                p_DCNMUA = bke.DCNMUA,
                p_TTKHAC_XML = bke.TTKHAC_XML,
                p_USERID = userId
            });
        }

        public async Task SetBkeReasonAsync(DapperSession session, long bkeId, EInvoiceBkeReason reason)
        {
            await session.QuerySingleAsync<long>(SetBkeReasonQuery, new
            {
                p_REASON_ID = reason.REASON_ID,
                p_BKE_ID = bkeId,
                p_SORT_ORDER = reason.SORT_ORDER,
                p_LDO = reason.LDO
            });
        }

        public async Task SetBkeDetailAsync(DapperSession session, long bkeId, EInvoiceBkeDetail detail)
        {
            await session.QuerySingleAsync<long>(SetBkeDetailQuery, new
            {
                p_DETAIL_ID = detail.DETAIL_ID,
                p_BKE_ID = bkeId,
                p_STT = detail.STT,
                p_REF_INVOICE_ID = detail.REF_INVOICE_ID ?? 0,
                p_KHMSHDON = detail.KHMSHDON,
                p_KHHDON = detail.KHHDON,
                p_SHDON = detail.SHDON,
                p_THHDVGOC = detail.THHDVGOC,
                p_SLGOC = detail.SLGOC,
                p_DGGOC = detail.DGGOC,
                p_THTGOC = detail.THTGOC,
                p_TSGOC = detail.TSGOC,
                p_TTGOC = detail.TTGOC,
                p_TGTKGOC = detail.TGTKGOC,
                p_TGTSTGOC = detail.TGTSTGOC,
                p_THHDVTDOI = detail.THHDVTDOI,
                p_SLTDOI = detail.SLTDOI,
                p_DGTDOI = detail.DGTDOI,
                p_THTTDOI = detail.THTTDOI,
                p_TSTDOI = detail.TSTDOI,
                p_TTTDOI = detail.TTTDOI,
                p_TGTTDOI = detail.TGTTDOI,
                p_TGTSTTDOI = detail.TGTSTTDOI,
                p_TGTCTCLECH = detail.TGTCTCLECH,
                p_TGTTCLECH = detail.TGTTCLECH,
                p_TGTKCLECH = detail.TGTKCLECH,
                p_TGTTTCLECH = detail.TGTTTCLECH,
                p_EXTRA_JSON = detail.EXTRA_JSON
            });
        }

        public Task DeleteBkeChildrenAsync(DapperSession session, long bkeId)
        {
            return session.ExecuteAsync(DeleteBkeChildrenQuery, new { p_BKE_ID = bkeId });
        }

        public Task DeleteBkeByInvoiceAsync(DapperSession session, string companyCd, long invoiceId, string userId)
        {
            return session.ExecuteAsync(DeleteBkeByInvoiceQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_USERID = userId
            });
        }

        public Task SetBkeSignatureAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            string signedXml,
            int isSigned)
        {
            return session.ExecuteAsync(SetBkeSignatureQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_SIGNED_XML = signedXml,
                p_IS_SIGNED = isSigned,
                p_USERID = userId
            });
        }

        public Task<int> SetSignatureAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            string xmlFtpPath,
            string? mtdiep,
            int? isSigned,
            string? errorMessage)
        {
            return session.ExecuteAsync(SetSignatureQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_XML_FTP_PATH = xmlFtpPath,
                p_MTDIEP = mtdiep,
                p_IS_SIGNED = isSigned,
                p_ERROR_MESSAGE = errorMessage,
                p_USERID = userId
            });
        }

        public async Task<EInvoiceSigningSequence> GetSigningSequenceAsync(
            DapperSession session,
            string companyCd,
            long invoiceId,
            string? khmsHDON,
            string? khhdon)
        {
            var result = await session.QuerySingleAsync<SigningSequenceResult>(GetNextShdonQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_KHMSHDON = khmsHDON,
                p_KHHDON = khhdon
            });

            var nextShdon = Common.NormalizeNullableText(result.NEXT_SHDON)
                ?? throw new InvalidOperationException("Failed to generate next e-invoice number");

            return new EInvoiceSigningSequence
            {
                NextShdon = nextShdon,
                LastNlap = result.LAST_NLAP
            };
        }

        public Task<int> SetShdonAsync(DapperSession session, string companyCd, string userId, long invoiceId, string shdon, DateTime nlap)
        {
            return session.ExecuteAsync(SetShdonQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_SHDON = shdon,
                p_NLAP = nlap.Date,
                p_USERID = userId
            });
        }

        public Task<int> RevertSigningReservationAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            string? shdon,
            DateTime? nlap)
        {
            return session.ExecuteAsync(RevertSigningReservationQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_SHDON = shdon,
                p_NLAP = nlap?.Date,
                p_USERID = userId
            });
        }

        private sealed class SigningSequenceResult
        {
            public string? NEXT_SHDON { get; set; }
            public DateTime? LAST_NLAP { get; set; }
        }

        public Task<int> DeleteDetailsByInvoiceAsync(DapperSession session, string companyCd, long invoiceId, string userId)
        {
            return session.ExecuteAsync(DeleteDetailsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_USERID = userId
            });
        }

        public Task<int> DeleteAsync(DapperSession session, string companyCd, long invoiceId, string userId)
        {
            return session.ExecuteAsync(DeleteQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_USERID = userId
            });
        }

        public async Task<int> GetTchdonAsync(string companyCd, long invoiceId, string? dbName = null)
        {
            var rows = await _db.QueryAsync<int?>(
                Net_DB.Net_DB_Company,
                GetTchdonQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INVOICE_ID = invoiceId
                },
                sDBName: dbName);

            return rows.FirstOrDefault() ?? 0;
        }

        private static object BuildHeaderParams(string companyCd, string userId, EInvoiceInfo invoice)
        {
            return new
            {
                p_INVOICE_ID = invoice.INVOICE_ID,
                p_COMPANY_CD = companyCd,
                p_SELLER_ID = invoice.SELLER_ID,
                p_XSL_ID = invoice.XSL_ID,
                p_PBAN = invoice.PBAN,
                p_THDON = invoice.THDON,
                p_KHMSHDON = invoice.KHMSHDON,
                p_KHHDON = invoice.KHHDON,
                p_SHDON = invoice.SHDON,
                p_MHSO = invoice.MHSO,
                p_NLAP = invoice.NLAP,
                p_HDCTTCHINH = invoice.HDCTTCHINH,
                p_SBKE = invoice.SBKE,
                p_NBKE = invoice.NBKE,
                p_DVTTE = invoice.DVTTE,
                p_TGIA = invoice.TGIA,
                p_HTTTOAN = invoice.HTTTOAN,
                p_MSTTCGP = invoice.MSTTCGP,
                p_TCHDON = invoice.TCHDON,
                p_SOURCE_INVOICE_ID = invoice.SOURCE_INVOICE_ID,
                p_NMUA_TEN = invoice.NMUA_TEN,
                p_NMUA_MST = invoice.NMUA_MST,
                p_NMUA_MDVQHNSACH = invoice.NMUA_MDVQHNSACH,
                p_NMUA_DCHI = invoice.NMUA_DCHI,
                p_NMUA_MTINH = invoice.NMUA_MTINH,
                p_NMUA_TTINH = invoice.NMUA_TTINH,
                p_NMUA_MXA = invoice.NMUA_MXA,
                p_NMUA_TXA = invoice.NMUA_TXA,
                p_NMUA_MKHANG = invoice.NMUA_MKHANG,
                p_NMUA_SDTHOAI = invoice.NMUA_SDTHOAI,
                p_NMUA_CCCDAN = invoice.NMUA_CCCDAN,
                p_NMUA_SHCHIEU = invoice.NMUA_SHCHIEU,
                p_NMUA_DCTDTU = invoice.NMUA_DCTDTU,
                p_NMUA_HVTNMHANG = invoice.NMUA_HVTNMHANG,
                p_NMUA_STKNHANG = invoice.NMUA_STKNHANG,
                p_NMUA_TNHANG = invoice.NMUA_TNHANG,
                p_TGTCTHUE = invoice.TGTCTHUE,
                p_TGTKCTHUE = invoice.TGTKCTHUE,
                p_TGTTTHUE = invoice.TGTTTHUE,
                p_TTCKTMAI = invoice.TTCKTMAI,
                p_CKTMAI_GCHU = invoice.CKTMAI_GCHU,
                p_TGTKHAC = invoice.TGTKHAC,
                p_TGTTTBSO = invoice.TGTTTBSO,
                p_TGTTTBCHU = invoice.TGTTTBCHU,
                p_TGTCTHUE_VND = invoice.TGTCTHUE_VND,
                p_TGTKCTHUE_VND = invoice.TGTKCTHUE_VND,
                p_TGTTTHUE_VND = invoice.TGTTTHUE_VND,
                p_TTCKTMAI_VND = invoice.TTCKTMAI_VND,
                p_TGTKHAC_VND = invoice.TGTKHAC_VND,
                p_TGTTTBSO_VND = invoice.TGTTTBSO_VND,
                p_DLQRCODE = invoice.DLQRCODE,
                p_MCCQT = invoice.MCCQT,
                p_MTRACUU = invoice.MTRACUU,
                p_MTDIEP = invoice.MTDIEP,
                p_MGDDTu = invoice.MGDDTu,
                p_TAX_SUMMARY_JSON = invoice.TAX_SUMMARY_JSON,
                p_FEE_JSON = invoice.FEE_JSON,
                p_EXTRA_JSON = invoice.EXTRA_JSON,
                p_ERROR_MESSAGE = invoice.ERROR_MESSAGE,
                p_DOC_VERSION = invoice.DOC_VERSION,
                p_USERID = userId
            };
        }

        public Task<int> UpdateBuyerEmailAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            string? buyerEmail)
        {
            return session.ExecuteAsync(SetBuyerEmailQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_NMUA_DCTDTU = buyerEmail,
                p_USERID = userId,
            });
        }

        public Task<int> SetMailStatusAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long invoiceId,
            int mailStatus)
        {
            return session.ExecuteAsync(SetMailStatusQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_INVOICE_ID = invoiceId,
                p_MAIL_STATUS = mailStatus,
                p_USERID = userId,
            });
        }
    }
}

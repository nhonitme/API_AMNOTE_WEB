namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceMapper
    {
        public static EInvoiceDto ToDto(EInvoiceInfo entity)
        {
            return new EInvoiceDto
            {
                INVOICE_ID = entity.INVOICE_ID,
                DOC_VERSION = entity.DOC_VERSION,
                COMPANY_CD = entity.COMPANY_CD,
                SELLER_ID = entity.SELLER_ID,
                XSL_ID = entity.XSL_ID,
                SELLER_NM = entity.SELLER_NM,
                SELLER_TAX_CD = entity.SELLER_TAX_CD,
                PBAN = entity.PBAN,
                THDON = entity.THDON,
                KHMSHDON = entity.KHMSHDON,
                KHHDON = entity.KHHDON,
                SHDON = entity.SHDON,
                MHSO = entity.MHSO,
                NLAP = entity.NLAP,
                HDCTTCHINH = entity.HDCTTCHINH,
                SBKE = entity.SBKE,
                NBKE = entity.NBKE,
                DVTTE = entity.DVTTE,
                TGIA = entity.TGIA,
                HTTTOAN = entity.HTTTOAN,
                MSTTCGP = entity.MSTTCGP,
                TCHDON = entity.TCHDON,
                NMUA_TEN = entity.NMUA_TEN,
                NMUA_MST = entity.NMUA_MST,
                NMUA_MDVQHNSACH = entity.NMUA_MDVQHNSACH,
                NMUA_DCHI = entity.NMUA_DCHI,
                NMUA_MTINH = entity.NMUA_MTINH,
                NMUA_TTINH = entity.NMUA_TTINH,
                NMUA_MXA = entity.NMUA_MXA,
                NMUA_TXA = entity.NMUA_TXA,
                NMUA_MKHANG = entity.NMUA_MKHANG,
                NMUA_SDTHOAI = entity.NMUA_SDTHOAI,
                NMUA_CCCDAN = entity.NMUA_CCCDAN,
                NMUA_SHCHIEU = entity.NMUA_SHCHIEU,
                NMUA_DCTDTU = entity.NMUA_DCTDTU,
                NMUA_HVTNMHANG = entity.NMUA_HVTNMHANG,
                NMUA_STKNHANG = entity.NMUA_STKNHANG,
                NMUA_TNHANG = entity.NMUA_TNHANG,
                TGTCTHUE = entity.TGTCTHUE,
                TGTKCTHUE = entity.TGTKCTHUE,
                TGTTTHUE = entity.TGTTTHUE,
                TTCKTMAI = entity.TTCKTMAI,
                CKTMAI_GCHU = entity.CKTMAI_GCHU,
                TGTKHAC = entity.TGTKHAC,
                TGTTTBSO = entity.TGTTTBSO,
                TGTTTBCHU = entity.TGTTTBCHU,
                TGTCTHUE_VND = entity.TGTCTHUE_VND,
                TGTKCTHUE_VND = entity.TGTKCTHUE_VND,
                TGTTTHUE_VND = entity.TGTTTHUE_VND,
                TTCKTMAI_VND = entity.TTCKTMAI_VND,
                TGTKHAC_VND = entity.TGTKHAC_VND,
                TGTTTBSO_VND = entity.TGTTTBSO_VND,
                DLQRCODE = entity.DLQRCODE,
                MCCQT = entity.MCCQT,
                MTRACUU = entity.MTRACUU,
                XML_FTP_PATH = entity.XML_FTP_PATH,
                MTDIEP = entity.MTDIEP,
                MGDDTu = entity.MGDDTu,
                TAX_SUMMARY_JSON = entity.TAX_SUMMARY_JSON,
                FEE_JSON = entity.FEE_JSON,
                EXTRA_JSON = entity.EXTRA_JSON,
                IS_SIGNED = entity.IS_SIGNED,
                INVOICE_STATUS = entity.INVOICE_STATUS,
                MAIL_STATUS = entity.MAIL_STATUS,
                SOURCE_INVOICE_ID = entity.SOURCE_INVOICE_ID,
                ERROR_MESSAGE = entity.ERROR_MESSAGE,
                ISDEL = entity.ISDEL,
                PXK_INFO = entity.PXK_INFO == null ? null : ToPxkDto(entity.PXK_INFO),
                RELATED = entity.RELATED == null ? null : ToRelatedDto(entity.RELATED),
                BKE_INFO = entity.BKE_INFO == null ? null : ToBkeDto(entity.BKE_INFO),
                DETAILS = entity.DETAILS.Select(ToDetailDto).ToList()
            };
        }

        public static EInvoiceBkeInfoDto ToBkeDto(EInvoiceBkeInfo entity)
        {
            return new EInvoiceBkeInfoDto
            {
                BKE_ID = entity.BKE_ID,
                COMPANY_CD = entity.COMPANY_CD,
                INVOICE_ID = entity.INVOICE_ID,
                SELLER_ID = entity.SELLER_ID,
                PBAN = entity.PBAN,
                TBKE = entity.TBKE,
                KHMBKE = entity.KHMBKE,
                SBKE = entity.SBKE,
                NBKE = entity.NBKE,
                TCHDON = entity.TCHDON,
                NBAN = entity.NBAN,
                MSTNBAN = entity.MSTNBAN,
                DCNBAN = entity.DCNBAN,
                TCTCNHANG = entity.TCTCNHANG,
                NMUA = entity.NMUA,
                MSTNMUA = entity.MSTNMUA,
                DCNMUA = entity.DCNMUA,
                TTKHAC_XML = entity.TTKHAC_XML,
                SIGNED_XML = entity.SIGNED_XML,
                IS_SIGNED = entity.IS_SIGNED,
                NMUA_IS_SIGNED = entity.NMUA_IS_SIGNED,
                ISDEL = entity.ISDEL,
                REASONS = entity.REASONS.Select(ToBkeReasonDto).ToList(),
                DETAILS = entity.DETAILS.Select(ToBkeDetailDto).ToList()
            };
        }

        public static EInvoiceBkeReasonDto ToBkeReasonDto(EInvoiceBkeReason entity)
        {
            return new EInvoiceBkeReasonDto
            {
                REASON_ID = entity.REASON_ID,
                BKE_ID = entity.BKE_ID,
                SORT_ORDER = entity.SORT_ORDER,
                LDO = entity.LDO,
                ISDEL = entity.ISDEL
            };
        }

        public static EInvoiceBkeDetailDto ToBkeDetailDto(EInvoiceBkeDetail entity)
        {
            return new EInvoiceBkeDetailDto
            {
                DETAIL_ID = entity.DETAIL_ID,
                BKE_ID = entity.BKE_ID,
                STT = entity.STT,
                REF_INVOICE_ID = entity.REF_INVOICE_ID,
                KHMSHDON = entity.KHMSHDON,
                KHHDON = entity.KHHDON,
                SHDON = entity.SHDON,
                THHDVGOC = entity.THHDVGOC,
                SLGOC = entity.SLGOC,
                DGGOC = entity.DGGOC,
                THTGOC = entity.THTGOC,
                TSGOC = entity.TSGOC,
                TTGOC = entity.TTGOC,
                TGTKGOC = entity.TGTKGOC,
                TGTSTGOC = entity.TGTSTGOC,
                THHDVTDOI = entity.THHDVTDOI,
                SLTDOI = entity.SLTDOI,
                DGTDOI = entity.DGTDOI,
                THTTDOI = entity.THTTDOI,
                TSTDOI = entity.TSTDOI,
                TTTDOI = entity.TTTDOI,
                TGTTDOI = entity.TGTTDOI,
                TGTSTTDOI = entity.TGTSTTDOI,
                TGTCTCLECH = entity.TGTCTCLECH,
                TGTTCLECH = entity.TGTTCLECH,
                TGTKCLECH = entity.TGTKCLECH,
                TGTTTCLECH = entity.TGTTTCLECH,
                EXTRA_JSON = entity.EXTRA_JSON,
                ISDEL = entity.ISDEL
            };
        }

        public static EInvoicePxkInfoDto ToPxkDto(EInvoicePxkInfo entity)
        {
            return new EInvoicePxkInfoDto
            {
                PXK_ID = entity.PXK_ID,
                INVOICE_ID = entity.INVOICE_ID,
                PXK_TYPE = entity.PXK_TYPE,
                NBAN_DCHI = entity.NBAN_DCHI,
                LDDNBO = entity.LDDNBO,
                HDKTSO = entity.HDKTSO,
                HDKTNGAY = entity.HDKTNGAY,
                HVTNXHANG = entity.HVTNXHANG,
                TNVCHUYEN = entity.TNVCHUYEN,
                HDSO = entity.HDSO,
                PTVCHUYEN = entity.PTVCHUYEN,
                EXTRA_JSON = entity.EXTRA_JSON,
                ISDEL = entity.ISDEL
            };
        }

        public static EInvoiceRelatedInfoDto ToRelatedDto(EInvoiceRelatedInfo entity)
        {
            return new EInvoiceRelatedInfoDto
            {
                RELATED_ID = entity.RELATED_ID,
                COMPANY_CD = entity.COMPANY_CD,
                INVOICE_ID = entity.INVOICE_ID,
                TCHDON = entity.TCHDON,
                IS_EXTERNAL = entity.IS_EXTERNAL,
                MSTCLQUAN = entity.MSTCLQUAN,
                LHDCLQUAN = entity.LHDCLQUAN,
                KHMSHDCLQUAN = entity.KHMSHDCLQUAN,
                KHHDCLQUAN = entity.KHHDCLQUAN,
                SHDCLQUAN = entity.SHDCLQUAN,
                NLHDCLQUAN = entity.NLHDCLQUAN,
                LDDCTTHE = entity.LDDCTTHE,
                SBKCLQUAN = entity.SBKCLQUAN,
                NBKCLQUAN = entity.NBKCLQUAN,
                GCHU = entity.GCHU,
            };
        }

        public static EInvoiceDetailSpecialInfoDto ToSpecialDto(EInvoiceDetailSpecialInfo entity)
        {
            return new EInvoiceDetailSpecialInfoDto
            {
                SPECIAL_ID = entity.SPECIAL_ID,
                INVOICE_ID = entity.INVOICE_ID,
                DETAIL_ID = entity.DETAIL_ID,
                COMPANY_CD = entity.COMPANY_CD,
                LHHDTRUNG = entity.LHHDTRUNG,
                SKHUNG = entity.SKHUNG,
                SMAY = entity.SMAY,
                BKSPT_VCHUYEN = entity.BKSPT_VCHUYEN,
                TNG_HANG = entity.TNG_HANG,
                DCNG_HANG = entity.DCNG_HANG,
                MSTNG_HANG = entity.MSTNG_HANG,
                MDDNG_HANG = entity.MDDNG_HANG,
                EXTRA_JSON = entity.EXTRA_JSON,
            };
        }

        public static EInvoiceDetailDto ToDetailDto(EInvoiceDetail entity)
        {
            return new EInvoiceDetailDto
            {
                DETAIL_ID = entity.DETAIL_ID,
                INVOICE_ID = entity.INVOICE_ID,
                COMPANY_CD = entity.COMPANY_CD,
                TCHAT = entity.TCHAT,
                STT = entity.STT,
                MHHDVU = entity.MHHDVU,
                THHDVU = entity.THHDVU,
                DVTINH = entity.DVTINH,
                SLUONG = entity.SLUONG,
                SLTHUCNHAP = entity.SLTHUCNHAP,
                DGIA = entity.DGIA,
                TLCKHAU = entity.TLCKHAU,
                STCKHAU = entity.STCKHAU,
                THTIEN = entity.THTIEN,
                TSUAT = entity.TSUAT,
                TTHUE = entity.TTHUE,
                TSAUTHUE = entity.TSAUTHUE,
                DGIA_VND = entity.DGIA_VND,
                STCKHAU_VND = entity.STCKHAU_VND,
                THTIEN_VND = entity.THTIEN_VND,
                TTHUE_VND = entity.TTHUE_VND,
                TSAUTHUE_VND = entity.TSAUTHUE_VND,
                SPECIAL = entity.SPECIAL == null ? null : ToSpecialDto(entity.SPECIAL),
                EXTRA_JSON = entity.EXTRA_JSON,
                ISDEL = entity.ISDEL
            };
        }

        public static EInvoiceSellerDto ToSellerDto(EInvoiceSellerInfo entity)
        {
            return new EInvoiceSellerDto
            {
                SELLER_ID = entity.SELLER_ID,
                COMPANY_CD = entity.COMPANY_CD,
                SELLER_CD = entity.SELLER_CD,
                SELLER_NM = entity.SELLER_NM,
                SELLER_TAX_CD = entity.SELLER_TAX_CD,
                MCCQT = entity.MCCQT,
                SELLER_ADDRESS = entity.SELLER_ADDRESS,
                MDDKDOANH = entity.MDDKDOANH,
                TDDKDOANH = entity.TDDKDOANH,
                DCDDKDOANH = entity.DCDDKDOANH,
                THDON = entity.THDON,
                KHMSHDON = entity.KHMSHDON,
                KHHDON = entity.KHHDON,
                FROM_SHDON = entity.FROM_SHDON,
                TO_SHDON = entity.TO_SHDON,
                MCHANG = entity.MCHANG,
                TCHANG = entity.TCHANG,
                SDTHOAI = entity.SDTHOAI,
                DCTDTU = entity.DCTDTU,
                STKNHANG = entity.STKNHANG,
                TNHANG = entity.TNHANG,
                FAX = entity.FAX,
                WEBSITE = entity.WEBSITE,
                LOGO_PATH = entity.LOGO_PATH,
                INVOICE_BACKGROUND_PATH = entity.INVOICE_BACKGROUND_PATH,
                INVOICE_BORDER_PATH = entity.INVOICE_BORDER_PATH,
                BACKGROUND_PATH = entity.BACKGROUND_PATH,
                USE_MULTI_TAX_RATE = entity.USE_MULTI_TAX_RATE,
                XSL_ID = entity.XSL_ID,
                XSL_TEMPLATE_NM = entity.XSL_TEMPLATE_NM,
                HAS_XSL_TEMPLATE = entity.HAS_XSL_TEMPLATE > 0
                    ? 1
                    : (!string.IsNullOrWhiteSpace(entity.XSL_CONTENT) ? 1 : 0),
                VERSION_NO = entity.VERSION_NO,
                XSL_IS_DEFAULT = entity.XSL_IS_DEFAULT,
                XSL_IS_ACTIVE = entity.XSL_IS_ACTIVE,
            };
        }

        public static EInvoiceInfo ToEntity(EInvoiceSaveRequest request, string companyCd)
        {
            return new EInvoiceInfo
            {
                INVOICE_ID = request.INVOICE_ID,
                DOC_VERSION = request.DOC_VERSION,
                COMPANY_CD = companyCd,
                SELLER_ID = request.SELLER_ID,
                XSL_ID = request.XSL_ID,
                THDON = request.THDON,
                KHMSHDON = request.KHMSHDON,
                KHHDON = request.KHHDON,
                SHDON = null,
                MHSO = request.MHSO,
                NLAP = request.NLAP,
                HDCTTCHINH = request.HDCTTCHINH,
                SBKE = request.SBKE,
                NBKE = request.NBKE,
                DVTTE = request.DVTTE,
                TGIA = request.TGIA,
                HTTTOAN = request.HTTTOAN,
                MSTTCGP = request.MSTTCGP,
                TCHDON = request.TCHDON,
                NMUA_TEN = request.NMUA_TEN,
                NMUA_MST = request.NMUA_MST,
                NMUA_MDVQHNSACH = request.NMUA_MDVQHNSACH,
                NMUA_DCHI = request.NMUA_DCHI,
                NMUA_MTINH = request.NMUA_MTINH,
                NMUA_TTINH = request.NMUA_TTINH,
                NMUA_MXA = request.NMUA_MXA,
                NMUA_TXA = request.NMUA_TXA,
                NMUA_MKHANG = request.NMUA_MKHANG,
                NMUA_SDTHOAI = request.NMUA_SDTHOAI,
                NMUA_CCCDAN = request.NMUA_CCCDAN,
                NMUA_SHCHIEU = request.NMUA_SHCHIEU,
                NMUA_DCTDTU = request.NMUA_DCTDTU,
                NMUA_HVTNMHANG = request.NMUA_HVTNMHANG,
                NMUA_STKNHANG = request.NMUA_STKNHANG,
                NMUA_TNHANG = request.NMUA_TNHANG,
                TGTCTHUE = request.TGTCTHUE,
                TGTKCTHUE = request.TGTKCTHUE,
                TGTTTHUE = request.TGTTTHUE,
                TTCKTMAI = request.TTCKTMAI,
                CKTMAI_GCHU = request.CKTMAI_GCHU,
                TGTKHAC = request.TGTKHAC,
                TGTTTBSO = request.TGTTTBSO,
                TGTTTBCHU = request.TGTTTBCHU,
                TGTCTHUE_VND = request.TGTCTHUE_VND,
                TGTKCTHUE_VND = request.TGTKCTHUE_VND,
                TGTTTHUE_VND = request.TGTTTHUE_VND,
                TTCKTMAI_VND = request.TTCKTMAI_VND,
                TGTKHAC_VND = request.TGTKHAC_VND,
                TGTTTBSO_VND = request.TGTTTBSO_VND,
                DLQRCODE = request.DLQRCODE,
                MCCQT = request.MCCQT,
                MTRACUU = request.MTRACUU,
                MTDIEP = request.MTDIEP,
                MGDDTu = request.MGDDTu,
                TAX_SUMMARY_JSON = request.TAX_SUMMARY_JSON,
                FEE_JSON = request.FEE_JSON,
                EXTRA_JSON = request.EXTRA_JSON,
                IS_SIGNED = request.IS_SIGNED,
                INVOICE_STATUS = request.INVOICE_STATUS,
                MAIL_STATUS = request.MAIL_STATUS,
                SOURCE_INVOICE_ID = request.SOURCE_INVOICE_ID,
                ERROR_MESSAGE = request.ERROR_MESSAGE,
                ISDEL = request.ISDEL,
                PXK_INFO = request.PXK_INFO == null ? null : ToPxkEntity(request.PXK_INFO, request.INVOICE_ID),
                RELATED = request.RELATED == null ? null : ToRelatedEntity(request.RELATED, companyCd, request.INVOICE_ID),
                BKE_INFO = request.BKE_INFO == null ? null : ToBkeEntity(request.BKE_INFO, companyCd, request.INVOICE_ID),
                DETAILS = request.DETAILS.Select(x => ToDetailEntity(x, companyCd, request.INVOICE_ID)).ToList()
            };
        }

        public static EInvoiceBkeInfo ToBkeEntity(EInvoiceBkeInfoDto request, string companyCd, long invoiceId)
        {
            return new EInvoiceBkeInfo
            {
                BKE_ID = request.BKE_ID,
                COMPANY_CD = companyCd,
                INVOICE_ID = invoiceId,
                SELLER_ID = request.SELLER_ID,
                PBAN = string.IsNullOrWhiteSpace(request.PBAN) ? "2.1.1" : request.PBAN,
                TBKE = request.TBKE ?? string.Empty,
                KHMBKE = string.IsNullOrWhiteSpace(request.KHMBKE) ? "01/BK-ĐCTT" : request.KHMBKE,
                SBKE = request.SBKE ?? string.Empty,
                NBKE = request.NBKE,
                TCHDON = request.TCHDON is 1 or 2 ? request.TCHDON : 1,
                NBAN = request.NBAN ?? string.Empty,
                MSTNBAN = request.MSTNBAN ?? string.Empty,
                DCNBAN = request.DCNBAN,
                TCTCNHANG = request.TCTCNHANG,
                NMUA = request.NMUA ?? string.Empty,
                MSTNMUA = request.MSTNMUA,
                DCNMUA = request.DCNMUA,
                TTKHAC_XML = request.TTKHAC_XML,
                SIGNED_XML = request.SIGNED_XML,
                IS_SIGNED = request.IS_SIGNED,
                NMUA_IS_SIGNED = request.NMUA_IS_SIGNED,
                ISDEL = request.ISDEL,
                REASONS = (request.REASONS ?? new List<EInvoiceBkeReasonDto>())
                    .Select(ToBkeReasonEntity)
                    .ToList(),
                DETAILS = (request.DETAILS ?? new List<EInvoiceBkeDetailDto>())
                    .Select(ToBkeDetailEntity)
                    .ToList()
            };
        }

        public static EInvoiceBkeReason ToBkeReasonEntity(EInvoiceBkeReasonDto request)
        {
            return new EInvoiceBkeReason
            {
                REASON_ID = request.REASON_ID,
                BKE_ID = request.BKE_ID,
                SORT_ORDER = request.SORT_ORDER,
                LDO = request.LDO ?? string.Empty,
                ISDEL = request.ISDEL
            };
        }

        public static EInvoiceBkeDetail ToBkeDetailEntity(EInvoiceBkeDetailDto request)
        {
            return new EInvoiceBkeDetail
            {
                DETAIL_ID = request.DETAIL_ID,
                BKE_ID = request.BKE_ID,
                STT = request.STT,
                REF_INVOICE_ID = request.REF_INVOICE_ID,
                KHMSHDON = request.KHMSHDON ?? string.Empty,
                KHHDON = request.KHHDON,
                SHDON = request.SHDON,
                THHDVGOC = request.THHDVGOC,
                SLGOC = request.SLGOC,
                DGGOC = request.DGGOC,
                THTGOC = request.THTGOC,
                TSGOC = request.TSGOC,
                TTGOC = request.TTGOC,
                TGTKGOC = request.TGTKGOC,
                TGTSTGOC = request.TGTSTGOC,
                THHDVTDOI = request.THHDVTDOI,
                SLTDOI = request.SLTDOI,
                DGTDOI = request.DGTDOI,
                THTTDOI = request.THTTDOI,
                TSTDOI = request.TSTDOI,
                TTTDOI = request.TTTDOI,
                TGTTDOI = request.TGTTDOI,
                TGTSTTDOI = request.TGTSTTDOI,
                TGTCTCLECH = request.TGTCTCLECH,
                TGTTCLECH = request.TGTTCLECH,
                TGTKCLECH = request.TGTKCLECH,
                TGTTTCLECH = request.TGTTTCLECH,
                EXTRA_JSON = request.EXTRA_JSON,
                ISDEL = request.ISDEL
            };
        }

        public static EInvoicePxkInfo ToPxkEntity(EInvoicePxkInfoDto request, long invoiceId)
        {
            return new EInvoicePxkInfo
            {
                PXK_ID = request.PXK_ID,
                INVOICE_ID = invoiceId,
                PXK_TYPE = request.PXK_TYPE,
                NBAN_DCHI = request.NBAN_DCHI,
                LDDNBO = request.LDDNBO,
                HDKTSO = request.HDKTSO,
                HDKTNGAY = request.HDKTNGAY,
                HVTNXHANG = request.HVTNXHANG,
                TNVCHUYEN = request.TNVCHUYEN,
                HDSO = request.HDSO,
                PTVCHUYEN = request.PTVCHUYEN,
                EXTRA_JSON = request.EXTRA_JSON,
                ISDEL = request.ISDEL
            };
        }

        public static EInvoiceRelatedInfo ToRelatedEntity(EInvoiceRelatedInfoDto request, string companyCd, long invoiceId)
        {
            return new EInvoiceRelatedInfo
            {
                RELATED_ID = request.RELATED_ID,
                COMPANY_CD = companyCd,
                INVOICE_ID = invoiceId,
                TCHDON = request.TCHDON,
                IS_EXTERNAL = request.IS_EXTERNAL,
                MSTCLQUAN = request.MSTCLQUAN,
                LHDCLQUAN = request.LHDCLQUAN,
                KHMSHDCLQUAN = request.KHMSHDCLQUAN,
                KHHDCLQUAN = request.KHHDCLQUAN,
                SHDCLQUAN = request.SHDCLQUAN,
                NLHDCLQUAN = request.NLHDCLQUAN,
                LDDCTTHE = request.LDDCTTHE,
                SBKCLQUAN = request.SBKCLQUAN,
                NBKCLQUAN = request.NBKCLQUAN,
                GCHU = request.GCHU,
            };
        }

        public static EInvoiceDetailSpecialInfo ToSpecialEntity(
            EInvoiceDetailSpecialInfoDto? request,
            string companyCd,
            long invoiceId,
            long detailId)
        {
            if (request == null)
            {
                return new EInvoiceDetailSpecialInfo
                {
                    COMPANY_CD = companyCd,
                    INVOICE_ID = invoiceId,
                    DETAIL_ID = detailId
                };
            }

            return new EInvoiceDetailSpecialInfo
            {
                SPECIAL_ID = request.SPECIAL_ID,
                COMPANY_CD = companyCd,
                INVOICE_ID = invoiceId,
                DETAIL_ID = detailId,
                LHHDTRUNG = request.LHHDTRUNG,
                SKHUNG = request.SKHUNG,
                SMAY = request.SMAY,
                BKSPT_VCHUYEN = request.BKSPT_VCHUYEN,
                TNG_HANG = request.TNG_HANG,
                DCNG_HANG = request.DCNG_HANG,
                MSTNG_HANG = request.MSTNG_HANG,
                MDDNG_HANG = request.MDDNG_HANG,
                EXTRA_JSON = request.EXTRA_JSON,
            };
        }

        public static EInvoiceDetail ToDetailEntity(EInvoiceDetailDto request, string companyCd, long invoiceId)
        {
            return new EInvoiceDetail
            {
                DETAIL_ID = request.DETAIL_ID,
                INVOICE_ID = invoiceId,
                COMPANY_CD = companyCd,
                TCHAT = request.TCHAT,
                STT = request.STT,
                MHHDVU = request.MHHDVU,
                THHDVU = request.THHDVU,
                DVTINH = request.DVTINH,
                SLUONG = request.SLUONG,
                SLTHUCNHAP = request.SLTHUCNHAP,
                DGIA = request.DGIA,
                TLCKHAU = request.TLCKHAU,
                STCKHAU = request.STCKHAU,
                THTIEN = request.THTIEN,
                TSUAT = request.TSUAT,
                TTHUE = request.TTHUE,
                TSAUTHUE = request.TSAUTHUE,
                DGIA_VND = request.DGIA_VND,
                STCKHAU_VND = request.STCKHAU_VND,
                THTIEN_VND = request.THTIEN_VND,
                TTHUE_VND = request.TTHUE_VND,
                TSAUTHUE_VND = request.TSAUTHUE_VND,
                SPECIAL = request.SPECIAL == null
                    ? null
                    : ToSpecialEntity(request.SPECIAL, companyCd, invoiceId, request.DETAIL_ID),
                EXTRA_JSON = request.EXTRA_JSON,
                ISDEL = request.ISDEL
            };
        }
    }
}

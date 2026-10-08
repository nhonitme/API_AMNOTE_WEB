using API_AMNOTE_WEB.Helpers;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceDto
    {
        public long INVOICE_ID { get; set; }
        public int DOC_VERSION { get; set; } = 1;
        public string COMPANY_CD { get; set; } = string.Empty;
        public long? SELLER_ID { get; set; }
        public long? XSL_ID { get; set; }
        public string? SELLER_NM { get; set; }
        public string? SELLER_TAX_CD { get; set; }
        public string PBAN { get; set; } = "2.1.0";
        public string? THDON { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        public string? MHSO { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NLAP { get; set; }
        public int HDCTTCHINH { get; set; }
        public string? SBKE { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NBKE { get; set; }
        public string DVTTE { get; set; } = "VND";
        public decimal? TGIA { get; set; } = 1m;
        public string? HTTTOAN { get; set; }
        public string? MSTTCGP { get; set; }
        public int TCHDON { get; set; }
        public string? NMUA_TEN { get; set; }
        public string? NMUA_MST { get; set; }
        public string? NMUA_MDVQHNSACH { get; set; }
        public string? NMUA_DCHI { get; set; }
        public string? NMUA_MTINH { get; set; }
        public string? NMUA_TTINH { get; set; }
        public string? NMUA_MXA { get; set; }
        public string? NMUA_TXA { get; set; }
        public string? NMUA_MKHANG { get; set; }
        public string? NMUA_SDTHOAI { get; set; }
        public string? NMUA_CCCDAN { get; set; }
        public string? NMUA_SHCHIEU { get; set; }
        public string? NMUA_DCTDTU { get; set; }
        public string? NMUA_HVTNMHANG { get; set; }
        public string? NMUA_STKNHANG { get; set; }
        public string? NMUA_TNHANG { get; set; }
        public decimal? TGTCTHUE { get; set; }
        public decimal? TGTKCTHUE { get; set; }
        public decimal? TGTTTHUE { get; set; }
        public decimal? TTCKTMAI { get; set; }
        public string? CKTMAI_GCHU { get; set; }
        public decimal? TGTKHAC { get; set; }
        public decimal? TGTTTBSO { get; set; }
        public string? TGTTTBCHU { get; set; }
        public decimal? TGTCTHUE_VND { get; set; }
        public decimal? TGTKCTHUE_VND { get; set; }
        public decimal? TGTTTHUE_VND { get; set; }
        public decimal? TTCKTMAI_VND { get; set; }
        public decimal? TGTKHAC_VND { get; set; }
        public decimal? TGTTTBSO_VND { get; set; }
        public string? DLQRCODE { get; set; }
        public string? MCCQT { get; set; }
        public string? MTRACUU { get; set; }
        public string? XML_FTP_PATH { get; set; }
        public string? MTDIEP { get; set; }
        public string? MGDDTu { get; set; }
        public string? TAX_SUMMARY_JSON { get; set; }
        public string? FEE_JSON { get; set; }
        public string? EXTRA_JSON { get; set; }
        public int IS_SIGNED { get; set; }
        public int INVOICE_STATUS { get; set; }
        public int MAIL_STATUS { get; set; }
        public long? SOURCE_INVOICE_ID { get; set; }
        public string? ERROR_MESSAGE { get; set; }
        public int ISDEL { get; set; }
        public EInvoicePxkInfoDto? PXK_INFO { get; set; }
        public EInvoiceRelatedInfoDto? RELATED { get; set; }
        public EInvoiceBkeInfoDto? BKE_INFO { get; set; }
        public List<EInvoiceDetailDto> DETAILS { get; set; } = new();
    }

    public class EInvoicePxkInfoDto
    {
        public long PXK_ID { get; set; }
        public long INVOICE_ID { get; set; }
        public string PXK_TYPE { get; set; } = string.Empty;
        public string? NBAN_DCHI { get; set; }
        public string? LDDNBO { get; set; }
        public string? HDKTSO { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? HDKTNGAY { get; set; }
        public string? HVTNXHANG { get; set; }
        public string? TNVCHUYEN { get; set; }
        public string? HDSO { get; set; }
        public string? PTVCHUYEN { get; set; }
        public string? EXTRA_JSON { get; set; }
        public int ISDEL { get; set; }
    }

    public class EInvoiceRelatedInfoDto
    {
        public long RELATED_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long INVOICE_ID { get; set; }
        public int TCHDON { get; set; }
        public int IS_EXTERNAL { get; set; }
        public string? MSTCLQUAN { get; set; }
        public int? LHDCLQUAN { get; set; }
        public string? KHMSHDCLQUAN { get; set; }
        public string? KHHDCLQUAN { get; set; }
        public string? SHDCLQUAN { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NLHDCLQUAN { get; set; }
        public int? LDDCTTHE { get; set; }
        public string? SBKCLQUAN { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NBKCLQUAN { get; set; }
        public string? GCHU { get; set; }
    }

    public class EInvoiceDetailSpecialInfoDto
    {
        public long SPECIAL_ID { get; set; }
        public long INVOICE_ID { get; set; }
        public long DETAIL_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public int LHHDTRUNG { get; set; }
        public string? SKHUNG { get; set; }
        public string? SMAY { get; set; }
        public string? BKSPT_VCHUYEN { get; set; }
        public string? TNG_HANG { get; set; }
        public string? DCNG_HANG { get; set; }
        public string? MSTNG_HANG { get; set; }
        public string? MDDNG_HANG { get; set; }
        public string? EXTRA_JSON { get; set; }
    }

    public class EInvoiceDetailDto
    {
        public long DETAIL_ID { get; set; }
        public long INVOICE_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public int? TCHAT { get; set; }
        public int? STT { get; set; }
        public string? MHHDVU { get; set; }
        public string? THHDVU { get; set; }
        public string? DVTINH { get; set; }
        public decimal? SLUONG { get; set; }
        public decimal? SLTHUCNHAP { get; set; }
        public decimal? DGIA { get; set; }
        public decimal? TLCKHAU { get; set; }
        public decimal? STCKHAU { get; set; }
        public decimal? THTIEN { get; set; }
        public string? TSUAT { get; set; }
        public decimal? TTHUE { get; set; }
        public decimal? TSAUTHUE { get; set; }
        public decimal? DGIA_VND { get; set; }
        public decimal? STCKHAU_VND { get; set; }
        public decimal? THTIEN_VND { get; set; }
        public decimal? TTHUE_VND { get; set; }
        public decimal? TSAUTHUE_VND { get; set; }
        public EInvoiceDetailSpecialInfoDto? SPECIAL { get; set; }
        public string? EXTRA_JSON { get; set; }
        public int ISDEL { get; set; }
    }

    public class EInvoicePublicLookupDto
    {
        public string? MTRACUU { get; set; }
        public string? THDON { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NLAP { get; set; }
        public string DVTTE { get; set; } = "VND";
        public string? SELLER_NM { get; set; }
        public string? SELLER_TAX_CD { get; set; }
        public string? NMUA_TEN { get; set; }
        public string? NMUA_MST { get; set; }
        public string? NMUA_DCHI { get; set; }
        public decimal? TGTCTHUE { get; set; }
        public decimal? TGTTTHUE { get; set; }
        public decimal? TTCKTMAI { get; set; }
        public decimal? TGTTTBSO { get; set; }
        public string? TGTTTBCHU { get; set; }
        public string? MCCQT { get; set; }
        public bool CAN_DOWNLOAD_PDF { get; set; } = true;
        public bool CAN_DOWNLOAD_XML { get; set; } = true;
        public string? HTML { get; set; }
        public List<EInvoicePublicDetailDto> DETAILS { get; set; } = new();
    }

    public class EInvoicePublicDetailDto
    {
        public int? STT { get; set; }
        public int? TCHAT { get; set; }
        public string? MHHDVU { get; set; }
        public string? THHDVU { get; set; }
        public string? DVTINH { get; set; }
        public decimal? SLUONG { get; set; }
        public decimal? DGIA { get; set; }
        public decimal? STCKHAU { get; set; }
        public decimal? THTIEN { get; set; }
        public string? TSUAT { get; set; }
    }

    public class EInvoicePublicDownloadInfo
    {
        public long INVOICE_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? DB_NAME { get; set; }
        public string? MTRACUU { get; set; }
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
    }

    public class EInvoiceSaveRequest : EInvoiceDto
    {
    }

    public class EInvoiceSigningPayloadDto
    {
        public long INVOICE_ID { get; set; }
        public string RAW_XML { get; set; } = string.Empty;
        /// <summary>XML bảng kê 01/BK-ĐCTT (chưa ký) khi HĐ TCHDon 3/4.</summary>
        public string? BKE_RAW_XML { get; set; }
        public bool REQUIRES_BKE { get; set; }
        public bool IS_SIGNED { get; set; }
    }

    public class EInvoiceSignRequest
    {
        public string? XML { get; set; }
        /// <summary>XML bảng kê đã ký (bắt buộc khi HĐ TCHDon 3/4).</summary>
        public string? BKE_XML { get; set; }
        public string? CERTIFICATE_SUBJECT { get; set; }
        public string? CERTIFICATE_THUMBPRINT { get; set; }
        public string? CERTIFICATE_SERIAL_NUMBER { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? SIGNED_AT { get; set; }
    }

    public class EInvoiceSaveSignedXmlRequest
    {
        [JsonPropertyName("rawXml")]
        public string? RawXml { get; set; }

        [JsonPropertyName("signedXml")]
        public string? SignedXml { get; set; }

        [JsonPropertyName("bkeRawXml")]
        public string? BkeRawXml { get; set; }

        [JsonPropertyName("bkeSignedXml")]
        public string? BkeSignedXml { get; set; }

        [JsonPropertyName("certificateSubject")]
        public string? CertificateSubject { get; set; }

        [JsonPropertyName("certificateThumbprint")]
        public string? CertificateThumbprint { get; set; }

        [JsonPropertyName("certificateSerialNumber")]
        public string? CertificateSerialNumber { get; set; }

        [JsonPropertyName("signedAt")]
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? SignedAt { get; set; }

        public EInvoiceSignRequest ToSignRequest()
        {
            return new EInvoiceSignRequest
            {
                XML = SignedXml ?? RawXml,
                BKE_XML = BkeSignedXml ?? BkeRawXml,
                CERTIFICATE_SUBJECT = CertificateSubject,
                CERTIFICATE_THUMBPRINT = CertificateThumbprint,
                CERTIFICATE_SERIAL_NUMBER = CertificateSerialNumber,
                SIGNED_AT = SignedAt,
            };
        }
    }

    public class EInvoiceDeleteManyRequest
    {
        public List<long> InvoiceIds { get; set; } = new();
    }

    public class EInvoiceSearchRequest
    {
        public bool? CashRegister { get; set; }
        public long? InvoiceId { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? FromDate { get; set; }

        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? ToDate { get; set; }
        public string? Keyword { get; set; }
        public string? Khhdon { get; set; }
        public string? KhhdonOp { get; set; }
        public string? ShdonFrom { get; set; }
        public string? ShdonTo { get; set; }
        public string? NmuaTen { get; set; }
        public string? NmuaTenOp { get; set; }
        public string? NmuaMst { get; set; }
        public string? NmuaMstOp { get; set; }
        public int? InvoiceStatus { get; set; }
        public int? CqtStatus { get; set; }
        public int? IsSigned { get; set; }
        public int? Tchdon { get; set; }
        public int? MailStatus { get; set; }
        public bool IncludeDetails { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; }
    }

    public class EInvoiceSellerDto
    {
        public long SELLER_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string SELLER_CD { get; set; } = string.Empty;
        public string SELLER_NM { get; set; } = string.Empty;
        public string? MCCQT { get; set; }
        public string SELLER_TAX_CD { get; set; } = string.Empty;
        public string SELLER_ADDRESS { get; set; } = string.Empty;
        public string? MDDKDOANH { get; set; }
        public string? TDDKDOANH { get; set; }
        public string? DCDDKDOANH { get; set; }
        public string? THDON { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? FROM_SHDON { get; set; }
        public string? TO_SHDON { get; set; }
        public string? MCHANG { get; set; }
        public string? TCHANG { get; set; }
        public string? SDTHOAI { get; set; }
        public string? DCTDTU { get; set; }
        public string? STKNHANG { get; set; }
        public string? TNHANG { get; set; }
        public string? FAX { get; set; }
        public string? WEBSITE { get; set; }
        public string? LOGO_PATH { get; set; }
        public string? INVOICE_BACKGROUND_PATH { get; set; }
        public string? INVOICE_BORDER_PATH { get; set; }
        public string? BACKGROUND_PATH { get; set; }
        public int USE_MULTI_TAX_RATE { get; set; }
        public long XSL_ID { get; set; }
        public string? XSL_TEMPLATE_NM { get; set; }
        public int HAS_XSL_TEMPLATE { get; set; }
        public int VERSION_NO { get; set; }
        public int XSL_IS_DEFAULT { get; set; }
        public int XSL_IS_ACTIVE { get; set; }
    }
}

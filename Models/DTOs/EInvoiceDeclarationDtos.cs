using API_AMNOTE_WEB.Helpers;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceDeclarationDto
    {
        public long TKHAI_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string PBAN { get; set; } = "2.1.1";
        public string? MSO { get; set; }
        public string? TEN { get; set; }
        public int? HTHUC { get; set; }
        public string? TNNT { get; set; }
        public string? MST { get; set; }
        public string? CQTQLY { get; set; }
        public string? MCQTQLY { get; set; }
        public string? TNDDPLUAT { get; set; }
        public string? DTDDPLUAT { get; set; }
        public string? CCCDAN { get; set; }
        public string? SHCHIEU { get; set; }
        public string? MQTNDDPLUAT { get; set; }
        public string? QTICH { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NSDDPLUAT { get; set; }
        public int? GTINH { get; set; }
        public string? DCLHE { get; set; }
        public string? DCTDTU { get; set; }
        public string? NLHE { get; set; }
        public string? DTLHE { get; set; }
        public string? DDANH { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NLAP { get; set; }
        public int CMA { get; set; }
        public int CMTMTTIEN { get; set; }
        public int KCMTMTTIEN { get; set; }
        public int KCMA { get; set; }
        public int NNTDBKKHAN { get; set; }
        public int NNTKTDNUBND { get; set; }
        public int CQXLTSCONG { get; set; }
        public int CDLTTDCQT { get; set; }
        public int CDLQTCTN { get; set; }
        public int TCNNGOAI { get; set; }
        public int CDDU { get; set; }
        public int CDLTHDTHU { get; set; }
        public int CBTHOP { get; set; }
        public int CTTCTGDICH { get; set; }
        public int HDGTGT { get; set; }
        public int HDGTGTTHBLAI { get; set; }
        public int HDBHANG { get; set; }
        public int HDBHTHBLAI { get; set; }
        public int HDTMAI { get; set; }
        public int HDNCCNNGOAI { get; set; }
        public int HDBTSCONG { get; set; }
        public int HDBHDTQGIA { get; set; }
        public int HDKHAC { get; set; }
        public int CTU { get; set; }
        public string? MGDDTU { get; set; }
        public string? MTDIEP { get; set; }
        public string? XML { get; set; }
        public int IS_SIGNED { get; set; }
        public int CQT_STATUS { get; set; }
        public string? MCCQT { get; set; }
        public string? ERROR_MESSAGE { get; set; }
        public int ISDEL { get; set; }
        public List<EInvoiceDeclarationDetailDto> DETAILS { get; set; } = new();
    }

    public class EInvoiceDeclarationDetailDto
    {
        public long DETAIL_ID { get; set; }
        public long TKHAI_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string DETAIL_TYPE { get; set; } = string.Empty;
        public int? STT { get; set; }
        public string? TTCHUC { get; set; }
        public string? SERI { get; set; }
        public int? CTS_HTHUC { get; set; }
        public string? TTCGP { get; set; }
        public string? MSTTCGP { get; set; }
        public string? TTCTN { get; set; }
        public string? MSTTCTN { get; set; }
        public string? TDVHTPT { get; set; }
        public string? MSTDVHTPT { get; set; }
        public string? TDVI { get; set; }
        public string? MSTDUQ { get; set; }
        public int? HDBRMVAO { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? TDLHDTNGAY { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? TDLHDDNGAY { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? TGUQTNGAY { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? TGUQDNGAY { get; set; }
        public string? TLHDON { get; set; }
        public int? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? TENDKTH { get; set; }
        public string? MSTDKTH { get; set; }
        public string? MDICH { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? TNGAY { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? DNGAY { get; set; }
        public string? GCHU { get; set; }
        public string? RAW_DETAIL_XML { get; set; }
        public int ISDEL { get; set; }
    }

    public class EInvoiceDeclarationSaveRequest : EInvoiceDeclarationDto
    {
    }

    public class EInvoiceDeclarationSearchRequest
    {
        public long? TkhaiId { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? FromDate { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? ToDate { get; set; }
        public string? Keyword { get; set; }
        public int? IsSigned { get; set; }
        public bool IncludeDetails { get; set; }
    }

    public class EInvoiceDeclarationSigningPayloadDto
    {
        public long TKHAI_ID { get; set; }
        public string RAW_XML { get; set; } = string.Empty;
        public bool IS_SIGNED { get; set; }
    }

    public class EInvoiceDeclarationSignRequest
    {
        public string? XML { get; set; }
        public string? CERTIFICATE_SUBJECT { get; set; }
        public string? CERTIFICATE_THUMBPRINT { get; set; }
        public string? CERTIFICATE_SERIAL_NUMBER { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? SIGNED_AT { get; set; }
    }

    public class EInvoiceDeclarationDeleteManyRequest
    {
        public List<long> TkhaiIds { get; set; } = new();
    }
}

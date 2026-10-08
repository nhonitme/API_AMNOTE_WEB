using API_AMNOTE_WEB.Helpers;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceErrorNoticeDto
    {
        public long TBAO_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string PBAN { get; set; } = "2.1.0";
        public string MSO { get; set; } = "04/SS-HĐĐT";
        public string TEN { get; set; } = "Thông báo hóa đơn điện tử có sai sót";
        public int LOAI { get; set; } = 1;
        public string MCQT { get; set; } = string.Empty;
        public string TCQT { get; set; } = string.Empty;
        public string? SO { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NTBCCQT { get; set; }
        public string? MST { get; set; }
        public string TNNT { get; set; } = string.Empty;
        public string DDANH { get; set; } = string.Empty;
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NTBAO { get; set; }
        public string? XML { get; set; }
        public int IS_SIGNED { get; set; }
        public int IS_MAIL { get; set; }
        public string? MGDDTU { get; set; }
        public string? MTDIEP { get; set; }
        public string? ERROR_MESSAGE { get; set; }
        public int ISDEL { get; set; }
        public List<EInvoiceErrorNoticeDetailDto> DETAILS { get; set; } = new();
    }

    public class EInvoiceErrorNoticeDetailDto
    {
        public long DETAIL_ID { get; set; }
        public long TBAO_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public int? STT { get; set; }
        public string? MCCQT { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NGAY { get; set; }
        public int LADHDDT { get; set; }
        public string? LDO { get; set; }
        public int ISDEL { get; set; }
    }

    public class EInvoiceErrorNoticeSaveRequest : EInvoiceErrorNoticeDto
    {
    }

    public class EInvoiceErrorNoticeSearchRequest
    {
        public long? TbaoId { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? FromDate { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? ToDate { get; set; }
        public string? Keyword { get; set; }
        public int? IsSigned { get; set; }
        public bool IncludeDetails { get; set; }
    }

    public class EInvoiceErrorNoticeSigningPayloadDto
    {
        public long TBAO_ID { get; set; }
        public string RAW_XML { get; set; } = string.Empty;
        public bool IS_SIGNED { get; set; }
    }

    public class EInvoiceErrorNoticeSignRequest
    {
        public string? XML { get; set; }
        public string? CERTIFICATE_SUBJECT { get; set; }
        public string? CERTIFICATE_THUMBPRINT { get; set; }
        public string? CERTIFICATE_SERIAL_NUMBER { get; set; }
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? SIGNED_AT { get; set; }
    }

    public class EInvoiceErrorNoticeDeleteManyRequest
    {
        public List<long> TbaoIds { get; set; } = new();
    }
}

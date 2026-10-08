using API_AMNOTE_WEB.Helpers;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceBkeInfoDto
    {
        public long BKE_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long INVOICE_ID { get; set; }
        public long? SELLER_ID { get; set; }
        public string PBAN { get; set; } = "2.1.1";
        public string TBKE { get; set; } = string.Empty;
        public string KHMBKE { get; set; } = "01/BK-ĐCTT";
        public string SBKE { get; set; } = string.Empty;
        [JsonConverter(typeof(NullableDateTimeJsonConverter))]
        public DateTime? NBKE { get; set; }
        public int TCHDON { get; set; } = 1;
        public string NBAN { get; set; } = string.Empty;
        public string MSTNBAN { get; set; } = string.Empty;
        public string? DCNBAN { get; set; }
        public int TCTCNHANG { get; set; }
        public string NMUA { get; set; } = string.Empty;
        public string? MSTNMUA { get; set; }
        public string? DCNMUA { get; set; }
        public string? TTKHAC_XML { get; set; }
        public string? SIGNED_XML { get; set; }
        public int IS_SIGNED { get; set; }
        public int NMUA_IS_SIGNED { get; set; }
        public int ISDEL { get; set; }
        public List<EInvoiceBkeReasonDto> REASONS { get; set; } = new();
        public List<EInvoiceBkeDetailDto> DETAILS { get; set; } = new();
    }

    public class EInvoiceBkeReasonDto
    {
        public long REASON_ID { get; set; }
        public long BKE_ID { get; set; }
        public int SORT_ORDER { get; set; } = 1;
        public string LDO { get; set; } = string.Empty;
        public int ISDEL { get; set; }
    }

    public class EInvoiceBkeDetailDto
    {
        public long DETAIL_ID { get; set; }
        public long BKE_ID { get; set; }
        public int? STT { get; set; }
        public long? REF_INVOICE_ID { get; set; }
        public string KHMSHDON { get; set; } = string.Empty;
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        public string? THHDVGOC { get; set; }
        public decimal? SLGOC { get; set; }
        public decimal? DGGOC { get; set; }
        public decimal? THTGOC { get; set; }
        public string? TSGOC { get; set; }
        public decimal? TTGOC { get; set; }
        public decimal? TGTKGOC { get; set; }
        public decimal? TGTSTGOC { get; set; }
        public string? THHDVTDOI { get; set; }
        public decimal? SLTDOI { get; set; }
        public decimal? DGTDOI { get; set; }
        public decimal? THTTDOI { get; set; }
        public string? TSTDOI { get; set; }
        public decimal? TTTDOI { get; set; }
        public decimal? TGTTDOI { get; set; }
        public decimal? TGTSTTDOI { get; set; }
        public decimal? TGTCTCLECH { get; set; }
        public decimal? TGTTCLECH { get; set; }
        public decimal? TGTKCLECH { get; set; }
        public decimal? TGTTTCLECH { get; set; }
        public string? EXTRA_JSON { get; set; }
        public int ISDEL { get; set; }
    }
}

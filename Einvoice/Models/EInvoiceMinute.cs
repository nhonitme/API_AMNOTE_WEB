namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceMinuteInfo
    {
        public long BBAN_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long? SELLER_ID { get; set; }
        public long? INVOICE_ID { get; set; }
        public long? REF_INVOICE_ID { get; set; }
        public string PBAN { get; set; } = "2.1.0";
        public string TBBAN { get; set; } = "Biên bản điều chỉnh hóa đơn";
        public string SBBAN { get; set; } = string.Empty;
        public DateTime? NBBAN { get; set; }
        public int TCHDON { get; set; } = 2;
        public string NBAN { get; set; } = string.Empty;
        public string MSTNBAN { get; set; } = string.Empty;
        public string? DCNBAN { get; set; }
        public string NMUA { get; set; } = string.Empty;
        public string? MSTNMUA { get; set; }
        public string? DCNMUA { get; set; }
        public string KHMSHDON { get; set; } = string.Empty;
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        public DateTime? NLAP { get; set; }
        public string DVTTE { get; set; } = "VND";
        public decimal? TGIA { get; set; } = 1m;
        public string? MTRACUU { get; set; }
        public string? TTKHAC_XML { get; set; }
        public string? NDBBAN_XML { get; set; }
        public string? SIGNED_XML { get; set; }
        public int IS_SIGNED { get; set; }
        public int NMUA_IS_SIGNED { get; set; }
        public DateTime? NMUA_SIGN_DT { get; set; }
        public int IS_MAIL { get; set; }
        public string? CHECKSUM { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        public int ISDEL { get; set; }
        public decimal? TOTAL_AFTER_TTHUE { get; set; }
        public List<EInvoiceMinuteReason> REASONS { get; set; } = new();
        public List<EInvoiceMinuteLine> LINES_BEFORE { get; set; } = new();
        public List<EInvoiceMinuteLine> LINES_AFTER { get; set; } = new();
        public EInvoiceMinuteLine? TOTAL_BEFORE { get; set; }
        public EInvoiceMinuteLine? TOTAL_AFTER { get; set; }
    }

    public class EInvoiceMinuteReason
    {
        public long REASON_ID { get; set; }
        public long BBAN_ID { get; set; }
        public int SORT_ORDER { get; set; } = 1;
        public string LDO { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        public int ISDEL { get; set; }
    }

    public class EInvoiceMinuteLine
    {
        public long REASON_ID { get; set; }
        public long BBAN_ID { get; set; }
        public long? DETAIL_ID { get; set; }
        public int LINE_SIDE { get; set; }
        public int SORT_ORDER { get; set; } = 1;
        public string LDO { get; set; } = string.Empty;
        public int? TCHAT { get; set; }
        public int? STT { get; set; }
        public string? MHHDVU { get; set; }
        public string? THHDVU { get; set; }
        public string? DVTINH { get; set; }
        public decimal? SLUONG { get; set; }
        public decimal? DGIA { get; set; }
        public decimal? TLCKHAU { get; set; }
        public decimal? STCKHAU { get; set; }
        public decimal? THTIEN { get; set; }
        public string? TSUAT { get; set; }
        public decimal? TTHUE { get; set; }
        public decimal? TSAUTHUE { get; set; }
        public string? EXTRA_JSON { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        public int ISDEL { get; set; }
    }
}

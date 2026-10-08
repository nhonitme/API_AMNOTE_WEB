namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceErrorNoticeInfo
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
        public DateTime? NTBCCQT { get; set; }
        public string? MST { get; set; }
        public string TNNT { get; set; } = string.Empty;
        public string DDANH { get; set; } = string.Empty;
        public DateTime? NTBAO { get; set; }
        public string? XML { get; set; }
        public int IS_SIGNED { get; set; }
        public int IS_MAIL { get; set; }
        public string? MGDDTU { get; set; }
        public string? MTDIEP { get; set; }
        public string? ERROR_MESSAGE { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        public int ISDEL { get; set; }
        public List<EInvoiceErrorNoticeDetail> DETAILS { get; set; } = new();
    }

    public class EInvoiceErrorNoticeDetail
    {
        public long DETAIL_ID { get; set; }
        public long TBAO_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public int? STT { get; set; }
        public string? MCCQT { get; set; }
        public string? KHMSHDON { get; set; }
        public string? KHHDON { get; set; }
        public string? SHDON { get; set; }
        public DateTime? NGAY { get; set; }
        public int LADHDDT { get; set; }
        public string? LDO { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        public int ISDEL { get; set; }
    }
}

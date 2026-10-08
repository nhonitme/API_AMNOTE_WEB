namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceTransmissionMessageKinds
    {
        public const string Send = "SEND";
        public const string Receive = "RECEIVE";
    }

    public class EInvoiceMessageSendInfo
    {
        public long SEND_ID { get; set; }
        public long? INVOICE_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string PBAN { get; set; } = string.Empty;
        public string MNGUI { get; set; } = string.Empty;
        public string MNNHAN { get; set; } = string.Empty;
        public string MLTDIEP { get; set; } = string.Empty;
        public string MLTDIEP_NAME { get; set; } = string.Empty;
        public string MTDIEP { get; set; } = string.Empty;
        public string MTDTCHIEU { get; set; } = string.Empty;
        public string MST { get; set; } = string.Empty;
        public int SLUONG { get; set; }
        public string REQUEST_XML { get; set; } = string.Empty;
        public string ERROR_MESSAGE { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        public DateTime CREATE_AT { get; set; }
    }

    public class EInvoiceTransmissionMessageDto
    {
        public string MESSAGE_KEY { get; set; } = string.Empty;
        public string MESSAGE_KIND { get; set; } = EInvoiceTransmissionMessageKinds.Receive;
        public long? SEND_ID { get; set; }
        public long? RECEIVE_ID { get; set; }
        public long? INVOICE_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string PBAN { get; set; } = string.Empty;
        public string MNGUI { get; set; } = string.Empty;
        public string MNNHAN { get; set; } = string.Empty;
        public string MLTDIEP { get; set; } = string.Empty;
        public string MLTDIEP_NAME { get; set; } = string.Empty;
        public string MTDIEP { get; set; } = string.Empty;
        public string MTDTCHIEU { get; set; } = string.Empty;
        public string MTRA_CUU { get; set; } = string.Empty;
        public string MST { get; set; } = string.Empty;
        public int SLUONG { get; set; }
        public string ERROR_MESSAGE { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        public DateTime CREATE_AT { get; set; }
    }
}

namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceMessageSendRequest
    {
        public long? INVOICE_ID { get; set; }
        public string PBAN { get; set; } = string.Empty;
        public string MNGUI { get; set; } = string.Empty;
        public string MNNHAN { get; set; } = string.Empty;
        public string MLTDIEP { get; set; } = string.Empty;
        public string MTDIEP { get; set; } = string.Empty;
        public string MTDTCHIEU { get; set; } = string.Empty;
        public string MST { get; set; } = string.Empty;
        public int SLUONG { get; set; }
        public string REQUEST_XML { get; set; } = string.Empty;
        public string ERROR_MESSAGE { get; set; } = string.Empty;
    }

    public class EInvoiceMessageWorkQueueRequest
    {
        public string MST { get; set; } = string.Empty;
        public long? INVOICE_ID { get; set; }
        public string SEND_MTDIEP { get; set; } = string.Empty;
        public string WORK_STATUS { get; set; } = "WAIT_RESPONSE";
        public string ERROR_MESSAGE { get; set; } = string.Empty;
    }

    public class EInvoiceMessageReceiveRequest
    {
        public string PBAN { get; set; } = string.Empty;
        public string MNGUI { get; set; } = string.Empty;
        public string MNNHAN { get; set; } = string.Empty;
        public string MLTDIEP { get; set; } = string.Empty;
        public string MTDIEP { get; set; } = string.Empty;
        public string MTDTCHIEU { get; set; } = string.Empty;
        public string MST { get; set; } = string.Empty;
        public int SLUONG { get; set; }
        public string RESPONSE_XML { get; set; } = string.Empty;
        public string ERROR_MESSAGE { get; set; } = string.Empty;
    }

    public class EInvoiceMessageReceiveInfo
    {
        public long RECEIVE_ID { get; set; }
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
        public string RESPONSE_XML { get; set; } = string.Empty;
        public string ERROR_MESSAGE { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        public DateTime CREATE_AT { get; set; }
    }

    public readonly record struct EInvoicePackagedMessage(
        string RequestXml,
        string PBAN,
        string MNGUI,
        string MNNHAN,
        string MLTDIEP,
        string MTDIEP,
        string MTDTCHIEU,
        string MST,
        int SLUONG);
}
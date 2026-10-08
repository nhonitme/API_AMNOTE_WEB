namespace API_AMNOTE_WEB.Models
{
    public class EInvoiceMessageWorkQueueItem
    {
        public long WORK_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string DB_NAME { get; set; } = string.Empty;
        public string MST { get; set; } = string.Empty;
        public string TARGET_TYPE { get; set; } = EInvoiceMessageTargetTypes.Invoice;
        public long? TARGET_ID { get; set; }
        public string SEND_MTDIEP { get; set; } = string.Empty;
        public string WORK_STATUS { get; set; } = string.Empty;
        public int RETRY_COUNT { get; set; }
        public string ERROR_MESSAGE { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class EInvoiceGatewayOptions
    {
        public bool Enabled { get; set; }

        public string BaseUrl { get; set; } = "https://api-vinvoice.viettel.vn";

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public int PollIntervalSeconds { get; set; } = 5;

        public int TokenRefreshMinutes { get; set; } = 15;

        public int LoginRetryCount { get; set; } = 3;

        public int LoginRetryDelaySeconds { get; set; } = 2;

        public int PendingBatchSize { get; set; } = 50;

        public string SystemUserId { get; set; } = "SYSTEM";

        public string InboundGatewayBaseUrl { get; set; } = "https://vinvoice.viettel.vn/api";

        public string InboundGatewaySendPath { get; set; } = "services/tctninboundgateway/api/v1/invoice/dvgp/request";
    }

    public class ViettelEInvoiceLoginResponse
    {
        public string access_token { get; set; } = string.Empty;
        public string token_type { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
        public int expires_in { get; set; }
    }

    public class ViettelEInvoiceMessageLookupResult
    {
        public string PBAN { get; set; } = string.Empty;
        public string MNGUI { get; set; } = string.Empty;
        public string MNNHAN { get; set; } = string.Empty;
        public string MLTDIEP { get; set; } = string.Empty;
        public string MTDIEP { get; set; } = string.Empty;
        public string MTDTCHIEU { get; set; } = string.Empty;
        public string MST { get; set; } = string.Empty;
        public int SLUONG { get; set; }
        public string DLieu { get; set; } = string.Empty;
    }

    public class EInvoiceMessagePendingSendItem : EInvoiceMessageWorkQueueItem
    {
        public long SEND_ID { get; set; }
        public string REQUEST_XML { get; set; } = string.Empty;
    }

    public class ViettelEInvoiceSendResult
    {
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }
        public string ResponseBody { get; set; } = string.Empty;
    }
}

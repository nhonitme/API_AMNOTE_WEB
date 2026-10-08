using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceEmailHistoryRefTypes
    {
        public const string Invoice = "INVOICE";
    }

    public class EInvoiceUpdateBuyerEmailRequest
    {
        [JsonPropertyName("toEmail")]
        public string? ToEmail { get; set; }
    }

    public class EInvoiceEmailHistoryInfo
    {
        public long MAIL_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string DB_NAME { get; set; } = string.Empty;
        public string REF_TYPE { get; set; } = string.Empty;
        public long? REF_ID { get; set; }
        public string SEND_TYPE { get; set; } = string.Empty;
        public string SEND_STATUS { get; set; } = string.Empty;
        public string FROM_EMAIL { get; set; } = string.Empty;
        public string TO_EMAIL { get; set; } = string.Empty;
        public string CC_EMAIL { get; set; } = string.Empty;
        public string BCC_EMAIL { get; set; } = string.Empty;
        public string MAIL_SUBJECT { get; set; } = string.Empty;
        public int RETRY_COUNT { get; set; }
        public string ERROR_MESSAGE { get; set; } = string.Empty;
        public DateTime? SEND_DT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class EInvoiceEmailHistoryCreateRequest
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public string DB_NAME { get; set; } = string.Empty;
        public string REF_TYPE { get; set; } = EInvoiceEmailHistoryRefTypes.Invoice;
        public long? REF_ID { get; set; }
        public string SEND_TYPE { get; set; } = "EMAIL";
        public string SEND_STATUS { get; set; } = "WAIT";
        public string FROM_EMAIL { get; set; } = string.Empty;
        public string TO_EMAIL { get; set; } = string.Empty;
        public string CC_EMAIL { get; set; } = string.Empty;
        public string BCC_EMAIL { get; set; } = string.Empty;
        public string MAIL_SUBJECT { get; set; } = string.Empty;
        public string? MAIL_BODY { get; set; }
        public string? ATTACHMENT_INFO { get; set; }
        public int RETRY_COUNT { get; set; }
        public string ERROR_MESSAGE { get; set; } = string.Empty;
        public DateTime? SEND_DT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
    }
}

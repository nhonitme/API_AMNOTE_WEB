using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class MailSendRequest
    {
        public string To { get; set; } = string.Empty;
        public string? Cc { get; set; }
        public string? Bcc { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsBodyHtml { get; set; } = true;
        public List<MailAttachmentDto>? Attachments { get; set; }
    }

    public class MailAttachmentDto
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/octet-stream";
    }

    public class EInvoiceSendMailItemRequest
    {
        [JsonPropertyName("invoiceId")]
        public long InvoiceId { get; set; }

        [JsonPropertyName("toEmail")]
        public string? ToEmail { get; set; }
    }

    public class EInvoiceSendMailRequest
    {
        [JsonPropertyName("items")]
        public List<EInvoiceSendMailItemRequest>? Items { get; set; }
    }

    public class EInvoiceSendMailResultItem
    {
        public long InvoiceId { get; set; }
        public bool Success { get; set; }
        public string? ToEmail { get; set; }
        public string? Message { get; set; }
    }

    public class EInvoiceSendMailResult
    {
        public int Sent { get; set; }
        public int Skipped { get; set; }
        public List<EInvoiceSendMailResultItem> Results { get; set; } = new();
    }

    public class EInvoiceMinuteSendMailItemRequest
    {
        [JsonPropertyName("bbanId")]
        public long BbanId { get; set; }

        [JsonPropertyName("toEmail")]
        public string? ToEmail { get; set; }
    }

    public class EInvoiceMinuteSendMailRequest
    {
        [JsonPropertyName("items")]
        public List<EInvoiceMinuteSendMailItemRequest>? Items { get; set; }
    }

    public class EInvoiceMinuteSendMailResultItem
    {
        public long BbanId { get; set; }
        public bool Success { get; set; }
        public string? ToEmail { get; set; }
        public string? Message { get; set; }
    }

    public class EInvoiceMinuteSendMailResult
    {
        public int Sent { get; set; }
        public int Skipped { get; set; }
        public List<EInvoiceMinuteSendMailResultItem> Results { get; set; } = new();
    }

    public class EInvoiceErrorNoticeSendMailItemRequest
    {
        [JsonPropertyName("tbaoId")]
        public long TbaoId { get; set; }

        [JsonPropertyName("toEmail")]
        public string? ToEmail { get; set; }
    }

    public class EInvoiceErrorNoticeSendMailRequest
    {
        [JsonPropertyName("items")]
        public List<EInvoiceErrorNoticeSendMailItemRequest>? Items { get; set; }
    }

    public class EInvoiceErrorNoticeSendMailResultItem
    {
        public long TbaoId { get; set; }
        public bool Success { get; set; }
        public string? ToEmail { get; set; }
        public string? Message { get; set; }
    }

    public class EInvoiceErrorNoticeSendMailResult
    {
        public int Sent { get; set; }
        public int Skipped { get; set; }
        public List<EInvoiceErrorNoticeSendMailResultItem> Results { get; set; } = new();
    }

    public class MailSendOptions
    {
        public string? Cc { get; set; }
        public string? Bcc { get; set; }
        public int TimeoutMs { get; set; }
        public bool SendAll { get; set; }
        public bool AttachPdf { get; set; } = true;
        public bool AttachXml { get; set; } = true;
    }

    public class MailSettingDto
    {
        public long MAIL_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string MAIL_CD { get; set; } = "EINV";
        public string MAIL_NM { get; set; } = string.Empty;
        public string SMTP_HOST { get; set; } = string.Empty;
        public int SMTP_PORT { get; set; } = 587;
        public string SECURITY_TYPE { get; set; } = "STARTTLS";
        public string AUTH_TYPE { get; set; } = "PASSWORD";
        public string? USERNAME { get; set; }
        public bool HAS_PASSWORD { get; set; }
        public string FROM_EMAIL { get; set; } = string.Empty;
        public string? FROM_NAME { get; set; }
        public string? REPLY_TO_EMAIL { get; set; }
        public MailSendOptions CONFIG { get; set; } = new();
        public int IS_DEFAULT { get; set; }
        public int IS_ACTIVE { get; set; } = 1;
        public bool HAS_COMPANY_SETTING { get; set; }
        public bool IS_USING_SYSTEM_DEFAULT { get; set; }
        public MailSettingDto? SYSTEM_SETTING { get; set; }
    }

    public class MailSettingSaveRequest
    {
        public long MAIL_ID { get; set; }
        public string? MAIL_NM { get; set; }
        public string? SMTP_HOST { get; set; }
        public int SMTP_PORT { get; set; } = 587;
        public string? SECURITY_TYPE { get; set; }
        public string? AUTH_TYPE { get; set; }
        public string? USERNAME { get; set; }
        public string? PASSWORD { get; set; }
        public string? FROM_EMAIL { get; set; }
        public string? FROM_NAME { get; set; }
        public string? REPLY_TO_EMAIL { get; set; }
        public MailSendOptions? CONFIG { get; set; }
        public int IS_DEFAULT { get; set; }
        public int IS_ACTIVE { get; set; } = 1;
        public bool USE_SYSTEM_DEFAULT { get; set; }
    }

    public class MailSettingTestRequest : MailSettingSaveRequest
    {
        public string? TO_EMAIL { get; set; }
    }
}

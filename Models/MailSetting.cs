namespace API_AMNOTE_WEB.Models
{
    public class MailSetting
    {
        public long MAIL_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string MAIL_CD { get; set; } = "DEFAULT";
        public string MAIL_NM { get; set; } = string.Empty;
        public string SMTP_HOST { get; set; } = string.Empty;
        public int SMTP_PORT { get; set; } = 587;
        public string SECURITY_TYPE { get; set; } = "STARTTLS";
        public string AUTH_TYPE { get; set; } = "PASSWORD";
        public string? USERNAME { get; set; }
        public string? PASSWORD_ENC { get; set; }
        public int HAS_PASSWORD { get; set; }
        public string FROM_EMAIL { get; set; } = string.Empty;
        public string? FROM_NAME { get; set; }
        public string? REPLY_TO_EMAIL { get; set; }
        public string? CONFIG_JSON { get; set; }
        public int IS_DEFAULT { get; set; }
        public int IS_ACTIVE { get; set; } = 1;
        public int ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }
}

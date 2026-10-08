namespace API_AMNOTE_WEB.Models
{
    public class SysNotification
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = "";
        public string? USER_ID { get; set; }
        public string? ROLE_CD { get; set; }
        public string NOTIFICATION_TYPE { get; set; } = "";
        public string? SOURCE_MODULE { get; set; }
        public string? SOURCE_ID { get; set; }
        public string TITLE { get; set; } = "";
        public string? MESSAGE { get; set; }
        public string PRIORITY { get; set; } = "MEDIUM";
        public string? ACTION_URL { get; set; }
        public string IS_READ { get; set; } = "N";
        public DateTime? READ_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        public DateTime? CREATE_AT { get; set; }
        public DateTime? EXPIRED_AT { get; set; }
    }

    public class SysNotificationSummary
    {
        public int TOTAL_COUNT { get; set; }
        public int UNREAD_COUNT { get; set; }
    }

    public class SysNotificationListResult
    {
        public int TotalCount { get; set; }
        public IReadOnlyList<SysNotification> Items { get; set; } = Array.Empty<SysNotification>();
    }
}

namespace API_AMNOTE_WEB.Models.DTOs
{
    public class SysNotificationQueryDto
    {
        public string? IsRead { get; set; }
        public string? NotificationType { get; set; }
        public string? SourceModule { get; set; }
        public string? Keyword { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SysNotificationCreateRequest
    {
        public string? UserId { get; set; }
        public string? RoleCd { get; set; }
        public string NotificationType { get; set; } = "";
        public string? SourceModule { get; set; }
        public string? SourceId { get; set; }
        public string Title { get; set; } = "";
        public string? Message { get; set; }
        public string Priority { get; set; } = "MEDIUM";
        public string? ActionUrl { get; set; }
        public DateTime? ExpiredAt { get; set; }
    }

    public class SysNotificationSyncRequest
    {
        public string? FromYmd { get; set; }
        public string? ToYmd { get; set; }
    }

    public class SysNotificationSyncResultDto
    {
        public int SyncedCount { get; set; }
    }
}

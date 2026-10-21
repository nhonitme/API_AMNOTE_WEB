namespace API_AMNOTE_WEB.Models
{
    public sealed class LicenseInfo
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public string? COMPANY_NM { get; set; }
        public string LICENSE_KEY_MASKED { get; set; } = string.Empty;
        public string PLAN_CODE { get; set; } = string.Empty;
        public string LICENSE_TYPE { get; set; } = string.Empty;
        public DateTime? START_DATE { get; set; }
        public DateTime? END_DATE { get; set; }
        public int? MAX_USERS { get; set; }
        public int? ACTIVE_USERS { get; set; }
        public int? MAX_CONCURRENT_USERS { get; set; }
        public string LICENSE_STATUS { get; set; } = string.Empty;
        public int? DAYS_REMAINING { get; set; }
        public IReadOnlyList<LicenseRenewalHistory> RENEWAL_HISTORY { get; set; } = Array.Empty<LicenseRenewalHistory>();
    }

    public sealed class LicenseRenewalHistory
    {
        public long ID { get; set; }
        public DateTime RENEWED_AT { get; set; }
        public DateTime? OLD_END_DATE { get; set; }
        public DateTime? NEW_END_DATE { get; set; }
        public string? NOTE { get; set; }
    }
}

namespace API_AMNOTE_WEB.Models
{
    /// <summary>
    /// Lightweight result for checking a company's License without loading user counts,
    /// renewal history, or company details.
    /// </summary>
    public sealed class LicenseStatusInfo
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public string LICENSE_STATUS { get; set; } = string.Empty;
        public DateTime? END_DATE { get; set; }
    }
}

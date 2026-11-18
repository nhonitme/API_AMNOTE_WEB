namespace API_AMNOTE_WEB.Models
{
    public class LicenseAiInfo
    {
        public long LIC_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public DateTime? BEGIN_DATE { get; set; }
        public DateTime? END_DATE { get; set; }
        public short USER_LIMIT { get; set; }
        public int IS_ACTIVE { get; set; }
        public int IS_INIT_DATA { get; set; }
        public string? STOP_MSG { get; set; }
        public string? REMARK { get; set; }
        public int ISDEL { get; set; }
    }
}

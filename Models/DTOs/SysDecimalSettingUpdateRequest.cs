namespace API_AMNOTE_WEB.Models.DTOs
{
    public class SysDecimalSettingUpdateRequest
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public int DECIMAL_PLACES { get; set; } = 0;
        public string ROUNDING_MODE { get; set; } = "ROUND";
        public string USE_THOUSAND_SEPARATOR { get; set; } = "1";
        public string IS_ACTIVE { get; set; } = "1";
        public string? NOTE { get; set; }
    }
}

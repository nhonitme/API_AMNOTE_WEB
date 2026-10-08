namespace API_AMNOTE_WEB.Models
{
    public class SysDecimalSetting
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string SETTING_TYPE { get; set; } = string.Empty;
        public int DECIMAL_PLACES { get; set; }
        public string ROUNDING_MODE { get; set; } = "ROUND";
        public string USE_THOUSAND_SEPARATOR { get; set; } = "1";
        public string IS_ACTIVE { get; set; } = "1";
        public string NOTE { get; set; } = string.Empty;
        public string ISDEL { get; set; } = "0";
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime UPDATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string UPDATE_BY { get; set; } = string.Empty;
        public string? APPLY_SCOPE { get; set; }
    }
}

namespace API_AMNOTE_WEB.Models
{
    public class SysConfig
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string CONFIG_GROUP { get; set; } = string.Empty;
        public string CONFIG_KEY { get; set; } = string.Empty;
        public string? CONFIG_VALUE { get; set; }
        public string DATA_TYPE { get; set; } = "string";
        public string? DESCRIPTION { get; set; }
        public string IS_ACTIVE { get; set; } = "1";
        public string ISDEL { get; set; } = "0";
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }
}

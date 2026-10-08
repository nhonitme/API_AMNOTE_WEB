namespace API_AMNOTE_WEB.Models
{
    public class SysGridColumnTemplate
    {
        public long TEMPLATE_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USER_ID { get; set; } = string.Empty;
        public string GRID_ID { get; set; } = string.Empty;
        public string TEMPLATE_NAME { get; set; } = "Mặc định";
        public string IS_DEFAULT_TEMPLATE { get; set; } = "0";
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string UPDATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime UPDATE_AT { get; set; }
        public string ISDEL { get; set; } = "0";
    }
}

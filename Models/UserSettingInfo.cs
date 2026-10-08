namespace API_AMNOTE_WEB.Models
{
    public class UserSettingInfo
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USER_ID { get; set; } = string.Empty;
        public string KEY_NAME { get; set; } = string.Empty;
        public string VALUE { get; set; } = string.Empty;
        public string NOTE { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }
}

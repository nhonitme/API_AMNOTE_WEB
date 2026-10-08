namespace API_AMNOTE_WEB.Models
{
    public class SysCodeInfo
    {
        public long? CODE_ID { get; set; }
        public string CODE_TYPE { get; set; } = string.Empty;
        public string CODE_CD { get; set; } = string.Empty;
        public string CODE_NAME { get; set; } = string.Empty;
        public int? SORT_ORDER { get; set; }
        public string? NOTE { get; set; }
        public int? IS_ACTIVE { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }
}

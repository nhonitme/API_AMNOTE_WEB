namespace API_AMNOTE_WEB.Models
{
    public class SysGridColumnSetting
    {
        public long ID { get; set; }
        public long TEMPLATE_ID { get; set; }
        public string GRID_ID { get; set; } = string.Empty;
        public string FIELD_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string? IS_VISIBLE { get; set; }
        public int? VISIBLE_INDEX { get; set; }
        public int? COLUMN_WIDTH { get; set; }
        public string? IS_FIXED { get; set; }
        public string? FIXED_POSITION { get; set; }
        public string? ALLOW_HIDING { get; set; }
        public string? SORT_ORDER { get; set; }
        public int? SORT_INDEX { get; set; }
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

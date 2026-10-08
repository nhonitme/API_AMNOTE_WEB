namespace API_AMNOTE_WEB.Models.DTOs
{
    public class SysGridColumnDto
    {
        public long ID { get; set; }
        public string GRID_ID { get; set; } = string.Empty;
        public string FIELD_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string CAPTION { get; set; } = string.Empty;
        public string IS_VISIBLE { get; set; } = "1";
        public int? VISIBLE_INDEX { get; set; }
        public int? COLUMN_WIDTH { get; set; }
        public string IS_FIXED { get; set; } = "0";
        public string? FIXED_POSITION { get; set; }
        public string ALLOW_HIDING { get; set; } = "1";
        public string? ALIGN { get; set; }
        public string? FORMAT_TYPE { get; set; }
        public string? SORT_ORDER { get; set; }
        public int? SORT_INDEX { get; set; }
    }

    public class SysGridColumnSettingDto
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
    }

    public class SysGridColumnTemplateDto
    {
        public long TEMPLATE_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USER_ID { get; set; } = string.Empty;
        public string GRID_ID { get; set; } = string.Empty;
        public string TEMPLATE_NAME { get; set; } = "Mặc định";
        public string IS_DEFAULT_TEMPLATE { get; set; } = "0";
    }

    public class SysGridColumnBundleDto
    {
        public List<SysGridColumnDto> COLUMNS { get; set; } = new();
        public List<SysGridColumnTemplateDto> TEMPLATES { get; set; } = new();
        public List<SysGridColumnSettingDto> SETTINGS { get; set; } = new();
    }
}

namespace API_AMNOTE_WEB.Models.DTOs
{
    public sealed class ConfiguredReportPreviewDto
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public string REPORT_CODE { get; set; } = string.Empty;
        public string REPORT_NAME { get; set; } = string.Empty;
        public string PREVIEW_MODE { get; set; } = "FLAT_REPORT";
        public List<ConfiguredReportPreviewColumnDto> COLUMNS { get; set; } = new();
        public List<ConfiguredReportPreviewRowDto> ROWS { get; set; } = new();
    }

    public sealed class ConfiguredReportPreviewColumnDto
    {
        public string COLUMN_KEY { get; set; } = string.Empty;
        public string FIELD_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string CAPTION { get; set; } = string.Empty;
        public string DATA_TYPE { get; set; } = "string";
        public string? FORMAT { get; set; }
        public string ALIGN { get; set; } = "left";
        public double WIDTH { get; set; }
        public int SORT_ORDER { get; set; }
    }

    public sealed class ConfiguredReportPreviewRowDto
    {
        public string ROW_KEY { get; set; } = string.Empty;
        public Dictionary<string, object?> VALUES { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

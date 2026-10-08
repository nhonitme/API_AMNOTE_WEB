namespace API_AMNOTE_WEB.Models
{
    public class ReportColumnLayoutInfo
    {
        public string COLUMN_KEY { get; set; } = string.Empty;
        public string? PARENT_KEY { get; set; }
        public string? FIELD_NAME { get; set; }
        public string? LABEL_TEXT { get; set; }
        public string CAPTION { get; set; } = string.Empty;
        public int ROW_INDEX { get; set; }
        public int COL_INDEX { get; set; }
        public int COL_SPAN { get; set; } = 1;
        public int ROW_SPAN { get; set; } = 1;
        public decimal WIDTH { get; set; } = 1m;
        public string ALIGN { get; set; } = "LEFT";
        public string FORMAT_TYPE { get; set; } = "TEXT";
        public int SORT_ORDER { get; set; }
    }
}

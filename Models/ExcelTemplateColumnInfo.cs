namespace API_AMNOTE_WEB.Models
{
    public class ExcelTemplateColumnInfo
    {
        public string FIELD_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string TABLE_NM { get; set; } = string.Empty;
        public string? IS_REQUIRED { get; set; }
        public string? EXPLAIN_TABLE_QUERY { get; set; }
        public string? IS_PRIMARY_KEY { get; set; }
        //public string? EXPLAIN_TABLE { get; set; }
        //public string? EXPLAIN_COLUMN_CD { get; set; }
        //public string? EXPLAIN_COLUMN_NM { get; set; }
        //public string? EXPLAIN_WHERE { get; set; }
    }
}

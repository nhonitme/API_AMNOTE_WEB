namespace API_AMNOTE_WEB.Models
{
    public class CompanyReportElementInfo
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string REPORT_KEY { get; set; } = string.Empty;
        public string SECTION_TYPE { get; set; } = string.Empty;
        public string ELEMENT_TYPE { get; set; } = string.Empty;
        public string AREA_CODE { get; set; } = "LEFT";
        public string ITEM_KEY { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string VALUE_SOURCE { get; set; } = "FIXED_TEXT";
        public string? VALUE_FIELD { get; set; }
        public int ROW_NO { get; set; } = 1;
        public int COL_NO { get; set; } = 1;
        public int COL_SPAN { get; set; } = 1;
        public int SORT_ORDER { get; set; }
        public int? VISIBLE_INDEX { get; set; }
        public int? WIDTH { get; set; }
        public decimal? WIDTH_PERCENT { get; set; }
        public string? ALIGN_HEADER { get; set; }
        public string? ALIGN_DATA { get; set; }
        public string? ALIGN { get; set; }
        public string? DATA_TYPE { get; set; } = "TEXT";
        public string? FORMAT_STRING { get; set; }
        public string? IS_SUMMARY { get; set; } = "0";
        public string? SUMMARY_TYPE { get; set; }
        public decimal? FONT_SIZE { get; set; }
        public string? IS_BOLD { get; set; } = "0";
        public string? IS_ITALIC { get; set; } = "0";
        public string? IS_VISIBLE { get; set; } = "1";
        public string? ISDEL { get; set; } = "0";
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }
}

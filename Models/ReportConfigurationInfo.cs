using API_AMNOTE_WEB.Reports;

namespace API_AMNOTE_WEB.Models
{
    public class ReportConfigurationInfo
    {
        public long? MAPPING_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string REPORT_KEY { get; set; } = string.Empty;
        public long REPORT_ID { get; set; }
        public string? SIGN_IDS { get; set; }
        public string REPORT_CODE { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string REPORT_TYPE { get; set; } = string.Empty;
        public string REPORT_SOURCE { get; set; } = string.Empty;
        public string? DATA_SOURCE_TYPE { get; set; }
        public string? DATA_SOURCE_REF { get; set; }
        public string? DATA_SET_NAME { get; set; }
        public string? PARAM_MODE { get; set; }
        public string? IS_DEFAULT { get; set; }
        public string? MAPPING_IS_ACTIVE { get; set; }
        public string? REPORT_IS_ACTIVE { get; set; }
        public string? PAGE_ORIENTATION { get; set; }
        public string? PAPER_KIND { get; set; }
        public string? FONT_FAMILY { get; set; }
        public decimal? FONT_SIZE { get; set; }
        public decimal? TITLE_FONT_SIZE { get; set; }
        public decimal? INFO_FONT_SIZE { get; set; }
        public decimal? HEADER_FONT_SIZE { get; set; }
        public decimal? DETAIL_FONT_SIZE { get; set; }
        public decimal? FOOTER_FONT_SIZE { get; set; }
        public int? MARGIN_LEFT { get; set; }
        public int? MARGIN_RIGHT { get; set; }
        public int? MARGIN_TOP { get; set; }
        public int? MARGIN_BOTTOM { get; set; }
        public List<CompanyReportSignatureInfo> SIGNATURES { get; set; } = new();
        public List<CompanyReportElementInfo> ELEMENTS { get; set; } = new();
    }
}

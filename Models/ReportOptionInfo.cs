namespace API_AMNOTE_WEB.Models
{
    public sealed class ReportOptionInfo
    {
        public long OPTION_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string REPORT_GROUP_CODE { get; set; } = string.Empty;
        public string OPTION_CODE { get; set; } = string.Empty;
        public string OPTION_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string REPORT_CODE { get; set; } = string.Empty;
        public string IS_DEFAULT { get; set; } = "0";
        public int SORT_ORDER { get; set; }
    }
}

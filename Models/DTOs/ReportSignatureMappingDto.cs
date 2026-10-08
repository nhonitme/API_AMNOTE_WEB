namespace API_AMNOTE_WEB.Models.DTOs
{
    public sealed class ReportSignatureMappingDto
    {
        public long? MAPPING_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string REPORT_KEY { get; set; } = string.Empty;
        public string REPORT_CODE { get; set; } = string.Empty;
        public string REPORT_NAME { get; set; } = string.Empty;
        public long REPORT_ID { get; set; }
        public string? SIGN_IDS { get; set; }
        public List<ReportSignatureMappingSignatureDto> SIGNATURES { get; set; } = new();
    }

    public sealed class ReportSignatureMappingSignatureDto
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string SIGN_CODE { get; set; } = string.Empty;
        public string? DISPLAY_LABEL { get; set; }
        public string SIGN_NAME { get; set; } = string.Empty;
        public string? SIGN_TITLE { get; set; }
        public string? SIGN_IMAGE_URL { get; set; }
        public int? SORT_ORDER { get; set; }
        public string? IS_ACTIVE { get; set; }
        public bool IS_SELECTED { get; set; }
        public int? SELECTED_ORDER { get; set; }
    }
}

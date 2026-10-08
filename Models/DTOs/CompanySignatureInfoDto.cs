using System;

namespace API_AMNOTE_WEB.Models.DTOs
{
    public class CompanySignatureInfoDto
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
        public string? ISDEL { get; set; }
    }
}

using System;
using System.Collections.Generic;

namespace API_AMNOTE_WEB.Models
{
    public class CompanySignatureInfo
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
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class CompanySignatureInfoRequest
    {
        public long? ID { get; set; }
        public string? SIGN_CODE { get; set; }
        public string? DISPLAY_LABEL { get; set; }
        public string? SIGN_NAME { get; set; }
        public string? SIGN_TITLE { get; set; }
        public string? SIGN_IMAGE_URL { get; set; }
        public int? SORT_ORDER { get; set; }
        public string? IS_ACTIVE { get; set; }
        public string? ISDEL { get; set; }
    }

    public class DeleteCompanySignaturesRequest
    {
        public List<long> SignatureIds { get; set; } = new();
    }
}

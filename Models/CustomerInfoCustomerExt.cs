namespace API_AMNOTE_WEB.Models
{
    public class CustomerInfoCustomerExt
    {
        public long CUSTOMER_ID { get; set; }
        public long? CUSTOMER_EXT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string CUSTOMER_CD { get; set; } = string.Empty;
        public string? CATEGORY_CD { get; set; }
        public string? CUSTOMER_TYPE { get; set; }
        public string? CUSTOMER_NM_VIET { get; set; }
        public string? CUSTOMER_NM_ENG { get; set; }
        public string? CUSTOMER_NM_KOR { get; set; }
        public string? CUSTOMER_NM_CHINA { get; set; }
        public string? ADDRESS { get; set; }
        public string? TEL { get; set; }
        public string? FAX { get; set; }
        public string? TAX_CD { get; set; }
        public long? BANK_ID { get; set; }
        public string? BANK_CD { get; set; }
        public string? BANK_NM { get; set; }
        public string? EMAIL { get; set; }
        public string? NOTE { get; set; }
        public string? IDNUMBER { get; set; }
        public string? BUYER_NM { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
    }

    public class CustomerInfoCustomerExtRequest
    {
        public long? CUSTOMER_ID { get; set; }
        public long? CUSTOMER_EXT_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? CUSTOMER_CD { get; set; }
        public string? CATEGORY_CD { get; set; }
        public string? CUSTOMER_TYPE { get; set; }
        public string? CUSTOMER_NM_VIET { get; set; }
        public string? CUSTOMER_NM_ENG { get; set; }
        public string? CUSTOMER_NM_KOR { get; set; }
        public string? CUSTOMER_NM_CHINA { get; set; }
        public string? ADDRESS { get; set; }
        public string? TEL { get; set; }
        public string? FAX { get; set; }
        public string? TAX_CD { get; set; }
        public long? BANK_ID { get; set; }
        public string? BANK_CD { get; set; }
        public string? EMAIL { get; set; }
        public string? NOTE { get; set; }
        public string? IDNUMBER { get; set; }
        public string? BUYER_NM { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
    }

    public class DeleteCustomerInfosRequest
    {
        public List<long> CustomerIds { get; set; } = new();
    }
}

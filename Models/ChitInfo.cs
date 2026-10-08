namespace API_AMNOTE_WEB.Models
{
    public class ChitInfo
    {
        public long CHIT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string CHIT_CD { get; set; } = string.Empty;
        public string? CHIT_NO { get; set; }
        public string? CHIT_YMD { get; set; }
        public string? CHIT_TYPE { get; set; }
        public string? INPUT_TYPE { get; set; }
        public string? LOCK_STEP_CODE { get; set; }
        public decimal? AMOUNT { get; set; }
        public string? PAYER_INFO { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
        public string? IS_LOCK { get; set; }
        public string? ISEXCEL { get; set; }
        public string? EMAIL_EPAY { get; set; }
        public string? IS_CONFIRMED { get; set; }
        public string? NOTE { get; set; }
        public int? DAY_OF_PAYMENT { get; set; }
        public string? TIME_FOR_PAYMENT { get; set; }
        public string? IS_PAYMENT { get; set; }
        public string? CHIT_CD_COGS { get; set; }
        public string? DESCRIPTION_VIET { get; set; }
        public string? DESCRIPTION_ENG { get; set; }
        public string? DESCRIPTION_KOR { get; set; }
        public int DETAIL_COUNT { get; set; }
    }

    public class ChitDetail
    {
        public long CHITDETAIL_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long CHIT_ID { get; set; }
        public string CHITDETAIL_CD { get; set; } = string.Empty;
        public string? CHIT_YMD { get; set; }
        public string? CHIT_VMD { get; set; }
        public string? DEBIT { get; set; }
        public string? DEBIT_NM_VIET { get; set; }
        public string? DEBIT_NM_ENG { get; set; }
        public string? DEBIT_NM_KOR { get; set; }
        public string? DEBIT_NM_CHINA { get; set; }
        public string? CREDIT { get; set; }
        public string? CREDIT_NM_VIET { get; set; }
        public string? CREDIT_NM_ENG { get; set; }
        public string? CREDIT_NM_KOR { get; set; }
        public string? CREDIT_NM_CHINA { get; set; }
        public decimal? AMOUNT { get; set; }
        public decimal? FC_AMOUNT { get; set; }
        public string? FC_TYPE { get; set; }
        public decimal? FC_RATE { get; set; }
        public DateTime? FC_DATETIME { get; set; }
        public int? SORT { get; set; }
        public string? ISDEL { get; set; }
        public string? CHITDETAIL_VAT_CD { get; set; }
        public string? MG_CD { get; set; }
        public string? MG_CD_2 { get; set; }
        public string? MR_CD { get; set; }
        public string? MR_CD2 { get; set; }
        public long? BANK_ID { get; set; }
        public string? BANK_CD { get; set; }
        public string? BANK_OWN_CD { get; set; }
        public string? CUSTOMER_CD { get; set; }
        public long? CUSTOMER_ID { get; set; }
        public string? CUSTOMER_NM_VIET { get; set; }
        public string? CUSTOMER_NM_ENG { get; set; }
        public string? CUSTOMER_NM_KOR { get; set; }
        public string? CUSTOMER_NM_CHINA { get; set; }
        public string? CUSTOMER_OWN_CD { get; set; }
        public long? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CD { get; set; }
        public string? DEPARTMENT_CD_2 { get; set; }
        public string? HASINVENTORY { get; set; }
        public string? INVENTORY_YMD { get; set; }
        public string? ISPAY { get; set; }
        public string? ISCOLLECT { get; set; }
        public string? VAT_SERIAL_NO { get; set; }
        public string? VAT_CHIT_NO { get; set; }
        public string? VAT_CHIT_NO_2 { get; set; }
        public decimal? VAT_AMOUNT { get; set; }
        public decimal? VAT_TAXABLE_AMOUNT { get; set; }
        public decimal? FO_VAT_AMOUNT { get; set; }
        public string? VAT_ISFREE { get; set; }
        public string? VAT_INVOICE_CD { get; set; }
        public string? VAT_INVOICE_NM { get; set; }
        public string? VAT_INFO_TYPE { get; set; }
        public string? VAT_COMPANY_ISSUE { get; set; }
        public string? VAT_COMPANY_ISSUE_ADDRESS { get; set; }
        public string? VAT_COMPANY_ISSUE_CD { get; set; }
        public string? VAT_COMPANY_TAXCD { get; set; }
        public string? VAT_PRODUCT_NM { get; set; }
        public string? VAT_ETC { get; set; }
        public string? VAT_YMD { get; set; }
        public string? VAT_YMD_2 { get; set; }
        public string? VAT_INQUIRY_IN { get; set; }
        public string? VAT_INQUIRY_CODE { get; set; }
        public string? IS_NEXTVAT { get; set; }
        public string? UNDEFINE { get; set; }
        public string? DETAIL_DESCRIPTION_VIET { get; set; }
        public string? DETAIL_DESCRIPTION_ENG { get; set; }
        public string? DETAIL_DESCRIPTION_KOR { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class ChitInfoRequest
    {
        public long? CHIT_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public string? CHIT_CD { get; set; }
        public string? CHIT_NO { get; set; }
        public string? CHIT_YMD { get; set; }
        public string? CHIT_TYPE { get; set; }
        public decimal? AMOUNT { get; set; }
        public string? PAYER_INFO { get; set; }
        public string? ISDEL { get; set; }
        public string? IS_LOCK { get; set; }
        public string? ISEXCEL { get; set; }
        public string? EMAIL_EPAY { get; set; }
        public string? IS_CONFIRMED { get; set; }
        public string? NOTE { get; set; }
        public int? DAY_OF_PAYMENT { get; set; }
        public string? TIME_FOR_PAYMENT { get; set; }
        public string? IS_PAYMENT { get; set; }
        public string? CHIT_CD_COGS { get; set; }
        public string? DESCRIPTION_VIET { get; set; }
        public string? DESCRIPTION_ENG { get; set; }
        public string? DESCRIPTION_KOR { get; set; }
        public List<ChitDetailRequest> DETAILS { get; set; } = new();
    }

    public class ChitDetailRequest
    {
        public long? CHITDETAIL_ID { get; set; }
        public string? COMPANY_CD { get; set; }
        public long? CHIT_ID { get; set; }
        public string? CHITDETAIL_CD { get; set; }
        public string? CHIT_YMD { get; set; }
        public string? CHIT_VMD { get; set; }
        public string? DEBIT { get; set; }
        public string? DEBIT_NM_VIET { get; set; }
        public string? DEBIT_NM_ENG { get; set; }
        public string? DEBIT_NM_KOR { get; set; }
        public string? DEBIT_NM_CHINA { get; set; }
        public string? CREDIT { get; set; }
        public string? CREDIT_NM_VIET { get; set; }
        public string? CREDIT_NM_ENG { get; set; }
        public string? CREDIT_NM_KOR { get; set; }
        public string? CREDIT_NM_CHINA { get; set; }
        public decimal? AMOUNT { get; set; }
        public decimal? FC_AMOUNT { get; set; }
        public string? FC_TYPE { get; set; }
        public decimal? FC_RATE { get; set; }
        public DateTime? FC_DATETIME { get; set; }
        public int? SORT { get; set; }
        public string? ISDEL { get; set; }
        public string? CHITDETAIL_VAT_CD { get; set; }
        public string? MG_CD { get; set; }
        public string? MG_CD_2 { get; set; }
        public string? MR_CD { get; set; }
        public string? MR_CD2 { get; set; }
        public long? BANK_ID { get; set; }
        public string? BANK_CD { get; set; }
        public string? BANK_OWN_CD { get; set; }
        public long? CUSTOMER_ID { get; set; }
        public string? CUSTOMER_CD { get; set; }
        public string? CUSTOMER_NM_VIET { get; set; }
        public string? CUSTOMER_NM_ENG { get; set; }
        public string? CUSTOMER_NM_KOR { get; set; }
        public string? CUSTOMER_NM_CHINA { get; set; }
        public string? CUSTOMER_OWN_CD { get; set; }
        public long? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CD { get; set; }
        public string? DEPARTMENT_CD_2 { get; set; }
        public string? HASINVENTORY { get; set; }
        public string? INVENTORY_YMD { get; set; }
        public string? ISPAY { get; set; }
        public string? ISCOLLECT { get; set; }
        public string? VAT_SERIAL_NO { get; set; }
        public string? VAT_CHIT_NO { get; set; }
        public string? VAT_CHIT_NO_2 { get; set; }
        public decimal? VAT_AMOUNT { get; set; }
        public decimal? VAT_TAXABLE_AMOUNT { get; set; }
        public decimal? FO_VAT_AMOUNT { get; set; }
        public string? VAT_ISFREE { get; set; }
        public string? VAT_INVOICE_CD { get; set; }
        public string? VAT_INVOICE_NM { get; set; }
        public string? VAT_INFO_TYPE { get; set; }
        public string? VAT_COMPANY_ISSUE { get; set; }
        public string? VAT_COMPANY_ISSUE_ADDRESS { get; set; }
        public string? VAT_COMPANY_ISSUE_CD { get; set; }
        public string? VAT_COMPANY_TAXCD { get; set; }
        public string? VAT_PRODUCT_NM { get; set; }
        public string? VAT_ETC { get; set; }
        public string? VAT_YMD { get; set; }
        public string? VAT_YMD_2 { get; set; }
        public string? VAT_INQUIRY_IN { get; set; }
        public string? VAT_INQUIRY_CODE { get; set; }
        public string? IS_NEXTVAT { get; set; }
        public string? UNDEFINE { get; set; }
        public string? DETAIL_DESCRIPTION_VIET { get; set; }
        public string? DETAIL_DESCRIPTION_ENG { get; set; }
        public string? DETAIL_DESCRIPTION_KOR { get; set; }
        public List<InventoryInput>? INVENTORY_INPUTS { get; set; }
        public List<InventoryOutput>? INVENTORY_OUTPUTS { get; set; }
    }
}

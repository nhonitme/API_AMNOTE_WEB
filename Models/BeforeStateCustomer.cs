namespace API_AMNOTE_WEB.Models
{
    public class BeforeStateCustomer
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string OPEN_YMD { get; set; } = string.Empty;

        public long? CUSTOMER_ID { get; set; }
        public string? CUSTOMER_CD { get; set; }
        public string CUSTOMER_NM_VIET { get; set; } = string.Empty;
        public string CUSTOMER_NM_ENG { get; set; } = string.Empty;
        public string CUSTOMER_NM_KOR { get; set; } = string.Empty;
        public string CUSTOMER_NM_CHINA { get; set; } = string.Empty;


        public long? ACC_ID { get; set; }
        public string ACC_CD { get; set; } = string.Empty;
        public string ACC_NM_VIET { get; set; } = string.Empty;
        public string ACC_NM_ENG { get; set; } = string.Empty;
        public string ACC_NM_KOR { get; set; } = string.Empty;
        public string ACC_NM_CHINA { get; set; } = string.Empty;

        public string FC_TYPE { get; set; } = "VND";
        public decimal DEBIT { get; set; }
        public decimal CREDIT { get; set; }
        public decimal DEBIT_FC { get; set; }
        public decimal CREDIT_FC { get; set; }
        public decimal EXCHANGE_RATE { get; set; } = 1m;

        public string SUMMARY { get; set; } = string.Empty;
        public string NOTE { get; set; } = string.Empty;

        public string ISDEL { get; set; } = "0";
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string UPDATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime UPDATE_AT { get; set; }

        public string ROW_STATE { get; set; } = "UNCHANGED";
    }
}

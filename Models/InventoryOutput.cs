namespace API_AMNOTE_WEB.Models
{
    public class InventoryOutput
    {
        public long? OUTPUT_ID { get; set; }
        public long? CHITDETAIL_ID_COGS { get; set; }
        public string? COGS_DEBIT { get; set; }
        public string? COGS_CREDIT { get; set; }
        public string? OUTPUT_CD { get; set; } = string.Empty;
        public long? CHIT_ID { get; set; }
        public string? CHIT_CD { get; set; } = string.Empty;
        public string? CHIT_TYPE { get; set; } = string.Empty;
        public string? COMPANY_CD { get; set; } = string.Empty;
        public long? PRODUCT_ID { get; set; }
        public string? PRODUCT_CD { get; set; } = string.Empty;
        public string? PRODUCT_NM_VIET { get; set; } = string.Empty;
        public string? PRODUCT_NM_ENG { get; set; } = string.Empty;
        public string? PRODUCT_NM_KOR { get; set; } = string.Empty;
        public string? PRODUCT_NM_CHINA { get; set; } = string.Empty;

        public long? STORE_ID { get; set; }
        public string? STORE_CD { get; set; } = string.Empty;
        public string? STORE_NM_VIET { get; set; } = string.Empty;
        public string? STORE_NM_ENG { get; set; } = string.Empty;
        public string? STORE_NM_KOR { get; set; } = string.Empty;
        public string? STORE_NM_CHINA { get; set; } = string.Empty;

        public long? UNIT_ID { get; set; }
        public string? UNIT_CD { get; set; } = string.Empty;
        public string? UNIT_NM_VIET { get; set; } = string.Empty;
        public string? UNIT_NM_ENG { get; set; } = string.Empty;
        public string? UNIT_NM_KOR { get; set; } = string.Empty;
        public string? UNIT_NM_CHINA { get; set; } = string.Empty;

        public decimal QUANTITY { get; set; }
        public decimal UNIT_PRICE_CC { get; set; }
        public string? FC_TYPE { get; set; } = "VND";
        public decimal UNIT_PRICE_FC { get; set; }
        public decimal EXCHANGE_RATES { get; set; }
        public decimal AMOUNT_CC { get; set; }
        public decimal AMOUNT_FC { get; set; }
        public string SUMMARY { get; set; } = string.Empty;
        public string? INVENTORY_YMD { get; set; }
        public string? STATE { get; set; } = "1";
        public long? CHITDETAIL_ID { get; set; }
        public string? CHITDETAIL_CD { get; set; } = string.Empty;
        public int? SORT { get; set; }
        public string? ISDEL { get; set; } = "0";
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }
}

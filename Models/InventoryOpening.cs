namespace API_AMNOTE_WEB.Models
{
    public class InventoryOpening
    {
        public long INPUT_ID { get; set; }
        public string INPUT_CD { get; set; } = string.Empty;
        public long TRANSFER_ID { get; set; }
        public string TRANSFER_CD { get; set; } = string.Empty;
        public string? TRANSFER_NO { get; set; }
        public string? TRANSFER_YMD { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public long? PRODUCT_ID { get; set; }
        public string PRODUCT_CD { get; set; } = string.Empty;
        public string? PRODUCT_NM_VIET { get; set; }
        public string? PRODUCT_NM_ENG { get; set; }
        public string? PRODUCT_NM_KOR { get; set; }
        public string? PRODUCT_NM_CHINA { get; set; }
        public long? STORE_ID { get; set; }
        public string STORE_CD { get; set; } = string.Empty;
        public string? STORE_NM_VIET { get; set; }
        public string? STORE_NM_ENG { get; set; }
        public string? STORE_NM_KOR { get; set; }
        public string? STORE_NM_CHINA { get; set; }
        public long? UNIT_ID { get; set; }
        public string UNIT_CD { get; set; } = string.Empty;
        public string? UNIT_NM { get; set; }
        public decimal QUANTITY { get; set; }
        public decimal UNIT_PRICE_CC { get; set; }
        public decimal AMOUNT_CC { get; set; }
        public string? SUMMARY { get; set; }
        public string? INVENTORY_YMD { get; set; }
        public string STATE { get; set; } = "0";
        public int SORT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class InventoryOpeningRequest
    {
        public long INPUT_ID { get; set; }
        public long TRANSFER_ID { get; set; }
        public long? PRODUCT_ID { get; set; }
        public string? PRODUCT_CD { get; set; }
        public long? STORE_ID { get; set; }
        public string? STORE_CD { get; set; }
        public long? UNIT_ID { get; set; }
        public string? UNIT_CD { get; set; }
        public decimal QUANTITY { get; set; }
        public decimal UNIT_PRICE_CC { get; set; }
        public decimal? AMOUNT_CC { get; set; }
        public string? SUMMARY { get; set; }
    }

    public class InventoryOpeningIdsRequest
    {
        public List<long> InputIds { get; set; } = new();
    }
}

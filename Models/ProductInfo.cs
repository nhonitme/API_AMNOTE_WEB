namespace API_AMNOTE_WEB.Models
{
    public class ProductInfo
    {
        public int PRODUCT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string PRODUCT_CD { get; set; } = string.Empty;
        public string PRODUCT_NM_VIET { get; set; } = string.Empty;
        public string? PRODUCT_NM_ENG { get; set; } = string.Empty;
        public string? PRODUCT_NM_KOR { get; set; } = string.Empty;
        public string? PRODUCT_NM_CHINA { get; set; } = string.Empty;
        public int? PRODUCT_KIND_ID { get; set; }
        public int? UNIT_ID { get; set; }
        public int? STORE_ID { get; set; }
        public string? DIVISION { get; set; } = string.Empty;
        public string? SUMMARY { get; set; } = string.Empty;
        public string? ISDEL { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; } = string.Empty;
    }

    public class ProductIdsRequest
    {
        public List<int> ProductIds { get; set; } = new List<int>();
    }
}

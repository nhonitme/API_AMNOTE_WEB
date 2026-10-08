namespace API_AMNOTE_WEB.Models
{
    public class ProductInfoDto
    {
        public int PRODUCT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string PRODUCT_CD { get; set; } = string.Empty;
        public string PRODUCT_NM_VIET { get; set; } = string.Empty;
        public string? PRODUCT_NM_ENG { get; set; } = string.Empty;
        public string? PRODUCT_NM_KOR { get; set; } = string.Empty;
        public string? PRODUCT_NM_CHINA { get; set; } = string.Empty;
        public int? PRODUCT_KIND_ID { get; set; }
        public string? PRODUCT_KIND_CD { get; set; } = string.Empty;
        public string? PRODUCTKIND_NM_VIET { get; set; } = string.Empty;
        public int? UNIT_ID { get; set; }
        public string? UNIT_CD { get; set; } = string.Empty;
        public string? UNIT_NM { get; set; } = string.Empty;
        public int? STORE_ID { get; set; }
        public string? STORE_CD { get; set; } = string.Empty;
        public string? STORE_NM_VIET { get; set; } = string.Empty;
        public string? DIVISION { get; set; } = string.Empty;
        public string? SUMMARY { get; set; } = string.Empty;
        public string? ISDEL { get; set; } = string.Empty;
    }
}

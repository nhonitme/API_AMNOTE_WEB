namespace API_AMNOTE_WEB.Models
{
    public class ProductKind
    {
        public int PRODUCT_KIND_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string PRODUCT_KIND_CD { get; set; } = string.Empty;
        public string PRODUCTKIND_NM_VIET { get; set; } = string.Empty;
        public string? PRODUCTKIND_NM_ENG { get; set; } = string.Empty;
        public string? PRODUCTKIND_NM_KOR { get; set; } = string.Empty;
        public string? PRODUCTKIND_NM_CHINA { get; set; } = string.Empty;
        public string? REMARK { get; set; } = string.Empty;
        public string? ISDEL { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; } = string.Empty;
    }

    public class ProductKindIdsRequest
    {
        public List<int> ProductKindIds { get; set; } = new List<int>();
    }
}

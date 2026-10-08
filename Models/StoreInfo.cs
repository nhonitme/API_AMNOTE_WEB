using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Models
{
    public class StoreInfo
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public int STORE_ID { get; set; } = 0;
        public string STORE_CD { get; set; } = string.Empty;
        public string STORE_NM_VIET { get; set; } = string.Empty;
        public string STORE_NM_ENG { get; set; } = string.Empty;
        public string STORE_NM_KOR { get; set; } = string.Empty;
        public string STORE_NM_CHINA { get; set; } = string.Empty;
        public int STORE_KIND_ID { get; set; } = 0;
        public string STORE_KIND_CD { get; set; } = string.Empty;
        public string STORE_KIND_NM_VIET { get; set; } = string.Empty;
        public string STORE_KIND_NM_ENG { get; set; } = string.Empty;
        public string STORE_KIND_NM_KOR { get; set; } = string.Empty;
        public string STORE_KIND_NM_CHINA { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string UPDATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime UPDATE_AT { get; set; }
        public int SORT { get; set; } = 1;
        public string ISDEL { get; set; } = "0";
    }


    public class StoreInfoRequest
    {
        public int STORE_ID { get; set; } = 0;
        public string STORE_CD { get; set; } = string.Empty;
        public string STORE_NM_VIET { get; set; } = string.Empty;
        public string STORE_NM_ENG { get; set; } = string.Empty;
        public string STORE_NM_KOR { get; set; } = string.Empty;
        public string STORE_NM_CHINA { get; set; } = string.Empty;
        public int STORE_KIND_ID { get; set; } = 0;
    }

    public class DeleteStoresRequest
    {
        public List<int> StoreIds { get; set; } = new();
    }
}

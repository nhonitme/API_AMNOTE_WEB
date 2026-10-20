namespace API_AMNOTE_WEB.Models
{
    public sealed class CostObjectSetupItemDto
    {
        public int PRODUCT_ID { get; set; }
        public string PRODUCT_CD { get; set; } = string.Empty;
        public string PRODUCT_NM_VIET { get; set; } = string.Empty;
        public string? PRODUCT_NM_ENG { get; set; }
        public string? DIVISION { get; set; }
        public long? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CD { get; set; }
        public string? DEP_NAME_VIET { get; set; }
        public string STATUS { get; set; } = "MISSING";
    }

    public sealed class CostObjectSetupOverviewDto
    {
        public string DIVISION_PREFIX { get; set; } = "155";
        public int TOTAL { get; set; }
        public int REGISTERED { get; set; }
        public int MISSING { get; set; }
        public List<CostObjectSetupItemDto> ITEMS { get; set; } = new();
    }

    public sealed class CostObjectSyncRequest
    {
        public List<int> ProductIds { get; set; } = new();
        public string? DivisionPrefix { get; set; }
    }

    public sealed class CostObjectSyncItemDto
    {
        public int PRODUCT_ID { get; set; }
        public string PRODUCT_CD { get; set; } = string.Empty;
        public string STATUS { get; set; } = string.Empty;
        public string MESSAGE { get; set; } = string.Empty;
    }

    public sealed class CostObjectSyncResultDto
    {
        public int CREATED { get; set; }
        public int EXISTS { get; set; }
        public int FAILED { get; set; }
        public List<CostObjectSyncItemDto> ITEMS { get; set; } = new();
    }
}

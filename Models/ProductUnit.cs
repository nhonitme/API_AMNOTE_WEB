namespace API_AMNOTE_WEB.Models
{
    public class ProductUnit
    {
        public int UNIT_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string UNIT_CD { get; set; } = string.Empty;
        public string? UNIT_NM { get; set; } = string.Empty;
        public string? ISDEL { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; } = string.Empty;
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; } = string.Empty;
    }

    public class UnitIdsRequest
    {
        public List<int> UnitIds { get; set; } = new List<int>();
    }
}

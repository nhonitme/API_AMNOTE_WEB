namespace API_AMNOTE_WEB.Models.DTOs
{
    public class SysGridColumnSettingSaveRequest
    {
        public string? USER_ID { get; set; }
        public string? GRID_ID { get; set; }
        public long? TEMPLATE_ID { get; set; }
        public string? TEMPLATE_NAME { get; set; }
        public string? IS_DEFAULT_TEMPLATE { get; set; }
        public List<SysGridColumnSettingSaveItemRequest> COLUMNS { get; set; } = new();
    }
}

namespace API_AMNOTE_WEB.Models.DTOs
{
    public class SysGridColumnSettingSaveItemRequest
    {
        public string? FIELD_NAME { get; set; }
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
        public string? IS_VISIBLE { get; set; }
        public int? VISIBLE_INDEX { get; set; }
        public int? COLUMN_WIDTH { get; set; }
        public string? IS_FIXED { get; set; }
        public string? FIXED_POSITION { get; set; }
        public string? ALLOW_HIDING { get; set; }
        public string? SORT_ORDER { get; set; }
        public int? SORT_INDEX { get; set; }
    }
}

namespace API_AMNOTE_WEB.Models
{
    public class SysDecimalFieldSetting : SysDecimalSetting
    {
        public string FIELD_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
    }
}

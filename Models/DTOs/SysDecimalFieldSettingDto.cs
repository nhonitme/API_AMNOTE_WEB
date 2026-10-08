namespace API_AMNOTE_WEB.Models.DTOs
{
    public class SysDecimalFieldSettingDto : SysDecimalSettingDto
    {
        public string FIELD_NAME { get; set; } = string.Empty;
        public string? LABEL_TEXT { get; set; }
        public string? CAPTION { get; set; }
    }
}

namespace API_AMNOTE_WEB.Models.DTOs
{
    public class SysConfigDto
    {
        public long ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string CONFIG_GROUP { get; set; } = string.Empty;
        public string CONFIG_KEY { get; set; } = string.Empty;
        public string? CONFIG_VALUE { get; set; }
        public string DATA_TYPE { get; set; } = "string";
        public string? DESCRIPTION { get; set; }
        public string IS_ACTIVE { get; set; } = "1";
    }
}

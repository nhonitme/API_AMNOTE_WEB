using System.ComponentModel.DataAnnotations;

namespace API_AMNOTE_WEB.Models
{
    public class t_message_info
    {
        [Key]
        public string KEY { get; set; } = string.Empty;

        public string KOR { get; set; } = string.Empty;
        public string ENG { get; set; } = string.Empty;
        public string VIET { get; set; } = string.Empty;
        public string JPN { get; set; } = string.Empty;
        public string THA { get; set; } = string.Empty;
        public string CHN { get; set; } = string.Empty;
        public string COMMENT { get; set; } = string.Empty;
        public DateTime? REG_DATE { get; set; }
        public DateTime? LAST_MIDOFY_DATE { get; set; }
    }
}

using System;

namespace API_AMNOTE_WEB.Models.DTOs
{
    public class UserInfoDto
    {
        public long USER_PK_ID { get; set; }
        public long? USER_COMPANY_ID { get; set; }
        public string USERID { get; set; } = string.Empty;
        public string USERNM { get; set; } = string.Empty;
        public string? EMAIL { get; set; }
        public string? MOBILE_NO { get; set; }
        public string? AVATAR_URL { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public int? USERLV { get; set; }
        public string? ROLE_CODE { get; set; }
        public string DEFAULT_YN { get; set; } = "0";
        public string IS_ACTIVE { get; set; } = "1";
        public string ISDEL { get; set; } = "0";
    }
}

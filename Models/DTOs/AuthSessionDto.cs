namespace API_AMNOTE_WEB.Models.DTOs
{
    public class AuthSessionDto
    {
        public bool IsAuthenticated { get; set; }
        public string DefaultCompanyCd { get; set; } = string.Empty;
        public long UserPkId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Lang { get; set; } = "VIET";
        public string[] Roles { get; set; } = Array.Empty<string>();
        public List<AuthCompanyDto> Companies { get; set; } = new();
    }

    public class AuthCompanyDto
    {
        public string COMPANY_CD { get; set; } = string.Empty;
        public string COMPANY_NM { get; set; } = string.Empty;
        public string USERID { get; set; } = string.Empty;
        public int? USERLV { get; set; }
        public string ROLE_CODE { get; set; } = string.Empty;
        public string DEFAULT_YN { get; set; } = "0";
    }
}

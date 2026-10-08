using System;
using System.Collections.Generic;

namespace API_AMNOTE_WEB.Models
{
    public class UserInfo
    {
        public long USER_PK_ID { get; set; }
        public long? USER_COMPANY_ID { get; set; }
        public string USERID { get; set; } = string.Empty;
        public string PASSWD { get; set; } = string.Empty;
        public string USERNM { get; set; } = string.Empty;
        public string? EMAIL { get; set; }
        public string? MOBILE_NO { get; set; }
        public string? AVATAR_URL { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public int? USERLV { get; set; }
        public string? ROLE_CODE { get; set; }
        public string? DEFAULT_YN { get; set; }
        public string? IS_ACTIVE { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class UserInfoRequest
    {
        public long? USER_PK_ID { get; set; }
        public long? USER_COMPANY_ID { get; set; }
        public string? USERID { get; set; }
        public string? PASSWD { get; set; }
        public string? USERNM { get; set; }
        public string? EMAIL { get; set; }
        public string? MOBILE_NO { get; set; }
        public string? AVATAR_URL { get; set; }
        public int? USERLV { get; set; }
        public string? ROLE_CODE { get; set; }
        public string? DEFAULT_YN { get; set; }
        public string? IS_ACTIVE { get; set; }
        public string? ISDEL { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? UPDATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public string? CREATE_BY { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? CREATE_AT { get; set; }
        [API_AMNOTE_WEB.Helpers.BackendAudit]
        public DateTime? UPDATE_AT { get; set; }
    }

    public class DeleteUserInfosRequest
    {
        public List<long> UserPkIds { get; set; } = new();
    }
}

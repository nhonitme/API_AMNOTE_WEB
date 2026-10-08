using System;

namespace API_AMNOTE_WEB.Models
{
    public class UserProfile
    {
        public long USER_PK_ID { get; set; }
        public long? USER_DETAIL_ID { get; set; }
        public string COMPANY_CD { get; set; } = string.Empty;
        public string USERID { get; set; } = string.Empty;
        public string USERNM { get; set; } = string.Empty;
        public int? USERLV { get; set; }
        public string? EMPLOYEE_CD { get; set; }
        public string? EMPLOYEE_NM { get; set; }
        public string? EMPLOYEE_NM_ENG { get; set; }
        public long? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CD { get; set; }
        public long? POSITION_ID { get; set; }
        public string? POSITION_CD { get; set; }
        public long? BRANCH_ID { get; set; }
        public string? BRANCH_CD { get; set; }
        public string? EMAIL { get; set; }
        public string? MOBILE_NO { get; set; }
        public string? TEL_NO { get; set; }
        public string? ADDRESS { get; set; }
        public DateTime? BIRTHDAY { get; set; }
        public string? GENDER { get; set; }
        public string? AVATAR_URL { get; set; }
        public string? SIGN_IMAGE_URL { get; set; }
        public string? NOTE { get; set; }
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

    public class UserProfileUpdateRequest
    {
        public string? USERNM { get; set; }
        public string? EMPLOYEE_CD { get; set; }
        public string? EMPLOYEE_NM { get; set; }
        public string? EMPLOYEE_NM_ENG { get; set; }
        public long? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CD { get; set; }
        public long? POSITION_ID { get; set; }
        public string? POSITION_CD { get; set; }
        public long? BRANCH_ID { get; set; }
        public string? BRANCH_CD { get; set; }
        public string? EMAIL { get; set; }
        public string? MOBILE_NO { get; set; }
        public string? TEL_NO { get; set; }
        public string? ADDRESS { get; set; }
        public DateTime? BIRTHDAY { get; set; }
        public string? GENDER { get; set; }
        public string? AVATAR_URL { get; set; }
        public string? SIGN_IMAGE_URL { get; set; }
        public string? NOTE { get; set; }
    }

    public class ChangeUserProfilePasswordRequest
    {
        public string? CURRENT_PASSWORD { get; set; }
        public string? NEW_PASSWORD { get; set; }
        public string? CONFIRM_PASSWORD { get; set; }
    }
}

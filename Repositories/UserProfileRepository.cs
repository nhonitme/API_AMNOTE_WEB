using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Linq;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public class UserProfileRepository : IUserProfileRepository
    {
        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;

        public UserProfileRepository(DapperExecutor db, IActivityLogService activityLogService)
        {
            _db = db;
            _activityLogService = activityLogService;
        }

        public async Task<UserProfile?> GetUserProfileAsync(string companyCd, string userId)
        {
            const string query = "CALL getUserProfile(@p_COMPANY_CD, @p_USERID)";

            return (await _db.QueryAsync<UserProfile>(Net_DB.Net_DB_Manager, query, new
            {
                p_COMPANY_CD = companyCd,
                p_USERID = userId
            })).FirstOrDefault();
        }

        public async Task<int> UpdateUserProfileAsync(string companyCd, string modifierUserId, UserProfileUpdateRequest request)
        {
            var normalizedRequest = NormalizeRequest(request);
            var targetUserId = Common.GetUserId();
            var existing = await GetUserProfileAsync(companyCd, targetUserId);
            var oldData = existing == null ? string.Empty : JsonSerializer.Serialize(existing);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                const string query = @"CALL setUserProfile(
                    @p_COMPANY_CD,
                    @p_USERID,
                    @p_USERNM,
                    @p_EMPLOYEE_CD,
                    @p_EMPLOYEE_NM,
                    @p_EMPLOYEE_NM_ENG,
                    @p_DEPARTMENT_ID,
                    @p_DEPARTMENT_CD,
                    @p_POSITION_ID,
                    @p_POSITION_CD,
                    @p_BRANCH_ID,
                    @p_BRANCH_CD,
                    @p_EMAIL,
                    @p_MOBILE_NO,
                    @p_TEL_NO,
                    @p_ADDRESS,
                    @p_BIRTHDAY,
                    @p_GENDER,
                    @p_AVATAR_URL,
                    @p_SIGN_IMAGE_URL,
                    @p_NOTE,
                    @p_MODIFIER_USERID)";

                var result = await session.ExecuteAsync(query, new
                {
                    p_COMPANY_CD = companyCd,
                    p_USERID = targetUserId,
                    p_USERNM = normalizedRequest.USERNM,
                    p_EMPLOYEE_CD = normalizedRequest.EMPLOYEE_CD,
                    p_EMPLOYEE_NM = normalizedRequest.EMPLOYEE_NM,
                    p_EMPLOYEE_NM_ENG = normalizedRequest.EMPLOYEE_NM_ENG,
                    p_DEPARTMENT_ID = normalizedRequest.DEPARTMENT_ID,
                    p_DEPARTMENT_CD = normalizedRequest.DEPARTMENT_CD,
                    p_POSITION_ID = normalizedRequest.POSITION_ID,
                    p_POSITION_CD = normalizedRequest.POSITION_CD,
                    p_BRANCH_ID = normalizedRequest.BRANCH_ID,
                    p_BRANCH_CD = normalizedRequest.BRANCH_CD,
                    p_EMAIL = normalizedRequest.EMAIL,
                    p_MOBILE_NO = normalizedRequest.MOBILE_NO,
                    p_TEL_NO = normalizedRequest.TEL_NO,
                    p_ADDRESS = normalizedRequest.ADDRESS,
                    p_BIRTHDAY = normalizedRequest.BIRTHDAY,
                    p_GENDER = normalizedRequest.GENDER,
                    p_AVATAR_URL = normalizedRequest.AVATAR_URL,
                    p_SIGN_IMAGE_URL = normalizedRequest.SIGN_IMAGE_URL,
                    p_NOTE = normalizedRequest.NOTE,
                    p_MODIFIER_USERID = modifierUserId
                });

                if (result > 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        companyCd,
                        "UPDATE",
                        "UserProfile",
                        "user_info,user_company",
                        targetUserId,
                        oldData,
                        SerializeAuditRequest(normalizedRequest),
                        "Update current user profile");
                }

                session.Commit();
                return result;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<int> ChangePasswordAsync(string companyCd, string userId, string modifierUserId, string encryptedPassword)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                const string query = "CALL changeUserProfilePassword(@p_COMPANY_CD, @p_USERID, @p_PASSWD, @p_MODIFIER_USERID)";
                var result = await session.ExecuteAsync(query, new
                {
                    p_COMPANY_CD = companyCd,
                    p_USERID = userId,
                    p_PASSWD = encryptedPassword,
                    p_MODIFIER_USERID = modifierUserId
                });

                if (result > 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        companyCd,
                        "UPDATE",
                        "UserProfilePassword",
                        "user_info",
                        userId,
                        JsonSerializer.Serialize(new { USERID = userId }),
                        JsonSerializer.Serialize(new { USERID = userId, PASSWD = "***" }),
                        "Change current user password");
                }

                session.Commit();
                return result;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static UserProfileUpdateRequest NormalizeRequest(UserProfileUpdateRequest request)
        {
            return new UserProfileUpdateRequest
            {
                USERNM = Common.NormalizeNullableText(request.USERNM),
                EMPLOYEE_CD = Common.NormalizeNullableText(request.EMPLOYEE_CD),
                EMPLOYEE_NM = Common.NormalizeNullableText(request.EMPLOYEE_NM),
                EMPLOYEE_NM_ENG = Common.NormalizeNullableText(request.EMPLOYEE_NM_ENG),
                DEPARTMENT_ID = NormalizeNullablePositiveLong(request.DEPARTMENT_ID),
                DEPARTMENT_CD = Common.NormalizeNullableText(request.DEPARTMENT_CD),
                POSITION_ID = NormalizeNullablePositiveLong(request.POSITION_ID),
                POSITION_CD = Common.NormalizeNullableText(request.POSITION_CD),
                BRANCH_ID = NormalizeNullablePositiveLong(request.BRANCH_ID),
                BRANCH_CD = Common.NormalizeNullableText(request.BRANCH_CD),
                EMAIL = Common.NormalizeNullableText(request.EMAIL),
                MOBILE_NO = Common.NormalizeNullableText(request.MOBILE_NO),
                TEL_NO = Common.NormalizeNullableText(request.TEL_NO),
                ADDRESS = Common.NormalizeNullableText(request.ADDRESS),
                BIRTHDAY = request.BIRTHDAY?.Date,
                GENDER = Common.NormalizeNullableText(request.GENDER),
                AVATAR_URL = Common.NormalizeNullableText(request.AVATAR_URL),
                SIGN_IMAGE_URL = Common.NormalizeNullableText(request.SIGN_IMAGE_URL),
                NOTE = Common.NormalizeNullableText(request.NOTE)
            };
        }

        private static long? NormalizeNullablePositiveLong(long? value)
        {
            if (!value.HasValue || value.Value <= 0)
            {
                return null;
            }

            return value.Value;
        }

        private static string SerializeAuditRequest(UserProfileUpdateRequest request)
        {
            return JsonSerializer.Serialize(new
            {
                request.USERNM,
                request.EMPLOYEE_CD,
                request.EMPLOYEE_NM,
                request.EMPLOYEE_NM_ENG,
                request.DEPARTMENT_ID,
                request.DEPARTMENT_CD,
                request.POSITION_ID,
                request.POSITION_CD,
                request.BRANCH_ID,
                request.BRANCH_CD,
                request.EMAIL,
                request.MOBILE_NO,
                request.TEL_NO,
                request.ADDRESS,
                request.BIRTHDAY,
                request.GENDER,
                request.AVATAR_URL,
                request.SIGN_IMAGE_URL,
                request.NOTE
            });
        }
    }
}

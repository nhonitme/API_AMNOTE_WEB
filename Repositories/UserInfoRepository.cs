using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public class UserInfoRepository : IUserInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly IExistenceCheckService _existenceCheckService;
        private readonly IActivityLogService _activityLogService;

        public UserInfoRepository(DapperExecutor db, IExistenceCheckService existenceCheckService, IActivityLogService activityLogService)
        {
            _db = db;
            _existenceCheckService = existenceCheckService;
            _activityLogService = activityLogService;
        }

        public async Task<IEnumerable<UserInfo>> GetUserInfoAsync(string companyCd, long? userPkId = null, string? userId = null)
        {
            const string query = "CALL getUserInfo(@p_COMPANY_CD, @p_USER_PK_ID, @p_USERID)";

            return await _db.QueryAsync<UserInfo>(Net_DB.Net_DB_Manager, query, new
            {
                p_COMPANY_CD = companyCd,
                p_USER_PK_ID = userPkId,
                p_USERID = userId
            });
        }

        public async Task<int> SetUserInfoAsync(string companyCd, string userId, UserInfoRequest request)
        {
            var normalizedRequest = NormalizeRequest(request);
            var existing = normalizedRequest.USER_PK_ID.HasValue && normalizedRequest.USER_PK_ID.Value > 0
                ? (await GetUserInfoAsync(companyCd, normalizedRequest.USER_PK_ID, null)).FirstOrDefault()
                : (await GetUserInfoAsync(companyCd, null, normalizedRequest.USERID)).FirstOrDefault();

            var oldData = existing == null ? string.Empty : JsonSerializer.Serialize(existing);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                const string query = @"CALL setUserInfo(
                    @p_USER_PK_ID,
                    @p_COMPANY_CD,
                    @p_USERID,
                    @p_PASSWD,
                    @p_USERNM,
                    @p_EMAIL,
                    @p_MOBILE_NO,
                    @p_AVATAR_URL,
                    @p_USERLV,
                    @p_ROLE_CODE,
                    @p_DEFAULT_YN,
                    @p_IS_ACTIVE,
                    @p_ISDEL,
                    @p_MODIFIER_USERID)";

                var result = await session.ExecuteAsync(query, new
                {
                    p_USER_PK_ID = normalizedRequest.USER_PK_ID ?? 0,
                    p_COMPANY_CD = companyCd,
                    p_USERID = normalizedRequest.USERID,
                    p_PASSWD = normalizedRequest.PASSWD,
                    p_USERNM = normalizedRequest.USERNM,
                    p_EMAIL = normalizedRequest.EMAIL,
                    p_MOBILE_NO = normalizedRequest.MOBILE_NO,
                    p_AVATAR_URL = normalizedRequest.AVATAR_URL,
                    p_USERLV = normalizedRequest.USERLV,
                    p_ROLE_CODE = normalizedRequest.ROLE_CODE,
                    p_DEFAULT_YN = normalizedRequest.DEFAULT_YN,
                    p_IS_ACTIVE = normalizedRequest.IS_ACTIVE,
                    p_ISDEL = normalizedRequest.ISDEL,
                    p_MODIFIER_USERID = userId
                });

                if (result > 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        companyCd,
                        existing == null ? "INSERT" : "UPDATE",
                        "UserInfo",
                        "user_info,user_company",
                        normalizedRequest.USERID ?? existing?.USERID ?? string.Empty,
                        oldData,
                        SerializeAuditRequest(normalizedRequest),
                        "Upsert user_info");
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

        public async Task<int> DeleteUserInfoAsync(string companyCd, string userId, List<long> userPkIds)
        {
            if (userPkIds == null || userPkIds.Count == 0)
            {
                return 0;
            }

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                const string deleteQuery = "CALL delUserInfo(@p_COMPANY_CD, @p_USER_PK_ID, @p_MODIFIER_USERID)";
                var affectedRows = 0;

                foreach (var userPkId in userPkIds.Distinct())
                {
                    if (userPkId <= 0)
                    {
                        continue;
                    }

                    var existing = (await GetUserInfoAsync(companyCd, userPkId, null)).FirstOrDefault();
                    if (existing == null)
                    {
                        continue;
                    }

                    var result = await session.ExecuteAsync(deleteQuery, new
                    {
                        p_COMPANY_CD = companyCd,
                        p_USER_PK_ID = userPkId,
                        p_MODIFIER_USERID = userId
                    });

                    if (result > 0)
                    {
                        affectedRows += result;
                        await DeleteActivityLogHelper.LogDeleteAsync(
                            _activityLogService,
                            session.Connection,
                            session.Transaction,
                            companyCd,
                            "UserInfo",
                            "user_company",
                            DeleteActivityLogEntry.From(
                                userPkId,
                                existing.USERID ?? userPkId.ToString(CultureInfo.InvariantCulture),
                                existing),
                            "Delete user_info");
                    }
                }

                session.Commit();
                return affectedRows;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<bool> UserIdExistsAsync(string companyCd, string userId, long? userPkId = null)
        {
            return await _existenceCheckService.ExistsAsync(
                "user_company",
                "USERID",
                userId,
                companyCd,
                companyField: "COMPANY_CD",
                idField: "USER_PK_ID",
                excludeId: userPkId,
                db: Net_DB.Net_DB_Manager);
        }

        private static UserInfoRequest NormalizeRequest(UserInfoRequest request)
        {
            return new UserInfoRequest
            {
                USER_PK_ID = request.USER_PK_ID,
                USER_COMPANY_ID = request.USER_COMPANY_ID,
                USERID = Common.NormalizeNullableText(request.USERID),
                PASSWD = Common.NormalizeNullableText(request.PASSWD),
                USERNM = Common.NormalizeNullableText(request.USERNM),
                EMAIL = Common.NormalizeNullableText(request.EMAIL),
                MOBILE_NO = Common.NormalizeNullableText(request.MOBILE_NO),
                AVATAR_URL = Common.NormalizeNullableText(request.AVATAR_URL),
                USERLV = Common.NormalizeUserLevel(request.USERLV),
                ROLE_CODE = Common.DeriveRoleCode(Common.NormalizeUserLevel(request.USERLV)),
                DEFAULT_YN = Common.NormalizeFlagString(request.DEFAULT_YN, "0"),
                IS_ACTIVE = Common.NormalizeFlagString(request.IS_ACTIVE, "1"),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0"),
                UPDATE_BY = Common.NormalizeNullableText(request.UPDATE_BY),
                CREATE_BY = Common.NormalizeNullableText(request.CREATE_BY),
                CREATE_AT = request.CREATE_AT,
                UPDATE_AT = request.UPDATE_AT
            };
        }

        private static string SerializeAuditRequest(UserInfoRequest request)
        {
            return JsonSerializer.Serialize(new
            {
                request.USER_PK_ID,
                request.USER_COMPANY_ID,
                request.USERID,
                PASSWD = string.IsNullOrWhiteSpace(request.PASSWD) ? null : "***",
                request.USERNM,
                request.EMAIL,
                request.MOBILE_NO,
                request.AVATAR_URL,
                request.USERLV,
                request.ROLE_CODE,
                request.DEFAULT_YN,
                request.IS_ACTIVE,
                request.ISDEL,
                request.UPDATE_BY,
                request.CREATE_BY,
                request.CREATE_AT,
                request.UPDATE_AT
            });
        }
    }
}

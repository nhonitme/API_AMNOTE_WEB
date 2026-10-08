using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public class UserSettingRepository : IUserSettingRepository
    {
        private const string GetQuery = "CALL sp_user_setting_info_get(@p_COMPANY_CD, @p_USER_ID, @p_KEY_NAME)";
        private const string SetQuery = "CALL sp_user_setting_info_set(@p_COMPANY_CD, @p_USER_ID, @p_KEY_NAME, @p_VALUE, @p_NOTE)";
        private const string DeleteQuery = "CALL sp_user_setting_info_delete(@p_COMPANY_CD, @p_USER_ID, @p_KEY_NAME)";

        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;

        public UserSettingRepository(DapperExecutor db, IActivityLogService activityLogService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
        }

        public async Task<IReadOnlyList<UserSettingInfo>> GetCandidatesAsync(string companyCd, string userId, string? keyName = null)
        {
            var items = await _db.QueryAsync<UserSettingInfo>(Net_DB.Net_DB_Company, GetQuery, new
            {
                p_COMPANY_CD = NormalizeScopeValue(companyCd),
                p_USER_ID = NormalizeScopeValue(userId),
                p_KEY_NAME = NormalizeKeyName(keyName)
            });

            return items.ToList();
        }

        public async Task<UserSettingInfo?> GetExactAsync(string companyCd, string userId, string keyName)
        {
            var normalizedCompanyCd = NormalizeScopeValue(companyCd);
            var normalizedUserId = NormalizeScopeValue(userId);
            var normalizedKeyName = NormalizeRequiredKeyName(keyName);

            var items = await GetCandidatesAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            return items.FirstOrDefault(item =>
                item.COMPANY_CD.Equals(normalizedCompanyCd, StringComparison.OrdinalIgnoreCase) &&
                item.USER_ID.Equals(normalizedUserId, StringComparison.OrdinalIgnoreCase) &&
                item.KEY_NAME.Equals(normalizedKeyName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<int> UpsertAsync(string companyCd, string userId, string keyName, string value, string note)
        {
            var normalizedCompanyCd = NormalizeScopeValue(companyCd);
            var normalizedUserId = NormalizeScopeValue(userId);
            var normalizedKeyName = NormalizeRequiredKeyName(keyName);
            var normalizedValue = NormalizeValue(value);
            var normalizedNote = NormalizeNote(note);

            var existing = await GetExactAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            var oldData = existing == null ? string.Empty : JsonSerializer.Serialize(existing);
            var newData = JsonSerializer.Serialize(new
            {
                COMPANY_CD = normalizedCompanyCd,
                USER_ID = normalizedUserId,
                KEY_NAME = normalizedKeyName,
                VALUE = normalizedValue,
                NOTE = normalizedNote
            });

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var result = await session.ExecuteAsync(SetQuery, new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_USER_ID = normalizedUserId,
                    p_KEY_NAME = normalizedKeyName,
                    p_VALUE = normalizedValue,
                    p_NOTE = normalizedNote
                });

                if (result >= 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        normalizedCompanyCd,
                        existing == null ? "INSERT" : "UPDATE",
                        "UserSettingInfo",
                        "user_setting_info",
                        normalizedKeyName,
                        oldData,
                        newData,
                        $"Upsert user_setting_info {normalizedKeyName}");
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

        public async Task<int> DeleteAsync(string companyCd, string userId, string keyName)
        {
            var normalizedCompanyCd = NormalizeScopeValue(companyCd);
            var normalizedUserId = NormalizeScopeValue(userId);
            var normalizedKeyName = NormalizeRequiredKeyName(keyName);

            var existing = await GetExactAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            if (existing == null)
            {
                return 0;
            }

            var oldData = JsonSerializer.Serialize(existing);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var result = await session.ExecuteAsync(DeleteQuery, new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_USER_ID = normalizedUserId,
                    p_KEY_NAME = normalizedKeyName
                });

                if (result > 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        normalizedCompanyCd,
                        "DELETE",
                        "UserSettingInfo",
                        "user_setting_info",
                        normalizedKeyName,
                        oldData,
                        string.Empty,
                        $"Delete user_setting_info {normalizedKeyName}");
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

        private static string NormalizeScopeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string? NormalizeKeyName(string? keyName)
        {
            return string.IsNullOrWhiteSpace(keyName) ? null : keyName.Trim();
        }

        private static string NormalizeRequiredKeyName(string keyName)
        {
            var normalized = Common.NormalizeRequiredText(keyName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("KEY_NAME is required");
            }

            if (normalized.Length > 100)
            {
                throw new ArgumentException("KEY_NAME length must be less than or equal to 100");
            }

            return normalized;
        }

        private static string NormalizeValue(string? value)
        {
            var normalized = Common.NormalizeNullableText(value) ?? string.Empty;
            if (normalized.Length > 250)
            {
                throw new ArgumentException("VALUE length must be less than or equal to 250");
            }

            return normalized;
        }

        private static string NormalizeNote(string? note)
        {
            var normalized = Common.NormalizeNullableText(note) ?? string.Empty;
            if (normalized.Length > 250)
            {
                throw new ArgumentException("NOTE length must be less than or equal to 250");
            }

            return normalized;
        }
    }
}

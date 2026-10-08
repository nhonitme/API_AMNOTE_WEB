using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceSettingRepository : IEInvoiceSettingRepository
    {
        private const string DecimalSettingsCacheScope = "einvoice-decimal-setting";
        private const string UserSettingsCacheScope = "einvoice-user-setting";
        private const string GetDecimalSettingsQuery = "CALL getEInvoiceDecimalSetting(@p_COMPANY_CD, @p_XSL_ID, @p_SETTING_ID, @p_APPLY_TARGET, @p_FIELD_SCOPE, @p_KEYWORD, @p_INCLUDE_INACTIVE)";
        private const string GetUserSettingsQuery = "CALL getEInvoiceUserSetting(@p_COMPANY_CD, @p_SETTING_ID, @p_USER_ID, @p_KEYWORD, @p_INCLUDE_DELETED)";
        private const string GetAdminSettingsQuery = "CALL getEInvoiceAdminSetting(@p_COMPANY_CD, @p_SETTING_ID, @p_SETTING_TYPE, @p_KEYWORD, @p_INCLUDE_DELETED)";
        private const string SetUserSettingQuery = @"CALL setEInvoiceUserSetting(
            @p_SETTING_ID,
            @p_COMPANY_CD,
            @p_USER_ID,
            @p_SETTING_KEY,
            @p_SETTING_VALUE,
            @p_VALUE_TYPE,
            @p_USERID
        )";
        private const string SetDecimalSettingQuery = @"CALL setEInvoiceDecimalSetting(
            @p_SETTING_ID,
            @p_COMPANY_CD,
            @p_XSL_ID,
            @p_APPLY_TARGET,
            @p_FIELD_SCOPE,
            @p_FIELD_NAME,
            @p_LABEL_TEXT,
            @p_CAPTION,
            @p_CURRENCY_SCOPE,
            @p_DECIMAL_SCALE,
            @p_ROUND_MODE,
            @p_IS_ACTIVE,
            @p_SORT_ORDER,
            @p_NOTE,
            @p_USERID
        )";

        private readonly DapperExecutor _db;
        private readonly IMasterDataCacheService _cache;

        public EInvoiceSettingRepository(DapperExecutor db, IMasterDataCacheService cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<IEnumerable<EInvoiceDecimalSetting>> GetDecimalSettingsAsync(string companyCd, long? settingId, string? applyTarget, string? fieldScope, string? keyword, bool includeInactive, long? xslId = null)
        {
            var cacheKey = BuildDecimalSettingsCacheKey(settingId, xslId, applyTarget, fieldScope, keyword, includeInactive);
            var rows = await _cache.GetOrCreateAsync(
                DecimalSettingsCacheScope,
                companyCd,
                cacheKey,
                async () => (await QueryDecimalSettingsAsync(companyCd, settingId, applyTarget, fieldScope, keyword, includeInactive, xslId)).ToList());

            return rows;
        }

        public Task ClearDecimalSettingsCacheAsync(string companyCd)
            => _cache.ClearAsync(DecimalSettingsCacheScope, companyCd);

        private async Task<IEnumerable<EInvoiceDecimalSetting>> QueryDecimalSettingsAsync(string companyCd, long? settingId, string? applyTarget, string? fieldScope, string? keyword, bool includeInactive, long? xslId)
        {
            return await _db.QueryAsync<EInvoiceDecimalSetting>(Net_DB.Net_DB_Company, GetDecimalSettingsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_XSL_ID = xslId ?? 0,
                p_SETTING_ID = settingId ?? 0,
                p_APPLY_TARGET = applyTarget,
                p_FIELD_SCOPE = fieldScope,
                p_KEYWORD = keyword,
                p_INCLUDE_INACTIVE = includeInactive ? 1 : 0
            });
        }

        private static string BuildDecimalSettingsCacheKey(long? settingId, long? xslId, string? applyTarget, string? fieldScope, string? keyword, bool includeInactive)
        {
            return string.Join('|',
                $"xslId={xslId ?? 0}",
                $"settingId={settingId ?? 0}",
                $"applyTarget={applyTarget ?? string.Empty}",
                $"fieldScope={fieldScope ?? string.Empty}",
                $"keyword={keyword ?? string.Empty}",
                $"includeInactive={(includeInactive ? 1 : 0)}");
        }

        public Task<long> SetDecimalSettingAsync(DapperSession session, string companyCd, string userId, EInvoiceDecimalSetting setting)
        {
            return session.QuerySingleAsync<long>(SetDecimalSettingQuery, new
            {
                p_SETTING_ID = setting.SETTING_ID,
                p_COMPANY_CD = companyCd,
                p_XSL_ID = setting.XSL_ID,
                p_APPLY_TARGET = setting.APPLY_TARGET,
                p_FIELD_SCOPE = setting.FIELD_SCOPE,
                p_FIELD_NAME = setting.FIELD_NAME,
                p_LABEL_TEXT = setting.LABEL_TEXT,
                p_CAPTION = setting.CAPTION,
                p_CURRENCY_SCOPE = setting.CURRENCY_SCOPE,
                p_DECIMAL_SCALE = setting.DECIMAL_SCALE,
                p_ROUND_MODE = setting.ROUND_MODE,
                p_IS_ACTIVE = setting.IS_ACTIVE,
                p_SORT_ORDER = setting.SORT_ORDER,
                p_NOTE = setting.NOTE,
                p_USERID = userId
            });
        }

        public async Task<IEnumerable<EInvoiceUserSetting>> GetUserSettingsAsync(string companyCd, long? settingId, string? userId, string? keyword, bool includeDeleted)
        {
            var cacheKey = BuildUserSettingsCacheKey(settingId, userId, keyword, includeDeleted);
            var rows = await _cache.GetOrCreateAsync(
                UserSettingsCacheScope,
                companyCd,
                cacheKey,
                async () => (await QueryUserSettingsAsync(companyCd, settingId, userId, keyword, includeDeleted)).ToList());

            return rows;
        }

        public Task ClearUserSettingsCacheAsync(string companyCd)
            => _cache.ClearAsync(UserSettingsCacheScope, companyCd);

        private async Task<IEnumerable<EInvoiceUserSetting>> QueryUserSettingsAsync(string companyCd, long? settingId, string? userId, string? keyword, bool includeDeleted)
        {
            return await _db.QueryAsync<EInvoiceUserSetting>(Net_DB.Net_DB_Company, GetUserSettingsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_SETTING_ID = settingId ?? 0,
                p_USER_ID = userId,
                p_KEYWORD = keyword,
                p_INCLUDE_DELETED = includeDeleted ? 1 : 0
            });
        }

        private static string BuildUserSettingsCacheKey(long? settingId, string? userId, string? keyword, bool includeDeleted)
        {
            return string.Join('|',
                $"settingId={settingId ?? 0}",
                $"userId={userId ?? string.Empty}",
                $"keyword={keyword ?? string.Empty}",
                $"includeDeleted={(includeDeleted ? 1 : 0)}");
        }

        public Task<long> SetUserSettingAsync(DapperSession session, string userId, EInvoiceUserSetting setting)
        {
            return session.QuerySingleAsync<long>(SetUserSettingQuery, new
            {
                p_SETTING_ID = setting.SETTING_ID,
                p_COMPANY_CD = setting.COMPANY_CD,
                p_USER_ID = setting.USER_ID,
                p_SETTING_KEY = setting.SETTING_KEY,
                p_SETTING_VALUE = setting.SETTING_VALUE,
                p_VALUE_TYPE = setting.VALUE_TYPE,
                p_USERID = userId
            });
        }

        public async Task<IEnumerable<EInvoiceAdminSetting>> GetAdminSettingsAsync(string companyCd, long? settingId, string? settingType, string? keyword, bool includeDeleted)
        {
            return await _db.QueryAsync<EInvoiceAdminSetting>(Net_DB.Net_DB_Company, GetAdminSettingsQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_SETTING_ID = settingId ?? 0,
                p_SETTING_TYPE = settingType,
                p_KEYWORD = keyword,
                p_INCLUDE_DELETED = includeDeleted ? 1 : 0
            });
        }
    }
}

using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public class SysDecimalSettingRepository : ISysDecimalSettingRepository
    {
        private const string GetSettingsQuery = "CALL sys_decimal_setting_get(@p_COMPANY_CD, @p_SETTING_TYPE)";

        private const string GetCompanySettingQuery = "CALL sys_decimal_setting_get(@p_COMPANY_CD, @p_SETTING_TYPE)";

        private const string GetFieldSettingsQuery = "CALL sys_decimal_field_setting_get(@p_COMPANY_CD, @p_FIELD_NAME, @p_SETTING_TYPE)";

        private const string UpsertSettingQuery = "CALL sys_decimal_setting_set(@p_COMPANY_CD, @p_SETTING_TYPE, @p_DECIMAL_PLACES, @p_ROUNDING_MODE, @p_USE_THOUSAND_SEPARATOR, @p_IS_ACTIVE, @p_NOTE, @p_USER_ID)";

        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;

        public SysDecimalSettingRepository(DapperExecutor db, IActivityLogService activityLogService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
        }

        public async Task<IEnumerable<SysDecimalSetting>> GetSettingsAsync(string companyCd, string? settingType = null)
        {
            return await _db.QueryAsync<SysDecimalSetting>(
                Net_DB.Net_DB_Manager,
                GetSettingsQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_SETTING_TYPE = Common.NormalizeNullableText(settingType)
                });
        }

        public async Task<IEnumerable<SysDecimalFieldSetting>> GetFieldSettingsAsync(string companyCd, string? fieldName = null, string? settingType = null)
        {
            return await _db.QueryAsync<SysDecimalFieldSetting>(
                Net_DB.Net_DB_Manager,
                GetFieldSettingsQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_FIELD_NAME = Common.NormalizeNullableText(fieldName)?.ToUpperInvariant(),
                    p_SETTING_TYPE = Common.NormalizeNullableText(settingType)?.ToUpperInvariant()
                });
        }

        public async Task<SysDecimalSetting?> GetSettingAsync(string companyCd, string settingType)
        {
            return (await _db.QueryAsync<SysDecimalSetting>(
                Net_DB.Net_DB_Manager,
                GetCompanySettingQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_SETTING_TYPE = settingType
                })).FirstOrDefault();
        }

        public async Task<SysDecimalSetting?> UpsertSettingAsync(string companyCd, string settingType, SysDecimalSettingUpdateRequest request, string userId)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var existing = await GetSettingAsync(companyCd, settingType);
            var oldData = existing == null ? string.Empty : JsonSerializer.Serialize(existing);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                var saved = (await session.QueryAsync<SysDecimalSetting>(
                    UpsertSettingQuery,
                    new
                    {
                        p_COMPANY_CD = companyCd,
                        p_SETTING_TYPE = settingType,
                        p_DECIMAL_PLACES = request.DECIMAL_PLACES,
                        p_ROUNDING_MODE = request.ROUNDING_MODE,
                        p_USE_THOUSAND_SEPARATOR = request.USE_THOUSAND_SEPARATOR,
                        p_IS_ACTIVE = request.IS_ACTIVE,
                        p_NOTE = request.NOTE ?? string.Empty,
                        p_USER_ID = userId
                    })).FirstOrDefault();

                if (saved == null)
                {
                    session.Rollback();
                    return null;
                }

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    companyCd,
                    existing == null ? "INSERT" : "UPDATE",
                    "SysDecimalSetting",
                    "sys_decimal_setting",
                    settingType,
                    oldData,
                    JsonSerializer.Serialize(saved),
                    $"Upsert sys_decimal_setting {settingType}");

                session.Commit();
                return saved;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }
    }
}

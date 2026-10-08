using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services
{
    public class SysDecimalSettingService : ISysDecimalSettingService
    {
        private const string CacheScope = "sys-decimal-setting";

        private readonly ISysDecimalSettingRepository _repository;
        private readonly IMasterDataCacheService _cache;
        private readonly ILogger<SysDecimalSettingService> _logger;

        public SysDecimalSettingService(
            ISysDecimalSettingRepository repository,
            IMasterDataCacheService cache,
            ILogger<SysDecimalSettingService> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IEnumerable<SysDecimalSettingDto>> GetSettingsAsync(string companyCd, string? settingType = null)
        {
            var resolvedType = string.IsNullOrWhiteSpace(settingType) ? null : NormalizeSettingType(settingType);
            var cacheKey = $"settings|settingType={resolvedType ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetSettingsAsync(companyCd, resolvedType)).Select(MapToDto).ToList());
        }

        public async Task<IEnumerable<SysDecimalFieldSettingDto>> GetFieldSettingsAsync(string companyCd, string? fieldName = null, string? settingType = null)
        {
            var resolvedFieldName = string.IsNullOrWhiteSpace(fieldName) ? null : Common.NormalizeRequiredText(fieldName).ToUpperInvariant();
            var resolvedType = string.IsNullOrWhiteSpace(settingType) ? null : NormalizeSettingType(settingType);
            var cacheKey = $"fields|fieldName={resolvedFieldName ?? string.Empty}|settingType={resolvedType ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetFieldSettingsAsync(companyCd, resolvedFieldName, resolvedType)).Select(MapToFieldDto).ToList());
        }

        public Task<SysDecimalSetting?> GetSettingAsync(string companyCd, string settingType)
        {
            return _repository.GetSettingAsync(companyCd, settingType);
        }

        public async Task<SysDecimalSettingDto?> UpsertSettingAsync(string companyCd, string settingType, SysDecimalSettingUpdateRequest request, string userId)
        {
            var normalizedRequest = BuildUpdateRequest(request);
            var item = await _repository.UpsertSettingAsync(companyCd, settingType, normalizedRequest, userId);
            if (item == null) return null;

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("SysDecimalSetting upserted: settingType={SettingType}, company={CompanyCd}", settingType, companyCd);
            return MapToDto(item);
        }

        private static string NormalizeSettingType(string value)
        {
            var normalized = Common.NormalizeRequiredText(value);
            if (string.IsNullOrWhiteSpace(normalized))
                throw new ArgumentException("SETTING_TYPE is required");
            if (normalized.Length > 30)
                throw new ArgumentException("SETTING_TYPE length must be less than or equal to 30");
            return normalized.ToUpperInvariant();
        }

        private static SysDecimalSettingUpdateRequest BuildUpdateRequest(SysDecimalSettingUpdateRequest request)
        {
            if (request.DECIMAL_PLACES < 0)
                throw new ArgumentException("DECIMAL_PLACES must be greater than or equal to 0");

            return new SysDecimalSettingUpdateRequest
            {
                COMPANY_CD = request.COMPANY_CD,
                DECIMAL_PLACES = request.DECIMAL_PLACES,
                ROUNDING_MODE = string.IsNullOrWhiteSpace(request.ROUNDING_MODE)
                    ? "ROUND"
                    : Common.NormalizeRequiredText(request.ROUNDING_MODE).ToUpperInvariant(),
                USE_THOUSAND_SEPARATOR = Common.NormalizeFlagString(request.USE_THOUSAND_SEPARATOR, "1"),
                IS_ACTIVE = Common.NormalizeFlagString(request.IS_ACTIVE, "1"),
                NOTE = Common.NormalizeNullableText(request.NOTE) ?? string.Empty
            };
        }

        private static SysDecimalSettingDto MapToDto(SysDecimalSetting item)
        {
            return new SysDecimalSettingDto
            {
                ID = item.ID,
                COMPANY_CD = item.COMPANY_CD,
                SETTING_TYPE = item.SETTING_TYPE,
                DECIMAL_PLACES = item.DECIMAL_PLACES,
                ROUNDING_MODE = item.ROUNDING_MODE,
                USE_THOUSAND_SEPARATOR = item.USE_THOUSAND_SEPARATOR,
                IS_ACTIVE = item.IS_ACTIVE,
                NOTE = item.NOTE,
                APPLY_SCOPE = item.APPLY_SCOPE
            };
        }

        private static SysDecimalFieldSettingDto MapToFieldDto(SysDecimalFieldSetting item)
        {
            var settingDto = MapToDto(item);
            return new SysDecimalFieldSettingDto
            {
                ID = settingDto.ID,
                COMPANY_CD = settingDto.COMPANY_CD,
                FIELD_NAME = item.FIELD_NAME,
                LABEL_TEXT = item.LABEL_TEXT,
                CAPTION = item.CAPTION,
                SETTING_TYPE = settingDto.SETTING_TYPE,
                DECIMAL_PLACES = settingDto.DECIMAL_PLACES,
                ROUNDING_MODE = settingDto.ROUNDING_MODE,
                USE_THOUSAND_SEPARATOR = settingDto.USE_THOUSAND_SEPARATOR,
                IS_ACTIVE = settingDto.IS_ACTIVE,
                NOTE = settingDto.NOTE,
                APPLY_SCOPE = settingDto.APPLY_SCOPE
            };
        }
    }
}

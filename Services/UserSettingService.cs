using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace API_AMNOTE_WEB.Services
{
    public class UserSettingService : IUserSettingService
    {
        public const string SourceUser = "USER";
        public const string SourceCompany = "COMPANY";
        public const string SourceSystem = "SYSTEM";
        public const string SourceDefault = "DEFAULT";

        private const string AllSettingsCacheSuffix = "__all__";

        private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(3);
        private static readonly ConcurrentDictionary<string, CacheKeyInfo> CacheKeys = new(StringComparer.OrdinalIgnoreCase);

        private readonly IUserSettingRepository _repository;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<UserSettingService> _logger;

        public UserSettingService(
            IUserSettingRepository repository,
            IMemoryCache memoryCache,
            ILogger<UserSettingService> logger)
        {
            _repository = repository;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<UserSettingResolvedDto?> GetSettingAsync(string companyCd, string userId, string keyName, string? defaultValue = null)
        {
            var normalizedCompanyCd = NormalizeScopeValue(companyCd);
            var normalizedUserId = NormalizeScopeValue(userId);
            var normalizedKeyName = NormalizeRequiredKeyName(keyName);

            var cacheKey = BuildCacheKey(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            CacheKeys[cacheKey] = new CacheKeyInfo(normalizedCompanyCd, normalizedUserId, normalizedKeyName);

            return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheAbsoluteExpiration;
                entry.SlidingExpiration = CacheSlidingExpiration;
                entry.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
                {
                    if (evictedKey is string keyText)
                    {
                        CacheKeys.TryRemove(keyText, out _);
                    }
                });

                var candidates = await _repository.GetCandidatesAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
                return ResolveSetting(normalizedCompanyCd, normalizedUserId, normalizedKeyName, candidates, defaultValue);
            });
        }

        public async Task<IReadOnlyList<UserSettingResolvedDto>> GetSettingsAsync(string companyCd, string userId)
        {
            var normalizedCompanyCd = NormalizeScopeValue(companyCd);
            var normalizedUserId = NormalizeScopeValue(userId);

            var cacheKey = BuildCacheKey(normalizedCompanyCd, normalizedUserId, AllSettingsCacheSuffix);
            CacheKeys[cacheKey] = new CacheKeyInfo(normalizedCompanyCd, normalizedUserId, AllSettingsCacheSuffix);

            return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheAbsoluteExpiration;
                entry.SlidingExpiration = CacheSlidingExpiration;
                entry.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
                {
                    if (evictedKey is string keyText)
                    {
                        CacheKeys.TryRemove(keyText, out _);
                    }
                });

                var candidates = await _repository.GetCandidatesAsync(normalizedCompanyCd, normalizedUserId);
                return ResolveSettings(normalizedCompanyCd, normalizedUserId, candidates, null);
            }) ?? Array.Empty<UserSettingResolvedDto>();
        }

        public async Task<IReadOnlyList<UserSettingResolvedDto>> GetSettingsAsync(string companyCd, string userId, IEnumerable<string> keyNames)
        {
            var normalizedKeys = NormalizeKeyNames(keyNames);
            if (normalizedKeys.Count == 0)
            {
                return Array.Empty<UserSettingResolvedDto>();
            }

            var results = new List<UserSettingResolvedDto>(normalizedKeys.Count);
            foreach (var keyName in normalizedKeys)
            {
                var item = await GetSettingAsync(companyCd, userId, keyName);
                if (item != null)
                {
                    results.Add(item);
                }
            }

            return results;
        }

        public async Task<string?> GetStringAsync(string companyCd, string userId, string keyName, string? defaultValue = null)
        {
            var setting = await GetSettingAsync(companyCd, userId, keyName, defaultValue);
            if (setting == null || string.IsNullOrWhiteSpace(setting.VALUE))
            {
                return defaultValue;
            }

            return setting.VALUE;
        }

        public async Task<bool> GetBoolAsync(string companyCd, string userId, string keyName, bool defaultValue = false)
        {
            var value = await GetStringAsync(companyCd, userId, keyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Trim().ToUpperInvariant() switch
            {
                "1" => true,
                "Y" => true,
                "YES" => true,
                "TRUE" => true,
                "T" => true,
                "0" => false,
                "N" => false,
                "NO" => false,
                "FALSE" => false,
                "F" => false,
                _ => defaultValue
            };
        }

        public async Task<int?> GetIntAsync(string companyCd, string userId, string keyName, int? defaultValue = null)
        {
            var value = await GetStringAsync(companyCd, userId, keyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return int.TryParse(value.Trim(), out var parsed) ? parsed : defaultValue;
        }

        public async Task<UserSettingResolvedDto> SaveAsync(string companyCd, string userId, UserSettingSaveRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedCompanyCd = NormalizeScopeValue(companyCd);
            var normalizedUserId = NormalizeScopeValue(userId);
            var normalizedKeyName = NormalizeRequiredKeyName(request.KEY_NAME);
            var normalizedValue = NormalizeValue(request.VALUE);
            var normalizedNote = NormalizeNote(request.NOTE);

            await _repository.UpsertAsync(
                normalizedCompanyCd,
                normalizedUserId,
                normalizedKeyName,
                normalizedValue,
                normalizedNote);

            await ClearCacheAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            await ClearCacheAsync(normalizedCompanyCd, normalizedUserId, AllSettingsCacheSuffix);

            var saved = await _repository.GetExactAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            return MapResolved(saved ?? new UserSettingInfo
            {
                COMPANY_CD = normalizedCompanyCd,
                USER_ID = normalizedUserId,
                KEY_NAME = normalizedKeyName,
                VALUE = normalizedValue,
                NOTE = normalizedNote
            }, SourceUser);
        }

        public async Task<IReadOnlyList<UserSettingResolvedDto>> SaveBulkAsync(
            string companyCd,
            string userId,
            IEnumerable<UserSettingSaveRequest> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var results = new List<UserSettingResolvedDto>();
            foreach (var item in items)
            {
                results.Add(await SaveAsync(companyCd, userId, item));
            }

            return results;
        }

        public async Task<bool> DeleteAsync(string companyCd, string userId, string keyName)
        {
            var normalizedCompanyCd = NormalizeScopeValue(companyCd);
            var normalizedUserId = NormalizeScopeValue(userId);
            var normalizedKeyName = NormalizeRequiredKeyName(keyName);

            var deleted = await _repository.DeleteAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            if (deleted <= 0)
            {
                return false;
            }

            await ClearCacheAsync(normalizedCompanyCd, normalizedUserId, normalizedKeyName);
            await ClearCacheAsync(normalizedCompanyCd, normalizedUserId, AllSettingsCacheSuffix);
            return true;
        }

        public Task<int> ClearCacheAsync(string? companyCd = null, string? userId = null, string? keyName = null)
        {
            var normalizedCompanyCd = NormalizeKeyPart(companyCd);
            var normalizedUserId = NormalizeKeyPart(userId);
            var normalizedKeyName = NormalizeKeyPart(keyName);
            var removed = 0;

            foreach (var item in CacheKeys.ToArray())
            {
                if (!IsMatch(item.Value, normalizedCompanyCd, normalizedUserId, normalizedKeyName))
                {
                    continue;
                }

                _memoryCache.Remove(item.Key);
                CacheKeys.TryRemove(item.Key, out _);
                removed++;
            }

            if (removed > 0)
            {
                _logger.LogInformation(
                    "Cleared {Count} user_setting cache item(s). Company: {CompanyCd}, User: {UserId}, Key: {KeyName}",
                    removed,
                    companyCd,
                    userId,
                    keyName);
            }

            return Task.FromResult(removed);
        }

        internal static UserSettingResolvedDto ResolveSetting(
            string companyCd,
            string userId,
            string keyName,
            IEnumerable<UserSettingInfo> candidates,
            string? defaultValue)
        {
            var forKey = candidates
                .Where(item => item.KEY_NAME.Equals(keyName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var userSetting = forKey.FirstOrDefault(item =>
                item.COMPANY_CD.Equals(companyCd, StringComparison.OrdinalIgnoreCase) &&
                item.USER_ID.Equals(userId, StringComparison.OrdinalIgnoreCase));
            if (userSetting != null)
            {
                return MapResolved(userSetting, SourceUser);
            }

            var companySetting = forKey.FirstOrDefault(item =>
                item.COMPANY_CD.Equals(companyCd, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(item.USER_ID));
            if (companySetting != null)
            {
                return MapResolved(companySetting, SourceCompany);
            }

            var systemSetting = forKey.FirstOrDefault(item =>
                string.IsNullOrEmpty(item.COMPANY_CD) &&
                string.IsNullOrEmpty(item.USER_ID));
            if (systemSetting != null)
            {
                return MapResolved(systemSetting, SourceSystem);
            }

            return new UserSettingResolvedDto
            {
                KEY_NAME = keyName,
                VALUE = defaultValue ?? string.Empty,
                NOTE = string.Empty,
                SOURCE = SourceDefault
            };
        }

        private static IReadOnlyList<UserSettingResolvedDto> ResolveSettings(
            string companyCd,
            string userId,
            IEnumerable<UserSettingInfo> candidates,
            IEnumerable<string>? keyNames)
        {
            var candidateList = candidates.ToList();
            var keys = keyNames?.Select(key => NormalizeRequiredKeyName(key)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? candidateList.Select(item => item.KEY_NAME).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            return keys
                .Select(keyName => ResolveSetting(companyCd, userId, keyName, candidateList, null))
                .OrderBy(item => item.KEY_NAME, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static UserSettingResolvedDto MapResolved(UserSettingInfo item, string source)
        {
            return new UserSettingResolvedDto
            {
                KEY_NAME = item.KEY_NAME,
                VALUE = item.VALUE ?? string.Empty,
                NOTE = item.NOTE ?? string.Empty,
                SOURCE = source,
            };
        }

        private static List<string> NormalizeKeyNames(IEnumerable<string> keyNames)
        {
            return keyNames
                .Select(keyName => NormalizeRequiredKeyName(keyName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeScopeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeRequiredKeyName(string? keyName)
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

        private static string NormalizeKeyPart(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string BuildCacheKey(string companyCd, string userId, string keyName)
        {
            return $"user-setting:{companyCd}:{userId}:{keyName}";
        }

        private static bool IsMatch(CacheKeyInfo cacheKey, string companyCd, string userId, string keyName)
        {
            if (!string.IsNullOrWhiteSpace(companyCd) &&
                !cacheKey.CompanyCd.Equals(companyCd, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(userId) &&
                !cacheKey.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(keyName) &&
                !cacheKey.KeyName.Equals(keyName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private sealed record CacheKeyInfo(string CompanyCd, string UserId, string KeyName);
    }
}

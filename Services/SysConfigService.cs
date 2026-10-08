using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace API_AMNOTE_WEB.Services
{
    public class SysConfigService : ISysConfigService
    {
        private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(3);
        private static readonly ConcurrentDictionary<string, CacheKeyInfo> CacheKeys = new(StringComparer.OrdinalIgnoreCase);

        private readonly ISysConfigRepository _repository;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<SysConfigService> _logger;

        public SysConfigService(ISysConfigRepository repository, IMemoryCache memoryCache, ILogger<SysConfigService> logger)
        {
            _repository = repository;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<bool> GetBoolAsync(string companyCd, string group, string key, bool defaultValue = false)
        {
            var value = await GetStringAsync(companyCd, group, key);
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

        public async Task<string?> GetStringAsync(string companyCd, string group, string key, string? defaultValue = null)
        {
            var config = await GetCachedConfigAsync(companyCd, group, key);
            return string.IsNullOrWhiteSpace(config?.CONFIG_VALUE) ? defaultValue : config.CONFIG_VALUE;
        }

        public Task<int> ClearCacheAsync(string? companyCd = null, string? group = null, string? key = null)
        {
            var normalizedCompanyCd = NormalizeKeyPart(companyCd);
            var normalizedGroup = NormalizeKeyPart(group);
            var normalizedKey = NormalizeKeyPart(key);
            var removed = 0;

            foreach (var item in CacheKeys.ToArray())
            {
                if (!IsMatch(item.Value, normalizedCompanyCd, normalizedGroup, normalizedKey))
                {
                    continue;
                }

                _memoryCache.Remove(item.Key);
                CacheKeys.TryRemove(item.Key, out _);
                removed++;
            }

            if (removed > 0)
            {
                _logger.LogInformation("Cleared {Count} sys_config cache item(s). Company: {CompanyCd}, Group: {Group}, Key: {Key}", removed, companyCd, group, key);
            }

            return Task.FromResult(removed);
        }

        private async Task<SysConfig?> GetCachedConfigAsync(string companyCd, string group, string key)
        {
            var normalizedCompanyCd = NormalizeKeyPart(companyCd);
            var normalizedGroup = NormalizeKeyPart(group);
            var normalizedKey = NormalizeKeyPart(key);

            if (string.IsNullOrWhiteSpace(normalizedCompanyCd) ||
                string.IsNullOrWhiteSpace(normalizedGroup) ||
                string.IsNullOrWhiteSpace(normalizedKey))
            {
                _logger.LogWarning("SysConfig lookup skipped because company/group/key is empty. Company: {CompanyCd}, Group: {Group}, Key: {Key}", companyCd, group, key);
                return null;
            }

            var cacheKey = BuildCacheKey(normalizedCompanyCd, normalizedGroup, normalizedKey);
            CacheKeys[cacheKey] = new CacheKeyInfo(normalizedCompanyCd, normalizedGroup, normalizedKey);

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

                return await _repository.GetSysConfigAsync(normalizedCompanyCd, normalizedGroup, normalizedKey, true);
            });
        }

        private static string NormalizeKeyPart(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string BuildCacheKey(string companyCd, string group, string key)
        {
            return $"sys-config:{companyCd}:{group}:{key}";
        }

        private static bool IsMatch(CacheKeyInfo cacheKey, string companyCd, string group, string key)
        {
            if (!string.IsNullOrWhiteSpace(companyCd) && !cacheKey.CompanyCd.Equals(companyCd, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(group) && !cacheKey.Group.Equals(group, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(key) && !cacheKey.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private sealed record CacheKeyInfo(string CompanyCd, string Group, string Key);
    }
}

using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace API_AMNOTE_WEB.Services
{
    public class SysConfigService : ISysConfigService
    {
        private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(3);
        private static readonly ConcurrentDictionary<string, CacheKeyInfo> CacheKeys = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Lazy<Task<SysConfigSnapshot>>> InflightLoads = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, long> CacheVersions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, object> CacheGates = new(StringComparer.OrdinalIgnoreCase);

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
            var normalizedGroup = NormalizeKeyPart(group);
            var normalizedKey = NormalizeKeyPart(key);
            if (normalizedGroup.Length == 0 || normalizedKey.Length == 0)
            {
                _logger.LogWarning("SysConfig lookup skipped because group/key is empty. Company: {CompanyCd}, Group: {Group}, Key: {Key}", companyCd, group, key);
                return defaultValue;
            }

            var snapshot = await GetCachedConfigsAsync(companyCd);
            return snapshot.ActiveByKey.TryGetValue(BuildLookupKey(normalizedGroup, normalizedKey), out var config)
                && !string.IsNullOrWhiteSpace(config.CONFIG_VALUE)
                    ? config.CONFIG_VALUE
                    : defaultValue;
        }

        public async Task<IReadOnlyList<SysConfig>> GetSysConfigsAsync(
            string companyCd,
            string? configGroup = null,
            string? configKey = null,
            bool activeOnly = true)
        {
            var snapshot = await GetCachedConfigsAsync(companyCd);
            var normalizedCompanyCd = NormalizeCompanyCode(companyCd);
            var normalizedGroup = NormalizeKeyPart(configGroup);
            var normalizedKey = NormalizeKeyPart(configKey);

            var rows = activeOnly
                ? snapshot.Rows
                    .Where(IsActive)
                    .GroupBy(item => BuildLookupKey(item.CONFIG_GROUP, item.CONFIG_KEY), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                : snapshot.Rows;

            return rows
                // Keep the existing list endpoint scoped to rows owned by the requested company.
                .Where(item => string.Equals(item.COMPANY_CD, normalizedCompanyCd, StringComparison.OrdinalIgnoreCase))
                .Where(item => normalizedGroup.Length == 0 || string.Equals(item.CONFIG_GROUP, normalizedGroup, StringComparison.OrdinalIgnoreCase))
                .Where(item => normalizedKey.Length == 0 || string.Equals(item.CONFIG_KEY, normalizedKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.CONFIG_GROUP, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.CONFIG_KEY, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<SysConfig?> GetSysConfigAsync(string companyCd, string configGroup, string configKey, bool activeOnly = true)
        {
            var normalizedGroup = NormalizeKeyPart(configGroup);
            var normalizedKey = NormalizeKeyPart(configKey);
            if (normalizedGroup.Length == 0 || normalizedKey.Length == 0)
            {
                return null;
            }

            var snapshot = await GetCachedConfigsAsync(companyCd);
            if (activeOnly)
            {
                return snapshot.ActiveByKey.TryGetValue(BuildLookupKey(normalizedGroup, normalizedKey), out var activeConfig)
                    ? activeConfig
                    : null;
            }

            var normalizedCompanyCd = NormalizeCompanyCode(companyCd);
            return snapshot.Rows.FirstOrDefault(item =>
                string.Equals(item.COMPANY_CD, normalizedCompanyCd, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.CONFIG_GROUP, normalizedGroup, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.CONFIG_KEY, normalizedKey, StringComparison.OrdinalIgnoreCase));
        }

        public Task<int> ClearCacheAsync(string? companyCd = null, string? group = null, string? key = null)
        {
            var normalizedCompanyCd = NormalizeCompanyCode(companyCd);
            var removed = 0;

            // Snapshots are company-scoped, so a group/key clear evicts the whole matching company snapshot.
            foreach (var item in CacheKeys.ToArray())
            {
                if (normalizedCompanyCd.Length > 0 && !item.Value.CompanyCd.Equals(normalizedCompanyCd, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var gate = CacheGates.GetOrAdd(item.Key, _ => new object());
                lock (gate)
                {
                    CacheVersions.AddOrUpdate(item.Key, 1, (_, version) => version + 1);
                    _memoryCache.Remove(item.Key);
                    InflightLoads.TryRemove(item.Key, out _);
                    if (CacheKeys.TryRemove(item.Key, out _))
                    {
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation("Cleared {Count} sys_config company snapshot(s). Company: {CompanyCd}, Group: {Group}, Key: {Key}", removed, companyCd, group, key);
            }

            return Task.FromResult(removed);
        }

        private async Task<SysConfigSnapshot> GetCachedConfigsAsync(string? companyCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCode(companyCd);
            var cacheKey = BuildCacheKey(normalizedCompanyCd);
            var gate = CacheGates.GetOrAdd(cacheKey, _ => new object());
            var version = CacheVersions.GetOrAdd(cacheKey, 0);

            CacheKeys[cacheKey] = new CacheKeyInfo(normalizedCompanyCd);
            if (_memoryCache.TryGetValue<SysConfigSnapshot>(cacheKey, out var cachedSnapshot) && cachedSnapshot != null)
            {
                return cachedSnapshot;
            }

            var load = InflightLoads.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<SysConfigSnapshot>>(
                    () => LoadAndCacheAsync(cacheKey, normalizedCompanyCd, version, gate),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                return await load.Value;
            }
            finally
            {
                if (InflightLoads.TryGetValue(cacheKey, out var current) && ReferenceEquals(current, load))
                {
                    InflightLoads.TryRemove(cacheKey, out _);
                }
            }
        }

        private async Task<SysConfigSnapshot> LoadAndCacheAsync(
            string cacheKey,
            string companyCd,
            long version,
            object gate)
        {
            var rows = await _repository.GetRuntimeSysConfigsAsync(companyCd);
            var snapshotRows = rows.ToList();
            var activeConfigs = new Dictionary<string, SysConfig>(StringComparer.OrdinalIgnoreCase);

            // The procedure orders company rows before system defaults and newest IDs first.
            foreach (var row in snapshotRows)
            {
                if (IsActive(row))
                {
                    activeConfigs.TryAdd(BuildLookupKey(row.CONFIG_GROUP, row.CONFIG_KEY), row);
                }
            }

            var snapshot = new SysConfigSnapshot(snapshotRows, activeConfigs);
            lock (gate)
            {
                if (CacheVersions.GetOrAdd(cacheKey, 0) == version)
                {
                    _memoryCache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CacheAbsoluteExpiration,
                        SlidingExpiration = CacheSlidingExpiration
                    }.RegisterPostEvictionCallback((evictedKey, _, _, state) =>
                    {
                        if (evictedKey is string keyText
                            && state is long evictedVersion
                            && CacheVersions.TryGetValue(keyText, out var currentVersion)
                            && currentVersion == evictedVersion)
                        {
                            CacheKeys.TryRemove(keyText, out _);
                        }
                    }, version));
                }
            }

            return snapshot;
        }

        private static string NormalizeKeyPart(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static bool IsActive(SysConfig config)
            => string.Equals(config.IS_ACTIVE, "1", StringComparison.OrdinalIgnoreCase);

        private static string NormalizeCompanyCode(string? value)
            => NormalizeKeyPart(value).ToUpperInvariant();

        private static string BuildCacheKey(string companyCd)
            => $"sys-config:{companyCd}";

        private static string BuildLookupKey(string group, string key)
            => $"{group.Trim()}\0{key.Trim()}";

        private sealed record SysConfigSnapshot(
            IReadOnlyList<SysConfig> Rows,
            IReadOnlyDictionary<string, SysConfig> ActiveByKey);

        private sealed record CacheKeyInfo(string CompanyCd);
    }
}

using System.Collections.Concurrent;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace API_AMNOTE_WEB.Services
{
    public class MasterDataCacheService : IMasterDataCacheService
    {
        private const string CompanyPartition = "company";
        private const string GlobalPartition = "global";
        private const string GlobalOwner = "system";
        private static readonly TimeSpan CacheAbsoluteExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan CacheSlidingExpiration = TimeSpan.FromMinutes(3);

        private readonly ConcurrentDictionary<string, CacheKeyInfo> _cacheKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inflightLoads = new(StringComparer.OrdinalIgnoreCase);
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MasterDataCacheService> _logger;

        public MasterDataCacheService(IMemoryCache memoryCache, ILogger<MasterDataCacheService> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<T> GetOrCreateAsync<T>(string scope, string companyCd, string key, Func<Task<T>> factory)
            => await GetOrCreateInternalAsync(scope, CompanyPartition, companyCd, key, factory);

        public async Task<T> GetOrCreateGlobalAsync<T>(string scope, string key, Func<Task<T>> factory)
            => await GetOrCreateInternalAsync(scope, GlobalPartition, GlobalOwner, key, factory);

        public async Task<T> RefreshAsync<T>(string scope, string companyCd, string key, Func<Task<T>> factory)
            => await RefreshInternalAsync(scope, CompanyPartition, companyCd, key, factory);

        public async Task<T> RefreshGlobalAsync<T>(string scope, string key, Func<Task<T>> factory)
            => await RefreshInternalAsync(scope, GlobalPartition, GlobalOwner, key, factory);

        public Task<int> ClearAsync(string scope, string? companyCd = null)
            => ClearInternalAsync(scope, CompanyPartition, companyCd);

        public Task<int> ClearGlobalAsync(string scope)
            => ClearInternalAsync(scope, GlobalPartition, GlobalOwner);

        public Task<int> ClearAllAsync()
        {
            var removed = 0;

            foreach (var item in _cacheKeys.ToArray())
            {
                _memoryCache.Remove(item.Key);
                if (_cacheKeys.TryRemove(item.Key, out _))
                {
                    removed++;
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation("Cleared all {Count} master data cache item(s).", removed);
            }

            return Task.FromResult(removed);
        }

        private async Task<T> GetOrCreateInternalAsync<T>(
            string scope,
            string partition,
            string owner,
            string key,
            Func<Task<T>> factory)
        {
            var normalizedScope = NormalizeKeyPart(scope);
            var normalizedPartition = NormalizeKeyPart(partition);
            var normalizedOwner = NormalizeKeyPart(owner);
            var normalizedKey = NormalizeKeyPart(key);
            var cacheKey = BuildCacheKey(normalizedScope, normalizedPartition, normalizedOwner, normalizedKey);

            _cacheKeys[cacheKey] = new CacheKeyInfo(normalizedScope, normalizedPartition, normalizedOwner);
            if (_memoryCache.TryGetValue(cacheKey, out T? cachedValue))
            {
                return cachedValue!;
            }

            var load = _inflightLoads.GetOrAdd(
                cacheKey,
                _ => new Lazy<Task<object?>>(
                    async () =>
                    {
                        if (_memoryCache.TryGetValue(cacheKey, out T? valueLoadedByAnotherRequest))
                        {
                            return valueLoadedByAnotherRequest;
                        }

                        var value = await factory();
                        _memoryCache.Set(cacheKey, value, BuildEntryOptions());
                        return value;
                    },
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                var value = await load.Value;
                return value is null ? default! : (T)value;
            }
            finally
            {
                if (_inflightLoads.TryGetValue(cacheKey, out var current) && ReferenceEquals(current, load))
                {
                    _inflightLoads.TryRemove(cacheKey, out _);
                }
            }
        }

        private async Task<T> RefreshInternalAsync<T>(
            string scope,
            string partition,
            string owner,
            string key,
            Func<Task<T>> factory)
        {
            var normalizedScope = NormalizeKeyPart(scope);
            var normalizedPartition = NormalizeKeyPart(partition);
            var normalizedOwner = NormalizeKeyPart(owner);
            var normalizedKey = NormalizeKeyPart(key);
            var cacheKey = BuildCacheKey(normalizedScope, normalizedPartition, normalizedOwner, normalizedKey);

            _memoryCache.Remove(cacheKey);
            _cacheKeys.TryRemove(cacheKey, out _);

            var value = await factory();
            _cacheKeys[cacheKey] = new CacheKeyInfo(normalizedScope, normalizedPartition, normalizedOwner);
            _memoryCache.Set(cacheKey, value, BuildEntryOptions());

            return value;
        }

        private Task<int> ClearInternalAsync(string scope, string partition, string? owner = null)
        {
            var normalizedScope = NormalizeKeyPart(scope);
            var normalizedPartition = NormalizeKeyPart(partition);
            var normalizedOwner = NormalizeKeyPart(owner);
            var removed = 0;

            foreach (var item in _cacheKeys.ToArray())
            {
                if (!item.Value.Scope.Equals(normalizedScope, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!item.Value.Partition.Equals(normalizedPartition, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(normalizedOwner) &&
                    !item.Value.Owner.Equals(normalizedOwner, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _memoryCache.Remove(item.Key);
                _cacheKeys.TryRemove(item.Key, out _);
                removed++;
            }

            if (removed > 0)
            {
                _logger.LogInformation("Cleared {Count} master data cache item(s). Scope: {Scope}, Partition: {Partition}, Owner: {Owner}", removed, scope, normalizedPartition, normalizedOwner);
            }

            return Task.FromResult(removed);
        }

        private static string NormalizeKeyPart(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string BuildCacheKey(string scope, string partition, string owner, string key)
        {
            return $"master-data:{partition}:{scope}:{owner}:{key}";
        }

        private MemoryCacheEntryOptions BuildEntryOptions()
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheAbsoluteExpiration,
                SlidingExpiration = CacheSlidingExpiration
            };

            options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                if (evictedKey is string keyText)
                {
                    _cacheKeys.TryRemove(keyText, out _);
                }
            });

            return options;
        }

        private sealed record CacheKeyInfo(string Scope, string Partition, string Owner);
    }
}
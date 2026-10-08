using System.Linq;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class LanguageRepository : ILanguageRepository
    {
        private const string CacheScope = "system-language";
        private const string CacheKey = "messages";

        private static readonly object LoadSync = new();
        private static Task<IReadOnlyList<t_message_info>>? Loading;

        private readonly DapperExecutor _db;
        private readonly IMasterDataCacheService _cacheService;

        public LanguageRepository(DapperExecutor db, IMasterDataCacheService cacheService)
        {
            _db = db;
            _cacheService = cacheService;
        }

        public async Task<IEnumerable<t_message_info>> getLanguageInfos(IEnumerable<string> keys)
        {
            var keySet = NormalizeKeys(keys);
            if (keySet.Count == 0)
            {
                return new List<t_message_info>();
            }

            var messages = await GetCachedMessagesAsync();
            return messages
                .Where(item => MessageKeyMatches(keySet, item.KEY))
                .ToList();
        }

        public async Task<IEnumerable<t_message_info>> getLanguageInfoAll(IEnumerable<string> keys)
        {
            var messages = await GetCachedMessagesAsync();
            var keySet = NormalizeKeys(keys);

            if (keySet.Count == 0)
            {
                return messages;
            }

            return messages
                .Where(item => MessageKeyMatches(keySet, item.KEY))
                .ToList();
        }

        private Task<IReadOnlyList<t_message_info>> GetCachedMessagesAsync()
        {
            lock (LoadSync)
            {
                if (Loading is { IsCompleted: false })
                {
                    return Loading;
                }

                var load = LoadMessagesIntoCacheAsync();
                Loading = load;
                load.ContinueWith(
                    completed =>
                    {
                        lock (LoadSync)
                        {
                            if (ReferenceEquals(Loading, completed))
                            {
                                Loading = null;
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default);
                return load;
            }
        }

        private async Task<IReadOnlyList<t_message_info>> LoadMessagesIntoCacheAsync()
        {
            return await _cacheService.GetOrCreateGlobalAsync(
                CacheScope,
                CacheKey,
                async () =>
                {
                    var messages = await _db.QueryAsync<t_message_info>(Net_DB.Net_DB_Manager, "CALL getMessageInfo()");
                    return messages.ToList();
                });
        }

        private static HashSet<string> NormalizeKeys(IEnumerable<string> keys)
        {
            if (keys == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return keys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static bool MessageKeyMatches(HashSet<string> keySet, string? messageKey)
        {
            if (string.IsNullOrWhiteSpace(messageKey))
            {
                return false;
            }

            return keySet.Contains(messageKey.Trim());
        }
    }
}
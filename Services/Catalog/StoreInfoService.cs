using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class StoreInfoService : IStoreInfoService
    {
        private const string CacheScope = "store-info";
        private const string SequenceMenuCode = "MD_WAREHOUSE";
        private const string SequenceCodeField = "STORE_CD";

        private readonly IStoreInfoRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<StoreInfoService> _logger;

        public StoreInfoService(
            IStoreInfoRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<StoreInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<StoreInfo>> GetListAsync(string companyCd, int? storeId = null, string? storeCd = null)
        {
            var cacheKey = $"list|storeId={storeId?.ToString() ?? string.Empty}|storeCd={storeCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey, async () =>
            {
                var list = (await _repository.GetStoreInfoAsync(companyCd, storeId)).ToList();
                if (string.IsNullOrWhiteSpace(storeCd))
                    return list;

                return list
                    .Where(s => string.Equals(s.STORE_CD, storeCd, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            });
        }

        public async Task<StoreInfo?> GetByIdAsync(string companyCd, int storeId)
            => (await GetListAsync(companyCd, storeId)).FirstOrDefault();

        private async Task<StoreInfo?> ResolveExistingAsync(string companyCd, StoreInfoRequest request)
        {
            if (request.STORE_ID > 0)
                return await GetByIdAsync(companyCd, request.STORE_ID);

            var storeCd = Common.NormalizeNullableText(request.STORE_CD);
            if (!string.IsNullOrWhiteSpace(storeCd))
                return (await GetListAsync(companyCd, null, storeCd)).FirstOrDefault();

            return null;
        }

        public async Task<StoreInfo> CreateAsync(string companyCd, string userId, StoreInfoRequest request)
        {
            var storeName = Common.NormalizeRequiredText(request.STORE_NM_VIET);
            if (string.IsNullOrWhiteSpace(storeName))
                throw new ArgumentException("STORE_NM_VIET is required");

            var storeCd = Common.NormalizeNullableText(request.STORE_CD);
            var payload = new StoreInfoRequest
            {
                STORE_ID = 0,
                STORE_CD = storeCd ?? string.Empty,
                STORE_NM_VIET = storeName,
                STORE_NM_ENG = Common.NormalizeNullableText(request.STORE_NM_ENG) ?? string.Empty,
                STORE_NM_KOR = Common.NormalizeNullableText(request.STORE_NM_KOR) ?? string.Empty,
                STORE_NM_CHINA = Common.NormalizeNullableText(request.STORE_NM_CHINA) ?? string.Empty,
                STORE_KIND_ID = request.STORE_KIND_ID
            };

            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("STORE_CD already exists");

            var result = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (result <= 0)
                throw new InvalidOperationException("Create failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("StoreInfo created: storeCd={StoreCd}, company={Company}", payload.STORE_CD, companyCd);
            return (await GetListAsync(companyCd, null, payload.STORE_CD)).FirstOrDefault()
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<StoreInfo> UpdateAsync(string companyCd, string userId, int storeId, StoreInfoRequest request)
        {
            var existing = await GetByIdAsync(companyCd, storeId)
                ?? throw new KeyNotFoundException("Store not found");

            var nextStoreCd = request.STORE_CD == null ? existing.STORE_CD : Common.NormalizeRequiredText(request.STORE_CD);
            if (string.IsNullOrWhiteSpace(nextStoreCd))
                throw new ArgumentException("STORE_CD is required");

            var nextStoreName = Common.NormalizeNullableText(request.STORE_NM_VIET) ?? existing.STORE_NM_VIET;
            if (string.IsNullOrWhiteSpace(nextStoreName))
                throw new ArgumentException("STORE_NM_VIET is required");

            if (await CodeExistsAsync(companyCd, nextStoreCd, storeId))
                throw new InvalidOperationException("STORE_CD already exists");

            var payload = new StoreInfoRequest
            {
                STORE_ID = existing.STORE_ID,
                STORE_CD = nextStoreCd,
                STORE_NM_VIET = nextStoreName,
                STORE_NM_ENG = Common.NormalizeNullableText(request.STORE_NM_ENG) ?? existing.STORE_NM_ENG,
                STORE_NM_KOR = Common.NormalizeNullableText(request.STORE_NM_KOR) ?? existing.STORE_NM_KOR,
                STORE_NM_CHINA = Common.NormalizeNullableText(request.STORE_NM_CHINA) ?? existing.STORE_NM_CHINA,
                STORE_KIND_ID = request.STORE_KIND_ID > 0 ? request.STORE_KIND_ID : existing.STORE_KIND_ID
            };

            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0)
                throw new InvalidOperationException("Update failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("StoreInfo updated: storeId={StoreId}, company={Company}", storeId, companyCd);
            return await GetByIdAsync(companyCd, storeId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<int> storeIds)
        {
            if (storeIds == null || storeIds.Count == 0)
                throw new ArgumentException("storeIds is required");

            var ids = storeIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Store {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "store", existing.STORE_ID, existing.STORE_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.STORE_ID, existing.STORE_CD ?? string.Empty, existing);
            }

            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids.Select(id => (long)id),
                    (s, cd, id) => _repository.DeleteStoreInfoAsync(s, cd, (int)id, userId),
                    auditEntries,
                    "StoreInfo",
                    "store_info",
                    "Delete store_info"));

            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("StoreInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }

            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string storeCd, int? excludeId = null)
            => _repository.StoreCdExistsAsync(companyCd, storeCd, excludeId);

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<StoreInfoRequest> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
            {
                record.STORE_ID = 0;
                record.STORE_CD = Common.NormalizeRequiredText(record.STORE_CD);
                record.STORE_NM_VIET = Common.NormalizeRequiredText(record.STORE_NM_VIET);
                if (string.IsNullOrWhiteSpace(record.STORE_NM_VIET))
                    throw new ArgumentException("STORE_NM_VIET is required");
            }

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.STORE_CD),
                code => CodeExistsAsync(companyCd, code),
                "STORE_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "StoreInfo",
                "store_info",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);
            return inserted;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, StoreInfoRequest payload, StoreInfo? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);

            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.STORE_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.STORE_CD,
                    payload.STORE_CD,
                    "STORE_CD");

                var result = await _repository.SetStoreInfoAsync(session, companyCd, userId, payload);
                if (result > 0)
                {
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "StoreInfo",
                        "store_info",
                        payload.STORE_CD ?? existing?.STORE_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert store_info");
                }

                return result;
            });
        }
    }
}

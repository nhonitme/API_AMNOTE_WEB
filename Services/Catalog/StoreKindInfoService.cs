using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class StoreKindInfoService : IStoreKindInfoService
    {
        private const string CacheScope = "store-kind-info";
        private const string SequenceMenuCode = "MD_WAREHOUSE_TYPE";
        private const string SequenceCodeField = "STORE_KIND_CD";

        private readonly IStoreKindInfoRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<StoreKindInfoService> _logger;

        public StoreKindInfoService(
            IStoreKindInfoRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<StoreKindInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<StoreKindInfo>> GetListAsync(string companyCd, int? storeKindId = null, string? storeKindCd = null)
        {
            var cacheKey = $"list|storeKindId={storeKindId?.ToString() ?? string.Empty}|storeKindCd={storeKindCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey, async () =>
            {
                var list = (await _repository.GetStoreKindInfoAsync(companyCd, storeKindId)).ToList();
                if (string.IsNullOrWhiteSpace(storeKindCd))
                    return list;

                return list
                    .Where(s => string.Equals(s.STORE_KIND_CD, storeKindCd, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            });
        }

        public async Task<StoreKindInfo?> GetByIdAsync(string companyCd, int storeKindId)
            => (await GetListAsync(companyCd, storeKindId)).FirstOrDefault();

        private async Task<StoreKindInfo?> ResolveExistingAsync(string companyCd, StoreKindInfoRequest request)
        {
            if (request.STORE_KIND_ID > 0)
                return await GetByIdAsync(companyCd, request.STORE_KIND_ID);

            var storeKindCd = Common.NormalizeNullableText(request.STORE_KIND_CD);
            if (!string.IsNullOrWhiteSpace(storeKindCd))
                return (await GetListAsync(companyCd, null, storeKindCd)).FirstOrDefault();

            return null;
        }

        public async Task<StoreKindInfo> CreateAsync(string companyCd, string userId, StoreKindInfoRequest request)
        {
            var storeKindName = Common.NormalizeRequiredText(request.STORE_KIND_NM_VIET);
            if (string.IsNullOrWhiteSpace(storeKindName))
                throw new ArgumentException("STORE_KIND_NM_VIET is required");

            var storeKindCd = Common.NormalizeNullableText(request.STORE_KIND_CD);
            var payload = new StoreKindInfoRequest
            {
                STORE_KIND_ID = 0,
                STORE_KIND_CD = storeKindCd ?? string.Empty,
                STORE_KIND_NM_VIET = storeKindName,
                STORE_KIND_NM_ENG = Common.NormalizeNullableText(request.STORE_KIND_NM_ENG) ?? string.Empty,
                STORE_KIND_NM_KOR = Common.NormalizeNullableText(request.STORE_KIND_NM_KOR) ?? string.Empty,
                STORE_KIND_NM_CHINA = Common.NormalizeNullableText(request.STORE_KIND_NM_CHINA) ?? string.Empty
            };

            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("STORE_KIND_CD already exists");

            var result = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (result <= 0)
                throw new InvalidOperationException("Create failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("StoreKindInfo created: storeKindCd={StoreKindCd}, company={Company}", payload.STORE_KIND_CD, companyCd);
            return (await GetListAsync(companyCd, null, payload.STORE_KIND_CD)).FirstOrDefault()
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<StoreKindInfo> UpdateAsync(string companyCd, string userId, int storeKindId, StoreKindInfoRequest request)
        {
            var existing = await GetByIdAsync(companyCd, storeKindId)
                ?? throw new KeyNotFoundException("Store kind not found");

            var nextStoreKindCd = request.STORE_KIND_CD == null
                ? existing.STORE_KIND_CD
                : Common.NormalizeRequiredText(request.STORE_KIND_CD);
            if (string.IsNullOrWhiteSpace(nextStoreKindCd))
                throw new ArgumentException("STORE_KIND_CD is required");

            var nextStoreKindName = Common.NormalizeNullableText(request.STORE_KIND_NM_VIET) ?? existing.STORE_KIND_NM_VIET;
            if (string.IsNullOrWhiteSpace(nextStoreKindName))
                throw new ArgumentException("STORE_KIND_NM_VIET is required");

            if (await CodeExistsAsync(companyCd, nextStoreKindCd, storeKindId))
                throw new InvalidOperationException("STORE_KIND_CD already exists");

            var payload = new StoreKindInfoRequest
            {
                STORE_KIND_ID = existing.STORE_KIND_ID,
                STORE_KIND_CD = nextStoreKindCd,
                STORE_KIND_NM_VIET = nextStoreKindName,
                STORE_KIND_NM_ENG = Common.NormalizeNullableText(request.STORE_KIND_NM_ENG) ?? existing.STORE_KIND_NM_ENG,
                STORE_KIND_NM_KOR = Common.NormalizeNullableText(request.STORE_KIND_NM_KOR) ?? existing.STORE_KIND_NM_KOR,
                STORE_KIND_NM_CHINA = Common.NormalizeNullableText(request.STORE_KIND_NM_CHINA) ?? existing.STORE_KIND_NM_CHINA
            };

            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0)
                throw new InvalidOperationException("Update failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("StoreKindInfo updated: storeKindId={StoreKindId}, company={Company}", storeKindId, companyCd);
            return await GetByIdAsync(companyCd, storeKindId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<int> storeKindIds)
        {
            if (storeKindIds == null || storeKindIds.Count == 0)
                throw new ArgumentException("storeKindIds is required");

            var ids = storeKindIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Store kind {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "store_kind", existing.STORE_KIND_ID, existing.STORE_KIND_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.STORE_KIND_ID, existing.STORE_KIND_CD ?? string.Empty, existing);
            }

            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids.Select(id => (long)id),
                    (s, cd, id) => _repository.DeleteStoreKindInfoAsync(s, cd, (int)id, userId),
                    auditEntries,
                    "StoreKindInfo",
                    "store_kind_info",
                    "Delete store_kind_info"));

            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("StoreKindInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }

            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string storeKindCd, int? excludeId = null)
            => _repository.StoreKindCdExistsAsync(companyCd, storeKindCd, excludeId);

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<StoreKindInfoRequest> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
            {
                record.STORE_KIND_ID = 0;
                record.STORE_KIND_CD = Common.NormalizeRequiredText(record.STORE_KIND_CD);
                record.STORE_KIND_NM_VIET = Common.NormalizeRequiredText(record.STORE_KIND_NM_VIET);
                if (string.IsNullOrWhiteSpace(record.STORE_KIND_NM_VIET))
                    throw new ArgumentException("STORE_KIND_NM_VIET is required");
            }

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.STORE_KIND_CD),
                code => CodeExistsAsync(companyCd, code),
                "STORE_KIND_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "StoreKindInfo",
                "store_kind_info",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);
            return inserted;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, StoreKindInfoRequest payload, StoreKindInfo? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);

            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.STORE_KIND_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.STORE_KIND_CD,
                    payload.STORE_KIND_CD,
                    "STORE_KIND_CD");

                var result = await _repository.SetStoreKindInfoAsync(session, companyCd, userId, payload);
                if (result > 0)
                {
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "StoreKindInfo",
                        "store_kind_info",
                        payload.STORE_KIND_CD ?? existing?.STORE_KIND_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert store_kind_info");
                }

                return result;
            });
        }
    }
}

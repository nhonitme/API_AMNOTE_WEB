using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class ProductKindService : IProductKindService
    {
        private const string CacheScope = "product-kind";
        private const string SequenceMenuCode = "MD_PRODUCT_GROUP";
        private const string SequenceCodeField = "PRODUCT_KIND_CD";

        private readonly IProductKindRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<ProductKindService> _logger;

        public ProductKindService(
            IProductKindRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<ProductKindService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<ProductKind>> GetListAsync(string companyCd, int? productKindId = null, string? productKindCd = null)
        {
            var cacheKey = $"list|productKindId={productKindId?.ToString() ?? string.Empty}|productKindCd={productKindCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetProductKindAsync(companyCd, productKindId, productKindCd)).ToList());
        }

        public async Task<ProductKind?> GetByIdAsync(string companyCd, int productKindId)
            => (await GetListAsync(companyCd, productKindId)).FirstOrDefault();

        private async Task<ProductKind?> ResolveExistingAsync(string companyCd, ProductKind request)
        {
            if (request.PRODUCT_KIND_ID > 0)
                return await GetByIdAsync(companyCd, request.PRODUCT_KIND_ID);
            var productKindCd = Common.NormalizeNullableText(request.PRODUCT_KIND_CD);
            if (!string.IsNullOrWhiteSpace(productKindCd))
                return (await GetListAsync(companyCd, null, productKindCd)).FirstOrDefault();
            return null;
        }

        public async Task<ProductKind> CreateAsync(string companyCd, string userId, ProductKind request)
        {
            var name = Common.NormalizeNullableText(request.PRODUCTKIND_NM_VIET);
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("PRODUCTKIND_NM_VIET is required");
            var payload = new ProductKind
            {
                PRODUCT_KIND_ID = 0,
                COMPANY_CD = companyCd,
                PRODUCT_KIND_CD = Common.NormalizeNullableText(request.PRODUCT_KIND_CD) ?? string.Empty,
                PRODUCTKIND_NM_VIET = name,
                PRODUCTKIND_NM_ENG = Common.NormalizeNullableText(request.PRODUCTKIND_NM_ENG),
                PRODUCTKIND_NM_KOR = Common.NormalizeNullableText(request.PRODUCTKIND_NM_KOR),
                PRODUCTKIND_NM_CHINA = Common.NormalizeNullableText(request.PRODUCTKIND_NM_CHINA),
                REMARK = Common.NormalizeNullableText(request.REMARK),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0")
            };
            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("PRODUCT_KIND_CD already exists");
            var id = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (id <= 0) throw new InvalidOperationException("Create failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ProductKind created: id={Id}, company={Company}", id, companyCd);
            return await GetByIdAsync(companyCd, id)
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<ProductKind> UpdateAsync(string companyCd, string userId, int productKindId, ProductKind request)
        {
            var existing = await GetByIdAsync(companyCd, productKindId)
                ?? throw new KeyNotFoundException("Product kind not found");
            var nextCd = request.PRODUCT_KIND_CD == null ? existing.PRODUCT_KIND_CD : Common.NormalizeRequiredText(request.PRODUCT_KIND_CD);
            if (string.IsNullOrWhiteSpace(nextCd))
                throw new ArgumentException("PRODUCT_KIND_CD is required");
            var nextName = request.PRODUCTKIND_NM_VIET ?? existing.PRODUCTKIND_NM_VIET;
            if (string.IsNullOrWhiteSpace(Common.NormalizeRequiredText(nextName)))
                throw new ArgumentException("PRODUCTKIND_NM_VIET is required");
            if (await CodeExistsAsync(companyCd, nextCd, productKindId))
                throw new InvalidOperationException("PRODUCT_KIND_CD already exists");
            var payload = new ProductKind
            {
                PRODUCT_KIND_ID = existing.PRODUCT_KIND_ID,
                COMPANY_CD = companyCd,
                PRODUCT_KIND_CD = nextCd,
                PRODUCTKIND_NM_VIET = request.PRODUCTKIND_NM_VIET == null ? existing.PRODUCTKIND_NM_VIET : Common.NormalizeRequiredText(request.PRODUCTKIND_NM_VIET),
                PRODUCTKIND_NM_ENG = request.PRODUCTKIND_NM_ENG == null ? existing.PRODUCTKIND_NM_ENG : Common.NormalizeNullableText(request.PRODUCTKIND_NM_ENG),
                PRODUCTKIND_NM_KOR = request.PRODUCTKIND_NM_KOR == null ? existing.PRODUCTKIND_NM_KOR : Common.NormalizeNullableText(request.PRODUCTKIND_NM_KOR),
                PRODUCTKIND_NM_CHINA = request.PRODUCTKIND_NM_CHINA == null ? existing.PRODUCTKIND_NM_CHINA : Common.NormalizeNullableText(request.PRODUCTKIND_NM_CHINA),
                REMARK = request.REMARK == null ? existing.REMARK : Common.NormalizeNullableText(request.REMARK),
                ISDEL = Common.NormalizeFlagString(request.ISDEL ?? existing.ISDEL, "0")
            };
            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0) throw new InvalidOperationException("Update failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ProductKind updated: id={Id}, company={Company}", productKindId, companyCd);
            return await GetByIdAsync(companyCd, productKindId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<int> productKindIds)
        {
            if (productKindIds == null || productKindIds.Count == 0)
                throw new ArgumentException("productKindIds is required");
            var ids = productKindIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Product kind {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "product_kind", existing.PRODUCT_KIND_ID, existing.PRODUCT_KIND_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.PRODUCT_KIND_ID, existing.PRODUCT_KIND_CD ?? string.Empty, existing);
            }
            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids.Select(x => (long)x),
                    (s, cd, id) => _repository.DeleteProductKindAsync(s, cd, (int)id, userId),
                    auditEntries,
                    "ProductKind",
                    "product_kind",
                    "Delete product_kind"));
            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("ProductKind deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }
            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string productKindCd, int? excludeId = null, string? databaseName = null, string? lang = null)
            => _repository.ProductKindCdExistsAsync(companyCd, productKindCd, excludeId, databaseName);

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<ProductKind> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
            {
                record.PRODUCT_KIND_CD = Common.NormalizeRequiredText(record.PRODUCT_KIND_CD);
                record.PRODUCTKIND_NM_VIET = Common.NormalizeRequiredText(record.PRODUCTKIND_NM_VIET);
                if (string.IsNullOrWhiteSpace(record.PRODUCTKIND_NM_VIET))
                    throw new ArgumentException("PRODUCTKIND_NM_VIET is required");
            }

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.PRODUCT_KIND_CD),
                code => CodeExistsAsync(companyCd, code, null, databaseName),
                "PRODUCT_KIND_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "ProductKind",
                "product_kind",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);
            return inserted;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, ProductKind payload, ProductKind? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);
            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.PRODUCT_KIND_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.PRODUCT_KIND_CD,
                    payload.PRODUCT_KIND_CD,
                    "PRODUCT_KIND_CD");
                var id = await _repository.SetProductKindAsync(session, companyCd, userId, payload);
                if (id > 0)
                {
                    payload.PRODUCT_KIND_ID = id;
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "ProductKind",
                        "product_kind",
                        payload.PRODUCT_KIND_CD ?? existing?.PRODUCT_KIND_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert product_kind");
                }
                return id;
            });
        }
    }
}

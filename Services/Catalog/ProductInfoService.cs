using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class ProductInfoService : IProductInfoService
    {
        private const string CacheScope = "product-info";
        private const string SequenceMenuCode = "MD_INVENTORY";
        private const string SequenceCodeField = "PRODUCT_CD";

        private readonly IProductInfoRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IProductKindService _productKindService;
        private readonly IProductUnitService _productUnitService;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<ProductInfoService> _logger;

        public ProductInfoService(
            IProductInfoRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IProductKindService productKindService,
            IProductUnitService productUnitService,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<ProductInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _productKindService = productKindService;
            _productUnitService = productUnitService;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<ProductInfoDto>> GetListAsync(string companyCd, int? productId = null, string? productCd = null)
        {
            var cacheKey = $"list|productId={productId?.ToString() ?? string.Empty}|productCd={productCd ?? string.Empty}";
            var list = await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetProductInfoAsync(companyCd, productId)).ToList());
            if (string.IsNullOrWhiteSpace(productCd))
                return list;
            return list.Where(x => string.Equals(x.PRODUCT_CD, productCd, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<ProductInfoDto?> GetByIdAsync(string companyCd, int productId)
            => (await GetListAsync(companyCd, productId)).FirstOrDefault();

        private async Task<ProductInfoDto?> ResolveExistingAsync(string companyCd, ProductInfoDto request)
        {
            if (request.PRODUCT_ID > 0)
                return await GetByIdAsync(companyCd, request.PRODUCT_ID);
            var productCd = Common.NormalizeNullableText(request.PRODUCT_CD);
            if (!string.IsNullOrWhiteSpace(productCd))
                return (await GetListAsync(companyCd, null, productCd)).FirstOrDefault();
            return null;
        }

        public async Task<ProductInfoDto> CreateAsync(string companyCd, string userId, ProductInfo request)
        {
            var productName = Common.NormalizeRequiredText(request.PRODUCT_NM_VIET);
            if (string.IsNullOrWhiteSpace(productName))
                throw new ArgumentException("PRODUCT_NM_VIET is required");
            var unitId = NormalizeOptionalFk(request.UNIT_ID)
                ?? throw new ArgumentException("UNIT_ID is required");
            await ValidateProductReferencesAsync(companyCd, request.PRODUCT_KIND_ID, unitId, request.STORE_ID);
            var payload = new ProductInfoDto
            {
                PRODUCT_ID = 0,
                COMPANY_CD = companyCd,
                PRODUCT_CD = Common.NormalizeNullableText(request.PRODUCT_CD) ?? string.Empty,
                PRODUCT_NM_VIET = productName,
                PRODUCT_NM_ENG = Common.NormalizeNullableText(request.PRODUCT_NM_ENG),
                PRODUCT_NM_KOR = Common.NormalizeNullableText(request.PRODUCT_NM_KOR),
                PRODUCT_NM_CHINA = Common.NormalizeNullableText(request.PRODUCT_NM_CHINA),
                PRODUCT_KIND_ID = NormalizeOptionalFk(request.PRODUCT_KIND_ID),
                UNIT_ID = unitId,
                STORE_ID = NormalizeOptionalFk(request.STORE_ID),
                DIVISION = Common.NormalizeNullableText(request.DIVISION),
                SUMMARY = Common.NormalizeNullableText(request.SUMMARY),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0")
            };
            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("PRODUCT_CD already exists");
            var id = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (id <= 0) throw new InvalidOperationException("Create failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ProductInfo created: id={Id}, company={Company}", id, companyCd);
            return await GetByIdAsync(companyCd, id)
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<ProductInfoDto> UpdateAsync(string companyCd, string userId, int productId, ProductInfoDto request)
        {
            var existing = await GetByIdAsync(companyCd, productId)
                ?? throw new KeyNotFoundException("Product not found");
            var nextName = request.PRODUCT_NM_VIET ?? existing.PRODUCT_NM_VIET;
            if (string.IsNullOrWhiteSpace(Common.NormalizeRequiredText(nextName)))
                throw new ArgumentException("PRODUCT_NM_VIET is required");

            // Kind/Store optional (null/0 clears). UNIT_ID required.
            var nextProductKindId = NormalizeOptionalFk(request.PRODUCT_KIND_ID);
            var nextUnitId = NormalizeOptionalFk(request.UNIT_ID)
                ?? throw new ArgumentException("UNIT_ID is required");
            var nextStoreId = NormalizeOptionalFk(request.STORE_ID);
            var productKindChanged = nextProductKindId != NormalizeOptionalFk(existing.PRODUCT_KIND_ID);
            var unitChanged = nextUnitId != NormalizeOptionalFk(existing.UNIT_ID);
            if (productKindChanged || unitChanged)
            {
                await ValidateProductReferencesAsync(
                    companyCd,
                    nextProductKindId,
                    nextUnitId,
                    nextStoreId,
                    validateProductKind: productKindChanged,
                    validateUnit: unitChanged);
            }
            var nextProductCd = request.PRODUCT_CD == null ? existing.PRODUCT_CD : Common.NormalizeRequiredText(request.PRODUCT_CD);
            if (string.IsNullOrWhiteSpace(nextProductCd))
                throw new ArgumentException("PRODUCT_CD is required");
            var productCodeChanged = !string.Equals(nextProductCd, existing.PRODUCT_CD, StringComparison.Ordinal);
            if (productCodeChanged && await CodeExistsAsync(companyCd, nextProductCd, productId))
                throw new InvalidOperationException("PRODUCT_CD already exists");
            var payload = new ProductInfoDto
            {
                PRODUCT_ID = existing.PRODUCT_ID,
                COMPANY_CD = companyCd,
                PRODUCT_CD = nextProductCd,
                PRODUCT_NM_VIET = request.PRODUCT_NM_VIET == null ? existing.PRODUCT_NM_VIET : Common.NormalizeRequiredText(request.PRODUCT_NM_VIET),
                PRODUCT_NM_ENG = request.PRODUCT_NM_ENG == null ? existing.PRODUCT_NM_ENG : Common.NormalizeNullableText(request.PRODUCT_NM_ENG),
                PRODUCT_NM_KOR = request.PRODUCT_NM_KOR == null ? existing.PRODUCT_NM_KOR : Common.NormalizeNullableText(request.PRODUCT_NM_KOR),
                PRODUCT_NM_CHINA = request.PRODUCT_NM_CHINA == null ? existing.PRODUCT_NM_CHINA : Common.NormalizeNullableText(request.PRODUCT_NM_CHINA),
                PRODUCT_KIND_ID = nextProductKindId,
                UNIT_ID = nextUnitId,
                STORE_ID = nextStoreId,
                DIVISION = request.DIVISION == null ? existing.DIVISION : Common.NormalizeNullableText(request.DIVISION),
                SUMMARY = request.SUMMARY == null ? existing.SUMMARY : Common.NormalizeNullableText(request.SUMMARY),
                ISDEL = Common.NormalizeFlagString(request.ISDEL ?? existing.ISDEL, "0")
            };
            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0) throw new InvalidOperationException("Update failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ProductInfo updated: id={Id}, company={Company}", productId, companyCd);
            var updated = await GetByIdAsync(companyCd, productId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
            return updated;
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<int> productIds)
        {
            if (productIds == null || productIds.Count == 0)
                throw new ArgumentException("productIds is required");
            var ids = productIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Product {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "product", existing.PRODUCT_ID, existing.PRODUCT_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.PRODUCT_ID, existing.PRODUCT_CD ?? string.Empty, existing);
            }
            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids.Select(x => (long)x),
                    (s, cd, id) => _repository.DeleteProductInfoAsync(s, cd, (int)id, userId),
                    auditEntries,
                    "ProductInfo",
                    "product_info",
                    "Delete product_info"));
            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("ProductInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }
            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string productCd, int? excludeId = null, string? databaseName = null, string? lang = null)
            => _repository.ProductCdExistsAsync(companyCd, productCd, excludeId, databaseName);

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<ProductInfoDto> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
            {
                record.PRODUCT_CD = Common.NormalizeRequiredText(record.PRODUCT_CD);
                record.PRODUCT_NM_VIET = Common.NormalizeRequiredText(record.PRODUCT_NM_VIET);
                if (string.IsNullOrWhiteSpace(record.PRODUCT_NM_VIET))
                    throw new ArgumentException("PRODUCT_NM_VIET is required");

                record.PRODUCT_KIND_ID = NormalizeOptionalFk(record.PRODUCT_KIND_ID);
                record.UNIT_ID = NormalizeOptionalFk(record.UNIT_ID)
                    ?? throw new ArgumentException("UNIT_ID is required");
                record.STORE_ID = NormalizeOptionalFk(record.STORE_ID);
                await ValidateProductReferencesAsync(companyCd, record.PRODUCT_KIND_ID, record.UNIT_ID, record.STORE_ID);
            }

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.PRODUCT_CD),
                code => CodeExistsAsync(companyCd, code),
                "PRODUCT_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "ProductInfo",
                "product_info",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);
            return inserted;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, ProductInfoDto payload, ProductInfoDto? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);
            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.PRODUCT_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.PRODUCT_CD,
                    payload.PRODUCT_CD,
                    "PRODUCT_CD");
                var id = await _repository.SetProductInfoAsync(session, companyCd, userId, payload);
                if (id > 0)
                {
                    payload.PRODUCT_ID = id;
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "ProductInfo",
                        "product_info",
                        payload.PRODUCT_CD ?? existing?.PRODUCT_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert product_info");
                }
                return id;
            });
        }

        private static int? NormalizeOptionalFk(int? id)
            => id is > 0 ? id : null;

        /// <summary>
        /// PRODUCT_KIND_ID / STORE_ID optional. UNIT_ID required and must exist when set.
        /// </summary>
        private async Task ValidateProductReferencesAsync(
            string companyCd,
            int? productKindId,
            int? unitId,
            int? storeId,
            bool validateProductKind = true,
            bool validateUnit = true)
        {
            var resolvedKindId = productKindId.GetValueOrDefault();
            if (validateProductKind && resolvedKindId > 0 && !(await _productKindService.GetListAsync(companyCd, resolvedKindId)).Any())
            {
                throw new ArgumentException("PRODUCT_KIND_ID not found");
            }

            var resolvedUnitId = unitId.GetValueOrDefault();
            if (validateUnit && (resolvedUnitId <= 0 || !(await _productUnitService.GetListAsync(companyCd, resolvedUnitId)).Any()))
            {
                throw new ArgumentException(resolvedUnitId <= 0 ? "UNIT_ID is required" : "UNIT_ID not found");
            }

            _ = storeId;
        }
    }
}

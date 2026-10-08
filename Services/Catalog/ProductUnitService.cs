using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class ProductUnitService : IProductUnitService
    {
        private const string CacheScope = "product-unit";
        private const string SequenceMenuCode = "MD_UNIT";
        private const string SequenceCodeField = "UNIT_CD";

        private readonly IProductUnitRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<ProductUnitService> _logger;

        public ProductUnitService(
            IProductUnitRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<ProductUnitService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<ProductUnit>> GetListAsync(string companyCd, int? unitId = null, string? unitCd = null)
        {
            var cacheKey = $"list|unitId={unitId?.ToString() ?? string.Empty}|unitCd={unitCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetProductUnitAsync(companyCd, unitId, unitCd)).ToList());
        }

        public async Task<ProductUnit?> GetByIdAsync(string companyCd, int unitId)
            => (await GetListAsync(companyCd, unitId)).FirstOrDefault();

        private async Task<ProductUnit?> ResolveExistingAsync(string companyCd, ProductUnit request)
        {
            if (request.UNIT_ID > 0)
                return await GetByIdAsync(companyCd, request.UNIT_ID);
            var unitCd = Common.NormalizeNullableText(request.UNIT_CD);
            if (!string.IsNullOrWhiteSpace(unitCd))
                return (await GetListAsync(companyCd, null, unitCd)).FirstOrDefault();
            return null;
        }

        public async Task<ProductUnit> CreateAsync(string companyCd, string userId, ProductUnit request)
        {
            var payload = new ProductUnit
            {
                UNIT_ID = 0,
                COMPANY_CD = companyCd,
                UNIT_CD = Common.NormalizeNullableText(request.UNIT_CD) ?? string.Empty,
                UNIT_NM = Common.NormalizeNullableText(request.UNIT_NM),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0")
            };
            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("UNIT_CD already exists");
            var id = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (id <= 0) throw new InvalidOperationException("Create failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ProductUnit created: id={Id}, company={Company}", id, companyCd);
            return await GetByIdAsync(companyCd, id)
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<ProductUnit> UpdateAsync(string companyCd, string userId, int unitId, ProductUnit request)
        {
            var existing = await GetByIdAsync(companyCd, unitId)
                ?? throw new KeyNotFoundException("Unit not found");
            var nextUnitCd = request.UNIT_CD == null ? existing.UNIT_CD : Common.NormalizeRequiredText(request.UNIT_CD);
            if (string.IsNullOrWhiteSpace(nextUnitCd))
                throw new ArgumentException("UNIT_CD is required");
            if (await CodeExistsAsync(companyCd, nextUnitCd, unitId))
                throw new InvalidOperationException("UNIT_CD already exists");
            var payload = new ProductUnit
            {
                UNIT_ID = existing.UNIT_ID,
                COMPANY_CD = companyCd,
                UNIT_CD = nextUnitCd,
                UNIT_NM = request.UNIT_NM == null ? existing.UNIT_NM : Common.NormalizeNullableText(request.UNIT_NM),
                ISDEL = Common.NormalizeFlagString(request.ISDEL ?? existing.ISDEL, "0")
            };
            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0) throw new InvalidOperationException("Update failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ProductUnit updated: id={Id}, company={Company}", unitId, companyCd);
            return await GetByIdAsync(companyCd, unitId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<int> unitIds)
        {
            if (unitIds == null || unitIds.Count == 0)
                throw new ArgumentException("unitIds is required");
            var ids = unitIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Unit {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "unit", existing.UNIT_ID, existing.UNIT_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.UNIT_ID, existing.UNIT_CD ?? string.Empty, existing);
            }
            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids.Select(x => (long)x),
                    (s, cd, id) => _repository.DeleteProductUnitAsync(s, cd, (int)id, userId),
                    auditEntries,
                    "ProductUnit",
                    "product_unit",
                    "Delete product_unit"));
            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("ProductUnit deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }
            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string unitCd, int? excludeId = null, string? databaseName = null, string? lang = null)
            => _repository.ProductUnitCdExistsAsync(companyCd, unitCd, excludeId, databaseName);

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<ProductUnit> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
                record.UNIT_CD = Common.NormalizeRequiredText(record.UNIT_CD);

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.UNIT_CD),
                code => CodeExistsAsync(companyCd, code, null, databaseName),
                "UNIT_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "ProductUnit",
                "product_unit",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);
            return inserted;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, ProductUnit payload, ProductUnit? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);
            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.UNIT_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.UNIT_CD,
                    payload.UNIT_CD,
                    "UNIT_CD");
                var id = await _repository.SetProductUnitAsync(session, companyCd, userId, payload);
                if (id > 0)
                {
                    payload.UNIT_ID = id;
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "ProductUnit",
                        "product_unit",
                        payload.UNIT_CD ?? existing?.UNIT_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert product_unit");
                }
                return id;
            });
        }
    }
}

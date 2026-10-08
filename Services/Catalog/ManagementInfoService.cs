using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class ManagementInfoService : IManagementInfoService
    {
        private const string CacheScope = "management-info";
        private const string SequenceMenuCode = "MD_MANAGEMENT";
        private const string SequenceCodeField = "MG_CD";

        private readonly IManagementInfoRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<ManagementInfoService> _logger;

        public ManagementInfoService(
            IManagementInfoRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<ManagementInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<ManagementInfo>> GetListAsync(string companyCd, long? managementId = null, string? mgCd = null)
        {
            var cacheKey = $"list|managementId={managementId?.ToString() ?? string.Empty}|mgCd={mgCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetManagementInfoAsync(companyCd, managementId, mgCd)).ToList());
        }

        public async Task<ManagementInfo?> GetByIdAsync(string companyCd, long managementId)
            => (await GetListAsync(companyCd, managementId)).FirstOrDefault();

        private async Task<ManagementInfo?> ResolveExistingAsync(string companyCd, ManagementInfoRequest request)
        {
            if (request.MG_ID.HasValue && request.MG_ID.Value > 0)
                return await GetByIdAsync(companyCd, request.MG_ID.Value);
            var mgCd = Common.NormalizeNullableText(request.MG_CD);
            if (!string.IsNullOrWhiteSpace(mgCd))
                return (await GetListAsync(companyCd, null, mgCd)).FirstOrDefault();
            return null;
        }

        public async Task<ManagementInfo> CreateAsync(string companyCd, string userId, ManagementInfoRequest request)
        {
            var now = DateTime.Now;
            var payload = new ManagementInfoRequest
            {
                MG_ID = 0,
                COMPANY_CD = companyCd,
                MG_CD = Common.NormalizeNullableText(request.MG_CD) ?? string.Empty,
                MG_DESC_KOR = Common.NormalizeNullableText(request.MG_DESC_KOR),
                MG_DESC_ENG = Common.NormalizeNullableText(request.MG_DESC_ENG),
                MG_DESC_VIET = Common.NormalizeNullableText(request.MG_DESC_VIET),
                MG_CD_ROOT = Common.NormalizeNullableText(request.MG_CD_ROOT) ?? string.Empty,
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0"),
                CREATE_AT = now,
                CREATE_BY = userId,
                UPDATE_AT = now,
                UPDATE_BY = userId
            };
            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("MG_CD already exists");
            var result = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (result <= 0) throw new InvalidOperationException("Create failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ManagementInfo created: mgCd={MgCd}, company={Company}", payload.MG_CD, companyCd);
            return (await GetListAsync(companyCd, null, payload.MG_CD)).FirstOrDefault()
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<ManagementInfo> UpdateAsync(string companyCd, string userId, long managementId, ManagementInfoRequest request)
        {
            var existing = await GetByIdAsync(companyCd, managementId)
                ?? throw new KeyNotFoundException("Management not found");
            var nextMgCd = request.MG_CD == null ? existing.MG_CD : Common.NormalizeRequiredText(request.MG_CD);
            if (string.IsNullOrWhiteSpace(nextMgCd))
                throw new ArgumentException("MG_CD is required");
            if (await CodeExistsAsync(companyCd, nextMgCd, managementId))
                throw new InvalidOperationException("MG_CD already exists");
            var payload = new ManagementInfoRequest
            {
                MG_ID = existing.MG_ID,
                COMPANY_CD = companyCd,
                MG_CD = nextMgCd,
                MG_DESC_KOR = request.MG_DESC_KOR == null ? existing.MG_DESC_KOR : Common.NormalizeNullableText(request.MG_DESC_KOR),
                MG_DESC_ENG = request.MG_DESC_ENG == null ? existing.MG_DESC_ENG : Common.NormalizeNullableText(request.MG_DESC_ENG),
                MG_DESC_VIET = request.MG_DESC_VIET == null ? existing.MG_DESC_VIET : Common.NormalizeNullableText(request.MG_DESC_VIET),
                MG_CD_ROOT = request.MG_CD_ROOT == null ? existing.MG_CD_ROOT : Common.NormalizeNullableText(request.MG_CD_ROOT) ?? string.Empty,
                ISDEL = request.ISDEL == null ? Common.NormalizeFlagString(existing.ISDEL, "0") : Common.NormalizeFlagString(request.ISDEL, "0"),
                CREATE_AT = existing.CREATE_AT,
                CREATE_BY = existing.CREATE_BY,
                UPDATE_AT = DateTime.Now,
                UPDATE_BY = userId
            };
            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0) throw new InvalidOperationException("Update failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("ManagementInfo updated: mgId={MgId}, company={Company}", managementId, companyCd);
            return await GetByIdAsync(companyCd, managementId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<long> managementIds)
        {
            if (managementIds == null || managementIds.Count == 0)
                throw new ArgumentException("managementIds is required");
            var ids = managementIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Management {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "management", existing.MG_ID, existing.MG_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.MG_ID, existing.MG_CD ?? string.Empty, existing);
            }
            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids,
                    (s, cd, id) => _repository.DeleteManagementInfoAsync(s, cd, id, userId),
                    auditEntries,
                    "ManagementInfo",
                    "management_info",
                    "Delete management_info"));
            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("ManagementInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }
            return result;
        }

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<ManagementInfoRequest> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
                record.MG_CD = Common.NormalizeRequiredText(record.MG_CD);

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.MG_CD),
                code => CodeExistsAsync(companyCd, code),
                "MG_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "ManagementInfo",
                "management_info",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);
            return inserted;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string mgCd, long? excludeId = null)
            => _repository.MgCdExistsAsync(companyCd, mgCd, excludeId);

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, ManagementInfoRequest payload, ManagementInfo? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);
            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.MG_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.MG_CD,
                    payload.MG_CD,
                    "MG_CD");
                var result = await _repository.SetManagementInfoAsync(session, companyCd, userId, payload);
                if (result > 0)
                {
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "ManagementInfo",
                        "management_info",
                        payload.MG_CD ?? existing?.MG_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert management_info");
                }
                return result;
            });
        }
    }
}

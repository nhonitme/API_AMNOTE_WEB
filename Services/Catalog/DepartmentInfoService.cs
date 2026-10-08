using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class DepartmentInfoService : IDepartmentInfoService
    {
        private const string CacheScope = "department-info";
        private const string SequenceMenuCode = "MD_COST_CENTER";
        private const string SequenceCodeField = "DEPARTMENT_CD";

        private readonly IDepartmentInfoRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<DepartmentInfoService> _logger;

        public DepartmentInfoService(
            IDepartmentInfoRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<DepartmentInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<DepartmentInfo>> GetListAsync(string companyCd, long? departmentId = null, string? departmentCd = null)
        {
            var cacheKey = $"list|departmentId={departmentId?.ToString() ?? string.Empty}|departmentCd={departmentCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetDepartmentInfoAsync(companyCd, departmentId, departmentCd)).ToList());
        }

        public async Task<DepartmentInfo?> GetByIdAsync(string companyCd, long departmentId)
            => (await GetListAsync(companyCd, departmentId)).FirstOrDefault();

        private async Task<DepartmentInfo?> ResolveExistingAsync(string companyCd, DepartmentInfoRequest request)
        {
            if (request.DEPARTMENT_ID.HasValue && request.DEPARTMENT_ID.Value > 0)
                return await GetByIdAsync(companyCd, request.DEPARTMENT_ID.Value);

            var departmentCd = NormalizeNullableText(request.DEPARTMENT_CD);
            if (!string.IsNullOrWhiteSpace(departmentCd))
                return (await GetListAsync(companyCd, null, departmentCd)).FirstOrDefault();

            return null;
        }

        public async Task<DepartmentInfo> CreateAsync(string companyCd, string userId, DepartmentInfoRequest request)
        {
            var departmentName = NormalizeRequiredText(request.DEP_NAME_VIET);
            if (string.IsNullOrWhiteSpace(departmentName))
                throw new ArgumentException("DEP_NAME_VIET is required");

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var payload = new DepartmentInfoRequest
            {
                DEPARTMENT_ID = 0,
                COMPANY_CD = companyCd,
                DEPARTMENT_CD = NormalizeNullableText(request.DEPARTMENT_CD) ?? string.Empty,
                PARENT_CD = NormalizeNullableText(request.PARENT_CD) ?? string.Empty,
                DEP_NAME_KOR = NormalizeNullableText(request.DEP_NAME_KOR),
                DEP_NAME_ENG = NormalizeNullableText(request.DEP_NAME_ENG),
                DEP_NAME_VIET = departmentName,
                DEP_NAME_CHINA = NormalizeNullableText(request.DEP_NAME_CHINA),
                UPDATE_AT = now,
                UPDATE_BY = userId,
                CREATE_AT = now,
                CREATE_BY = userId,
                ISDEL = NormalizeFlagString(request.ISDEL, "0")
            };

            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("DEPARTMENT_CD already exists");

            var result = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (result <= 0) throw new InvalidOperationException("Create failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("DepartmentInfo created: departmentCd={DepartmentCd}, company={Company}", payload.DEPARTMENT_CD, companyCd);
            return (await GetListAsync(companyCd, null, payload.DEPARTMENT_CD)).FirstOrDefault()
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<DepartmentInfo> UpdateAsync(string companyCd, string userId, long departmentId, DepartmentInfoRequest request)
        {
            var existing = await GetByIdAsync(companyCd, departmentId)
                ?? throw new KeyNotFoundException("Department not found");

            var nextDepartmentCd = request.DEPARTMENT_CD == null ? existing.DEPARTMENT_CD : NormalizeRequiredText(request.DEPARTMENT_CD);
            if (string.IsNullOrWhiteSpace(nextDepartmentCd))
                throw new ArgumentException("DEPARTMENT_CD is required");

            var nextName = request.DEP_NAME_VIET == null ? existing.DEP_NAME_VIET : NormalizeRequiredText(request.DEP_NAME_VIET);
            if (string.IsNullOrWhiteSpace(nextName))
                throw new ArgumentException("DEP_NAME_VIET is required");

            if (await CodeExistsAsync(companyCd, nextDepartmentCd, departmentId))
                throw new InvalidOperationException("DEPARTMENT_CD already exists");

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var payload = new DepartmentInfoRequest
            {
                DEPARTMENT_ID = existing.DEPARTMENT_ID,
                COMPANY_CD = companyCd,
                DEPARTMENT_CD = nextDepartmentCd,
                PARENT_CD = request.PARENT_CD == null ? existing.PARENT_CD : NormalizeNullableText(request.PARENT_CD) ?? string.Empty,
                DEP_NAME_KOR = request.DEP_NAME_KOR == null ? existing.DEP_NAME_KOR : NormalizeNullableText(request.DEP_NAME_KOR),
                DEP_NAME_ENG = request.DEP_NAME_ENG == null ? existing.DEP_NAME_ENG : NormalizeNullableText(request.DEP_NAME_ENG),
                DEP_NAME_VIET = nextName,
                DEP_NAME_CHINA = request.DEP_NAME_CHINA == null ? existing.DEP_NAME_CHINA : NormalizeNullableText(request.DEP_NAME_CHINA),
                UPDATE_AT = now,
                UPDATE_BY = userId,
                CREATE_AT = existing.CREATE_AT,
                CREATE_BY = existing.CREATE_BY,
                ISDEL = NormalizeFlagString(request.ISDEL ?? existing.ISDEL, "0")
            };

            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0) throw new InvalidOperationException("Update failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("DepartmentInfo updated: departmentId={DepartmentId}, company={Company}", departmentId, companyCd);
            return await GetByIdAsync(companyCd, departmentId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<long> departmentIds)
        {
            if (departmentIds == null || departmentIds.Count == 0)
                throw new ArgumentException("departmentIds is required");

            var ids = departmentIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Department {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "department", existing.DEPARTMENT_ID, existing.DEPARTMENT_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.DEPARTMENT_ID, existing.DEPARTMENT_CD ?? string.Empty, existing);
            }

            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids,
                    (s, cd, id) => _repository.DeleteDepartmentInfoAsync(s, cd, id, userId),
                    auditEntries,
                    "DepartmentInfo",
                    "department_info",
                    "Delete department_info"));

            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("DepartmentInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }
            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string departmentCd, long? excludeId = null, string? databaseName = null, string? lang = null)
            => _repository.DepartmentCdExistsAsync(companyCd, departmentCd, excludeId, databaseName);

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<DepartmentInfoRequest> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
            {
                record.DEPARTMENT_CD = Common.NormalizeRequiredText(record.DEPARTMENT_CD);
                record.PARENT_CD = NormalizeNullableText(record.PARENT_CD) ?? string.Empty;
                record.DEP_NAME_VIET = NormalizeRequiredText(record.DEP_NAME_VIET);
                if (string.IsNullOrWhiteSpace(record.DEP_NAME_VIET))
                    throw new ArgumentException("DEP_NAME_VIET is required");
            }

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.DEPARTMENT_CD),
                code => CodeExistsAsync(companyCd, code),
                "DEPARTMENT_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "DepartmentInfo",
                "department_info",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);

            return inserted;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, DepartmentInfoRequest payload, DepartmentInfo? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);

            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.DEPARTMENT_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.DEPARTMENT_CD,
                    payload.DEPARTMENT_CD,
                    "DEPARTMENT_CD");

                var result = await _repository.SetDepartmentInfoAsync(session, companyCd, userId, payload);
                if (result > 0)
                {
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "DepartmentInfo",
                        "department_info",
                        payload.DEPARTMENT_CD ?? existing?.DEPARTMENT_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert department_info");
                }

                return result;
            });
        }
    }
}

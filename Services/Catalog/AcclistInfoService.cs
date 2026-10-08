using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class AcclistInfoService : IAcclistInfoService
    {
        private const string CacheScope = "acclist-info";

        private readonly IAcclistInfoRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<AcclistInfoService> _logger;

        public AcclistInfoService(
            IAcclistInfoRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<AcclistInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<AcclistInfo>> GetListAsync(string companyCd, int? accId = null)
        {
            var cacheKey = $"list|accId={accId?.ToString() ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetAcclistInfoAsync(companyCd, accId)).ToList());
        }

        public async Task<AcclistInfo?> GetByIdAsync(string companyCd, int accId)
            => (await GetListAsync(companyCd, accId)).FirstOrDefault();

        private async Task<AcclistInfo?> ResolveExistingAsync(string companyCd, AcclistInfoRequest request)
        {
            if (request.ACC_ID > 0)
                return await GetByIdAsync(companyCd, request.ACC_ID);

            var accCd = Common.NormalizeNullableText(request.ACC_CD);
            if (!string.IsNullOrWhiteSpace(accCd))
            {
                return (await GetListAsync(companyCd))
                    .FirstOrDefault(a => string.Equals(a.ACC_CD, accCd, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        public async Task<AcclistInfo> CreateAsync(string companyCd, string userId, AcclistInfoRequest request)
        {
            var accTitle = Common.NormalizeRequiredText(request.ACCTITLE_NM_VIET);
            if (string.IsNullOrWhiteSpace(accTitle))
                throw new ArgumentException("ACCTITLE_NM_VIET is required");

            var accCd = Common.NormalizeRequiredText(request.ACC_CD);
            if (string.IsNullOrWhiteSpace(accCd))
                throw new ArgumentException("ACC_CD is required");

            var payload = new AcclistInfoRequest
            {
                ACC_ID = 0,
                ACC_CD = accCd,
                ACC_PARENT_ID = request.ACC_PARENT_ID,
                ACCTITLE_NM_KOR = Common.NormalizeNullableText(request.ACCTITLE_NM_KOR) ?? string.Empty,
                ACCTITLE_NM_ENG = Common.NormalizeNullableText(request.ACCTITLE_NM_ENG) ?? string.Empty,
                ACCTITLE_NM_VIET = accTitle,
                ACCTITLE_NM_CHINA = Common.NormalizeNullableText(request.ACCTITLE_NM_CHINA) ?? string.Empty,
                ISABLETYPE = request.ISABLETYPE > 0 ? request.ISABLETYPE : 1,
                ISUSERADD = Common.NormalizeFlagString(request.ISUSERADD, "1")
            };

            ApplyStoredProcedureDefaults(payload);

            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("ACC_CD already exists");

            var result = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (result <= 0)
                throw new InvalidOperationException("Create failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("AcclistInfo created: accCd={AccCd}, company={Company}", payload.ACC_CD, companyCd);
            return (await GetListAsync(companyCd))
                .FirstOrDefault(a => string.Equals(a.ACC_CD, payload.ACC_CD, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<AcclistInfo> UpdateAsync(string companyCd, string userId, int accId, AcclistInfoRequest request)
        {
            var existing = await GetByIdAsync(companyCd, accId)
                ?? throw new KeyNotFoundException("Account not found");

            var nextAccCd = request.ACC_CD == null ? existing.ACC_CD : Common.NormalizeRequiredText(request.ACC_CD);
            if (string.IsNullOrWhiteSpace(nextAccCd))
                throw new ArgumentException("ACC_CD is required");

            var nextTitle = request.ACCTITLE_NM_VIET == null
                ? existing.ACCTITLE_NM_VIET
                : Common.NormalizeRequiredText(request.ACCTITLE_NM_VIET);
            if (string.IsNullOrWhiteSpace(nextTitle))
                throw new ArgumentException("ACCTITLE_NM_VIET is required");

            if (await CodeExistsAsync(companyCd, nextAccCd, accId))
                throw new InvalidOperationException("ACC_CD already exists");

            var payload = new AcclistInfoRequest
            {
                ACC_ID = existing.ACC_ID,
                ACC_CD = nextAccCd,
                ACC_PARENT_ID = request.ACC_PARENT_ID > 0 ? request.ACC_PARENT_ID : existing.ACC_PARENT_ID,
                ACCTITLE_NM_KOR = request.ACCTITLE_NM_KOR == null
                    ? existing.ACCTITLE_NM_KOR
                    : Common.NormalizeNullableText(request.ACCTITLE_NM_KOR) ?? string.Empty,
                ACCTITLE_NM_ENG = request.ACCTITLE_NM_ENG == null
                    ? existing.ACCTITLE_NM_ENG
                    : Common.NormalizeNullableText(request.ACCTITLE_NM_ENG) ?? string.Empty,
                ACCTITLE_NM_VIET = nextTitle,
                ACCTITLE_NM_CHINA = request.ACCTITLE_NM_CHINA == null
                    ? existing.ACCTITLE_NM_CHINA
                    : Common.NormalizeNullableText(request.ACCTITLE_NM_CHINA) ?? string.Empty,
                ISABLETYPE = request.ISABLETYPE > 0 ? request.ISABLETYPE : existing.ISABLETYPE,
                ISUSERADD = Common.NormalizeFlagString(request.ISUSERADD ?? existing.ISUSERADD, existing.ISUSERADD),
                ISCUSTOMER = existing.ISCUSTOMER,
                DECISION = string.IsNullOrWhiteSpace(existing.DECISION) ? "C99" : existing.DECISION,
                ISDEL = existing.ISDEL,
                DESTINATION_ACC_CD = existing.DESTINATION_ACC_CD
            };

            ApplyStoredProcedureDefaults(payload);

            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0)
                throw new InvalidOperationException("Update failed");

            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("AcclistInfo updated: accId={AccId}, company={Company}", accId, companyCd);
            return await GetByIdAsync(companyCd, accId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<int> accIds)
        {
            if (accIds == null || accIds.Count == 0)
                throw new ArgumentException("accIds is required");

            var ids = accIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Account {id} not found");

                if (existing.ISUSERADD + "" != "1")
                    throw new InvalidOperationException("Không thể xóa tài khoản gốc");

                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "account", existing.ACC_ID, existing.ACC_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);

                auditEntries[id] = DeleteActivityLogEntry.From(existing.ACC_ID, existing.ACC_CD ?? string.Empty, existing);
            }

            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids.Select(i => (long)i),
                    (s, cd, id) => _repository.DeleteAcclistInfoAsync(s, cd, (int)id, userId),
                    auditEntries,
                    "AcclistInfo",
                    "acclist_info",
                    "Delete acclist_info"));

            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("AcclistInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }

            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string accCd, int? excludeId = null)
            => _repository.AccCdExistsAsync(companyCd, accCd, excludeId);

        private static void ApplyStoredProcedureDefaults(AcclistInfoRequest request)
        {
            request.ACC_CD = Common.NormalizeRequiredText(request.ACC_CD) ?? request.ACC_CD;
            request.LEVEL = Math.Max(0, request.ACC_CD.Length - 2);
            request.ISABLEINPUT = "1";
            request.ACCTITLE_NM_JAPAN = string.Empty;
            request.ISCUSTOMER = "0";
            request.DECISION = string.IsNullOrWhiteSpace(request.DECISION) ? "C99" : request.DECISION;
            request.ISDEL = Common.NormalizeFlagString(request.ISDEL, "0");
            request.DESTINATION_ACC_CD = request.DESTINATION_ACC_CD ?? string.Empty;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, AcclistInfoRequest payload, AcclistInfo? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);

            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                var result = await _repository.SetAcclistInfoAsync(session, companyCd, userId, payload);
                if (result > 0)
                {
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "AcclistInfo",
                        "acclist_info",
                        payload.ACC_CD ?? existing?.ACC_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert acclist_info");
                }

                return result;
            });
        }
    }
}

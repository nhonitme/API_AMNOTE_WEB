using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class BankInfoService : IBankInfoService
    {
        private const string CacheScope = "bank-info";
        private const string SequenceMenuCode = "MD_BANK";
        private const string SequenceCodeField = "BANK_CD";

        private readonly IBankInfoRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<BankInfoService> _logger;

        public BankInfoService(
            IBankInfoRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<BankInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<BankInfo>> GetListAsync(string companyCd, long? bankId = null, string? bankCd = null)
        {
            var cacheKey = $"list|bankId={bankId?.ToString() ?? string.Empty}|bankCd={bankCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetBankInfoAsync(companyCd, bankId, bankCd)).ToList());
        }

        public async Task<BankInfo?> GetByIdAsync(string companyCd, long bankId)
            => (await GetListAsync(companyCd, bankId)).FirstOrDefault();

        private async Task<BankInfo?> ResolveExistingAsync(string companyCd, BankInfoRequest request)
        {
            if (request.BANK_ID.HasValue && request.BANK_ID.Value > 0)
                return await GetByIdAsync(companyCd, request.BANK_ID.Value);
            var bankCd = Common.NormalizeNullableText(request.BANK_CD);
            if (!string.IsNullOrWhiteSpace(bankCd))
                return (await GetListAsync(companyCd, null, bankCd)).FirstOrDefault();
            return null;
        }

        public async Task<BankInfo> CreateAsync(string companyCd, string userId, BankInfoRequest request)
        {
            var bankName = Common.NormalizeRequiredText(request.BANK_NM);
            if (string.IsNullOrWhiteSpace(bankName))
                throw new ArgumentException("BANK_NM is required");
            var bankCd = Common.NormalizeNullableText(request.BANK_CD);
            var payload = new BankInfoRequest
            {
                BANK_ID = 0,
                COMPANY_CD = companyCd,
                BANK_CD = bankCd ?? string.Empty,
                BANK_NM = bankName,
                ACC_CD = Common.NormalizeNullableText(request.ACC_CD),
                PASSBOOK_NM = Common.NormalizeNullableText(request.PASSBOOK_NM),
                ACCOUNT_NUM = Common.NormalizeNullableText(request.ACCOUNT_NUM),
                CITAD_CODE = Common.NormalizeNullableText(request.CITAD_CODE),
                REMARK = Common.NormalizeNullableText(request.REMARK),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0"),
                CREATE_BY = userId,
                UPDATE_BY = userId
            };
            if (await ResolveExistingAsync(companyCd, payload) != null)
                throw new InvalidOperationException("BANK_CD already exists");
            var result = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (result <= 0) throw new InvalidOperationException("Create failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("BankInfo created: bankCd={BankCd}, company={Company}", payload.BANK_CD, companyCd);
            return (await GetListAsync(companyCd, null, payload.BANK_CD)).FirstOrDefault()
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<BankInfo> UpdateAsync(string companyCd, string userId, long bankId, BankInfoRequest request)
        {
            var existing = await GetByIdAsync(companyCd, bankId)
                ?? throw new KeyNotFoundException("Bank not found");
            var nextBankCd = request.BANK_CD == null ? existing.BANK_CD : Common.NormalizeRequiredText(request.BANK_CD);
            if (string.IsNullOrWhiteSpace(nextBankCd))
                throw new ArgumentException("BANK_CD is required");
            var nextBankName = Common.NormalizeNullableText(request.BANK_NM) ?? existing.BANK_NM;
            if (string.IsNullOrWhiteSpace(nextBankName))
                throw new ArgumentException("BANK_NM is required");
            if (await CodeExistsAsync(companyCd, nextBankCd, bankId))
                throw new InvalidOperationException("BANK_CD already exists");
            var payload = new BankInfoRequest
            {
                BANK_ID = existing.BANK_ID,
                COMPANY_CD = companyCd,
                BANK_CD = nextBankCd,
                BANK_NM = nextBankName,
                ACC_CD = Common.NormalizeNullableText(request.ACC_CD) ?? existing.ACC_CD,
                PASSBOOK_NM = Common.NormalizeNullableText(request.PASSBOOK_NM) ?? existing.PASSBOOK_NM,
                ACCOUNT_NUM = Common.NormalizeNullableText(request.ACCOUNT_NUM) ?? existing.ACCOUNT_NUM,
                CITAD_CODE = Common.NormalizeNullableText(request.CITAD_CODE) ?? existing.CITAD_CODE,
                REMARK = Common.NormalizeNullableText(request.REMARK) ?? existing.REMARK,
                ISDEL = Common.NormalizeFlagString(request.ISDEL ?? existing.ISDEL, "0"),
                CREATE_BY = existing.CREATE_BY,
                UPDATE_BY = userId
            };
            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0) throw new InvalidOperationException("Update failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("BankInfo updated: bankId={BankId}, company={Company}", bankId, companyCd);
            return await GetByIdAsync(companyCd, bankId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<long> bankIds)
        {
            if (bankIds == null || bankIds.Count == 0)
                throw new ArgumentException("bankIds is required");
            var ids = bankIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Bank {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "bank", existing.BANK_ID, existing.BANK_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.BANK_ID, existing.BANK_CD ?? string.Empty, existing);
            }
            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids,
                    _repository.DeleteBankInfoAsync,
                    auditEntries,
                    "BankInfo",
                    "bank_info",
                    "Delete bank_info"));
            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("BankInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }
            return result;
        }

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<BankInfoRequest> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
            {
                record.BANK_CD = Common.NormalizeRequiredText(record.BANK_CD);
                record.BANK_NM = Common.NormalizeRequiredText(record.BANK_NM);
                if (string.IsNullOrWhiteSpace(record.BANK_NM))
                    throw new ArgumentException("BANK_NM is required");
            }

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.BANK_CD),
                code => CodeExistsAsync(companyCd, code),
                "BANK_CD", lang);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewAsync(session, companyCd, userId, records),
                "BankInfo",
                "bank_info",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);
            return inserted;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string bankCd, long? excludeId = null)
            => _repository.BankCdExistsAsync(companyCd, bankCd, excludeId);

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, BankInfoRequest payload, BankInfo? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);
            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.BANK_CD = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    existing?.BANK_CD,
                    payload.BANK_CD,
                    "BANK_CD");
                var result = await _repository.SetBankInfoAsync(session, companyCd, userId, payload);
                if (result > 0)
                {
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "BankInfo",
                        "bank_info",
                        payload.BANK_CD ?? existing?.BANK_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert bank_info");
                }
                return result;
            });
        }
    }
}

using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.Catalog
{
    public class CustomerInfoService : ICustomerInfoService
    {
        private const string CacheScope = "customer-ext";
        private const string SequenceMenuCode = "MD_CUSTOMER";
        private const string SequenceCodeField = "CUSTOMER_CD";

        private readonly ICustomerInfoCustomerExtRepository _repository;
        private readonly IMasterInUseRepository _masterInUseRepository;
        private readonly IMasterDataCacheService _cache;
        private readonly ICatalogWriteSupport _writeSupport;
        private readonly ILogger<CustomerInfoService> _logger;

        public CustomerInfoService(
            ICustomerInfoCustomerExtRepository repository,
            IMasterInUseRepository masterInUseRepository,
            IMasterDataCacheService cache,
            ICatalogWriteSupport writeSupport,
            ILogger<CustomerInfoService> logger)
        {
            _repository = repository;
            _masterInUseRepository = masterInUseRepository;
            _cache = cache;
            _writeSupport = writeSupport;
            _logger = logger;
        }

        public async Task<IEnumerable<CustomerInfoCustomerExt>> GetListAsync(string companyCd, long? customerId = null, string? customerCd = null)
        {
            var cacheKey = $"list|customerId={customerId?.ToString() ?? string.Empty}|customerCd={customerCd ?? string.Empty}";
            return await _cache.GetOrCreateAsync(CacheScope, companyCd, cacheKey,
                async () => (await _repository.GetCustomerInfoCustomerExtAsync(companyCd, customerId, customerCd)).ToList());
        }

        public async Task<CustomerInfoCustomerExt?> GetByIdAsync(string companyCd, long customerId)
            => (await GetListAsync(companyCd, customerId)).FirstOrDefault();

        private async Task<CustomerInfoCustomerExt?> ResolveExistingAsync(string companyCd, CustomerInfoCustomerExtRequest request)
        {
            if (request.CUSTOMER_ID.HasValue && request.CUSTOMER_ID.Value > 0)
                return await GetByIdAsync(companyCd, request.CUSTOMER_ID.Value);
            var customerCd = Common.NormalizeNullableText(request.CUSTOMER_CD);
            if (!string.IsNullOrWhiteSpace(customerCd))
                return (await GetListAsync(companyCd, null, customerCd)).FirstOrDefault();
            return null;
        }

        public async Task<CustomerInfoCustomerExt> CreateAsync(string companyCd, string userId, CustomerInfoCustomerExtRequest request)
        {
            var now = DateTime.Now;
            var payload = new CustomerInfoCustomerExtRequest
            {
                CUSTOMER_ID = 0,
                CUSTOMER_EXT_ID = 0,
                COMPANY_CD = companyCd,
                CUSTOMER_CD = Common.NormalizeNullableText(request.CUSTOMER_CD) ?? string.Empty,
                CATEGORY_CD = Common.NormalizeNullableText(request.CATEGORY_CD),
                CUSTOMER_TYPE = Common.NormalizeNullableText(request.CUSTOMER_TYPE),
                CUSTOMER_NM_VIET = Common.NormalizeNullableText(request.CUSTOMER_NM_VIET),
                CUSTOMER_NM_ENG = Common.NormalizeNullableText(request.CUSTOMER_NM_ENG),
                CUSTOMER_NM_KOR = Common.NormalizeNullableText(request.CUSTOMER_NM_KOR),
                CUSTOMER_NM_CHINA = Common.NormalizeNullableText(request.CUSTOMER_NM_CHINA),
                ADDRESS = Common.NormalizeNullableText(request.ADDRESS),
                TEL = Common.NormalizeNullableText(request.TEL),
                FAX = Common.NormalizeNullableText(request.FAX),
                TAX_CD = Common.NormalizeNullableText(request.TAX_CD),
                BANK_ID = request.BANK_ID,
                BANK_CD = Common.NormalizeNullableText(request.BANK_CD),
                EMAIL = Common.NormalizeNullableText(request.EMAIL),
                NOTE = Common.NormalizeNullableText(request.NOTE),
                IDNUMBER = Common.NormalizeNullableText(request.IDNUMBER),
                BUYER_NM = Common.NormalizeNullableText(request.BUYER_NM),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0"),
                CREATE_AT = now,
                CREATE_BY = userId,
                UPDATE_AT = now,
                UPDATE_BY = userId
            };
            await EnsureCustomerIdentityUniqueAsync(companyCd, payload.TAX_CD, payload.CUSTOMER_NM_VIET, payload.ADDRESS, null);
            var result = await PersistUpsertAsync(companyCd, userId, payload, null);
            if (result <= 0) throw new InvalidOperationException("Create failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("CustomerInfo created: customerCd={CustomerCd}, company={Company}", payload.CUSTOMER_CD, companyCd);
            return (await GetListAsync(companyCd, null, payload.CUSTOMER_CD)).FirstOrDefault()
                ?? throw new InvalidOperationException("Failed to fetch created record");
        }

        public async Task<CustomerInfoCustomerExt> UpdateAsync(string companyCd, string userId, long customerId, CustomerInfoCustomerExtRequest request)
        {
            var existing = await GetByIdAsync(companyCd, customerId)
                ?? throw new KeyNotFoundException("Customer not found");
            // null = field omitted (keep existing); empty string = clear name (allowed)
            var nextCustomerName = request.CUSTOMER_NM_VIET == null
                ? existing.CUSTOMER_NM_VIET
                : Common.NormalizeNullableText(request.CUSTOMER_NM_VIET);
            var nextCustomerCd = request.CUSTOMER_CD == null ? existing.CUSTOMER_CD : Common.NormalizeNullableText(request.CUSTOMER_CD);
            if (!string.IsNullOrWhiteSpace(nextCustomerCd) && await CodeExistsAsync(companyCd, nextCustomerCd, customerId))
                throw new InvalidOperationException("CUSTOMER_CD already exists");
            var now = DateTime.Now;
            var payload = new CustomerInfoCustomerExtRequest
            {
                CUSTOMER_ID = existing.CUSTOMER_ID,
                CUSTOMER_EXT_ID = existing.CUSTOMER_EXT_ID,
                COMPANY_CD = companyCd,
                CUSTOMER_CD = nextCustomerCd,
                CATEGORY_CD = Common.NormalizeNullableText(request.CATEGORY_CD) ?? existing.CATEGORY_CD,
                CUSTOMER_TYPE = Common.NormalizeNullableText(request.CUSTOMER_TYPE) ?? existing.CUSTOMER_TYPE,
                CUSTOMER_NM_VIET = nextCustomerName,
                CUSTOMER_NM_ENG = Common.NormalizeNullableText(request.CUSTOMER_NM_ENG) ?? existing.CUSTOMER_NM_ENG,
                CUSTOMER_NM_KOR = Common.NormalizeNullableText(request.CUSTOMER_NM_KOR) ?? existing.CUSTOMER_NM_KOR,
                CUSTOMER_NM_CHINA = Common.NormalizeNullableText(request.CUSTOMER_NM_CHINA) ?? existing.CUSTOMER_NM_CHINA,
                ADDRESS = Common.NormalizeNullableText(request.ADDRESS) ?? existing.ADDRESS,
                TEL = Common.NormalizeNullableText(request.TEL) ?? existing.TEL,
                FAX = Common.NormalizeNullableText(request.FAX) ?? existing.FAX,
                TAX_CD = Common.NormalizeNullableText(request.TAX_CD) ?? existing.TAX_CD,
                BANK_ID = request.BANK_ID ?? existing.BANK_ID,
                BANK_CD = Common.NormalizeNullableText(request.BANK_CD) ?? existing.BANK_CD,
                EMAIL = Common.NormalizeNullableText(request.EMAIL) ?? existing.EMAIL,
                NOTE = Common.NormalizeNullableText(request.NOTE) ?? existing.NOTE,
                IDNUMBER = Common.NormalizeNullableText(request.IDNUMBER) ?? existing.IDNUMBER,
                BUYER_NM = Common.NormalizeNullableText(request.BUYER_NM) ?? existing.BUYER_NM,
                ISDEL = Common.NormalizeFlagString(request.ISDEL ?? existing.ISDEL, "0"),
                CREATE_AT = existing.CREATE_AT,
                CREATE_BY = existing.CREATE_BY,
                UPDATE_AT = now,
                UPDATE_BY = userId
            };
            await EnsureCustomerIdentityUniqueAsync(
                companyCd,
                payload.TAX_CD,
                payload.CUSTOMER_NM_VIET,
                payload.ADDRESS,
                existing.CUSTOMER_ID);
            var result = await PersistUpsertAsync(companyCd, userId, payload, existing);
            if (result <= 0) throw new InvalidOperationException("Update failed");
            await _cache.ClearAsync(CacheScope, companyCd);
            _logger.LogInformation("CustomerInfo updated: customerId={CustomerId}, company={Company}", customerId, companyCd);
            return await GetByIdAsync(companyCd, customerId)
                ?? throw new InvalidOperationException("Failed to fetch updated record");
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, List<long> customerIds)
        {
            if (customerIds == null || customerIds.Count == 0)
                throw new ArgumentException("customerIds is required");
            var ids = customerIds.Distinct().ToList();
            var auditEntries = new Dictionary<long, DeleteActivityLogEntry>();
            foreach (var id in ids)
            {
                var existing = await GetByIdAsync(companyCd, id)
                    ?? throw new KeyNotFoundException($"Customer {id} not found");
                var inUse = await _masterInUseRepository.CheckInUseAsync(companyCd, "customer", existing.CUSTOMER_ID, existing.CUSTOMER_CD);
                if (inUse.IsUsed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(inUse.MESSAGE) ? "Danh mục đã được sử dụng." : inUse.MESSAGE);
                auditEntries[id] = DeleteActivityLogEntry.From(existing.CUSTOMER_ID, existing.CUSTOMER_CD ?? string.Empty, existing);
            }
            var result = await _writeSupport.ExecuteInTransactionAsync(session =>
                _writeSupport.DeleteBatchAsync(
                    session,
                    companyCd,
                    ids,
                    (s, cd, id) => _repository.DeleteCustomerInfoCustomerExtAsync(s, cd, id, userId),
                    auditEntries,
                    "CustomerInfoCustomerExt",
                    "customer_info,customer_ext",
                    "Delete customer info"));
            if (result > 0)
            {
                await _cache.ClearAsync(CacheScope, companyCd);
                _logger.LogInformation("CustomerInfo deleted: count={Count}, company={Company}", ids.Count, companyCd);
            }
            return result;
        }

        public Task<bool> CodeExistsAsync(string companyCd, string customerCd, long? excludeId = null)
            => _repository.CustomerCdExistsAsync(companyCd, customerCd, excludeId);

        public async Task<int> BulkInsertAsync(string companyCd, string userId, List<CustomerInfoCustomerExtRequest> records, string? databaseName = null, string? lang = null)
        {
            if (records == null || records.Count == 0)
                return 0;

            foreach (var record in records)
                record.CUSTOMER_CD = Common.NormalizeRequiredText(record.CUSTOMER_CD);

            await CatalogCodeUniqueness.EnsureNewCodesUniqueAsync(
                records.Select(r => r.CUSTOMER_CD),
                code => CodeExistsAsync(companyCd, code),
                "CUSTOMER_CD", lang);

            await EnsureCustomerIdentitiesUniqueForBulkAsync(companyCd, records);

            var inserted = await CatalogExcelBulkInsert.ExecuteAsync(
                _writeSupport,
                companyCd,
                databaseName,
                session => _repository.BulkInsertNewCustomersAsync(session, companyCd, userId, records),
                "CustomerInfoCustomerExt",
                "customer_info,customer_ext",
                _logger);

            if (inserted > 0)
                await _cache.ClearAsync(CacheScope, companyCd);

            _logger.LogInformation(
                "CustomerInfo bulk insert done: company={Company} requested={Requested} inserted={Inserted}",
                companyCd,
                records.Count,
                inserted);
            return inserted;
        }

        private async Task<int> PersistUpsertAsync(string companyCd, string userId, CustomerInfoCustomerExtRequest payload, CustomerInfoCustomerExt? existing)
        {
            var isInsert = existing == null;
            var oldData = isInsert ? null : JsonSerializer.Serialize(existing);
            return await _writeSupport.ExecuteInTransactionAsync(async session =>
            {
                payload.CUSTOMER_CD = await ResolveCustomerCodeAsync(
                    session,
                    companyCd,
                    existing,
                    payload.CUSTOMER_CD,
                    isInsert);
                if (!string.IsNullOrWhiteSpace(payload.CUSTOMER_CD) &&
                    await _repository.CustomerCdExistsAsync(companyCd, payload.CUSTOMER_CD, existing?.CUSTOMER_ID))
                {
                    throw new InvalidOperationException($"Customer '{payload.CUSTOMER_CD}' already exists for company '{companyCd}'.");
                }
                var result = await _repository.SetCustomerInfoCustomerExtAsync(session, companyCd, userId, payload);
                if (result > 0)
                {
                    await _writeSupport.LogUpsertAsync(
                        session,
                        companyCd,
                        isInsert,
                        "CustomerInfoCustomerExt",
                        "customer_info,customer_ext",
                        payload.CUSTOMER_CD ?? existing?.CUSTOMER_CD ?? string.Empty,
                        oldData,
                        payload,
                        "Upsert customer info");
                }
                return result;
            });
        }

        /// <summary>
        /// Duplicate MST alone is allowed. Block only when TAX_CD + name + address all match an existing customer.
        /// </summary>
        private async Task EnsureCustomerIdentityUniqueAsync(
            string companyCd,
            string? taxCd,
            string? customerName,
            string? address,
            long? excludeCustomerId)
        {
            var taxKey = NormalizeTaxKey(taxCd);
            if (string.IsNullOrEmpty(taxKey))
                return;

            var identityKey = BuildIdentityKey(taxKey, customerName, address);
            var existing = await GetListAsync(companyCd);
            if (existing.Any(item =>
                (!excludeCustomerId.HasValue || item.CUSTOMER_ID != excludeCustomerId.Value)
                && MatchesIdentityKey(item, identityKey)))
            {
                throw new InvalidOperationException(
                    "Customer already exists (same TAX_CD, name and address)");
            }
        }

        private async Task EnsureCustomerIdentitiesUniqueForBulkAsync(
            string companyCd,
            IReadOnlyList<CustomerInfoCustomerExtRequest> records)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                var taxKey = NormalizeTaxKey(record.TAX_CD);
                if (string.IsNullOrEmpty(taxKey))
                    continue;

                var identityKey = BuildIdentityKey(taxKey, record.CUSTOMER_NM_VIET, record.ADDRESS);
                if (!seen.Add(identityKey))
                {
                    throw new InvalidOperationException(
                        "Customer already exists (same TAX_CD, name and address)");
                }
            }

            var existing = await GetListAsync(companyCd);
            var existingKeys = new HashSet<string>(
                existing
                    .Select(item =>
                    {
                        var taxKey = NormalizeTaxKey(item.TAX_CD);
                        return string.IsNullOrEmpty(taxKey)
                            ? null
                            : BuildIdentityKey(taxKey, item.CUSTOMER_NM_VIET, item.ADDRESS);
                    })
                    .Where(key => key != null)
                    .Cast<string>(),
                StringComparer.Ordinal);

            foreach (var record in records)
            {
                var taxKey = NormalizeTaxKey(record.TAX_CD);
                if (string.IsNullOrEmpty(taxKey))
                    continue;

                if (existingKeys.Contains(BuildIdentityKey(taxKey, record.CUSTOMER_NM_VIET, record.ADDRESS)))
                {
                    throw new InvalidOperationException(
                        "Customer already exists (same TAX_CD, name and address)");
                }
            }
        }

        private static bool MatchesIdentityKey(CustomerInfoCustomerExt item, string identityKey)
        {
            var taxKey = NormalizeTaxKey(item.TAX_CD);
            if (string.IsNullOrEmpty(taxKey))
                return false;
            return string.Equals(
                BuildIdentityKey(taxKey, item.CUSTOMER_NM_VIET, item.ADDRESS),
                identityKey,
                StringComparison.Ordinal);
        }

        private static string BuildIdentityKey(string taxKey, string? customerName, string? address)
            => $"{taxKey}\u001f{NormalizeTextKey(customerName)}\u001f{NormalizeTextKey(address)}";

        private static string NormalizeTaxKey(string? taxCd)
        {
            var text = Common.NormalizeNullableText(taxCd);
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var digits = new string(text.Where(char.IsDigit).ToArray());
            return digits;
        }

        private static string NormalizeTextKey(string? value)
            => (Common.NormalizeNullableText(value) ?? string.Empty).ToUpperInvariant();

        private async Task<string> ResolveCustomerCodeAsync(
            DapperSession session,
            string companyCd,
            CustomerInfoCustomerExt? existing,
            string? requestedCode,
            bool isInsert)
        {
            var baseDate = DateTime.Now;
            var normalizedRequest = Common.NormalizeNullableText(requestedCode);
            if (isInsert)
            {
                var nextCd = await _writeSupport.ResolveUpsertCodeByContextAsync(
                    session,
                    companyCd,
                    SequenceMenuCode,
                    SequenceCodeField,
                    null,
                    normalizedRequest,
                    "CUSTOMER_CD",
                    baseDate);

                for (var attempt = 0; attempt < 20; attempt++)
                {
                    if (string.IsNullOrWhiteSpace(nextCd) ||
                        !await _repository.CustomerCdExistsAsync(companyCd, nextCd, null))
                    {
                        return nextCd ?? string.Empty;
                    }

                    nextCd = await _writeSupport.ResolveUpsertCodeByContextAsync(
                        session,
                        companyCd,
                        SequenceMenuCode,
                        SequenceCodeField,
                        null,
                        null,
                        "CUSTOMER_CD",
                        baseDate);
                }

                throw new InvalidOperationException("Unable to allocate a unique CUSTOMER_CD");
            }
            var existingCd = existing?.CUSTOMER_CD;
            if (string.IsNullOrWhiteSpace(normalizedRequest) ||
                string.Equals(normalizedRequest, existingCd, StringComparison.OrdinalIgnoreCase))
            {
                return existingCd ?? string.Empty;
            }
            var resolvedCd = await _writeSupport.ResolveUpsertCodeByContextAsync(
                session,
                companyCd,
                SequenceMenuCode,
                SequenceCodeField,
                existingCd,
                normalizedRequest,
                "CUSTOMER_CD",
                baseDate);
            if (!string.Equals(resolvedCd, normalizedRequest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Requested customer code does not match the current sequence configuration.");
            return resolvedCd;
        }
    }
}
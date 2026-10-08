using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services
{
    public class CompanySignatureInfoService : ICompanySignatureInfoService
    {
        private const string CacheScope = "company-signature-info";
        private const string ReportConfigCacheScope = "company-report-configuration";

        private readonly ICompanySignatureInfoRepository _repository;
        private readonly IMasterDataCacheService _cacheService;
        private readonly ILogger<CompanySignatureInfoService> _logger;

        public CompanySignatureInfoService(
            ICompanySignatureInfoRepository repository,
            IMasterDataCacheService cacheService,
            ILogger<CompanySignatureInfoService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<CompanySignatureInfo>> GetCompanySignatureInfosAsync(
            string companyCd,
            long? id = null,
            string? signCode = null,
            string? isActive = null)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var normalizedSignCode = Common.NormalizeNullableText(signCode);
            var normalizedIsActive = Common.NormalizeNullableText(isActive);
            var cacheKey = BuildQueryCacheKey(id, normalizedSignCode, normalizedIsActive);

            var items = await _cacheService.GetOrCreateAsync(
                CacheScope,
                normalizedCompanyCd,
                cacheKey,
                async () =>
                {
                    var rows = await _repository.QueryCompanySignatureInfosAsync(
                        normalizedCompanyCd,
                        id,
                        normalizedSignCode,
                        normalizedIsActive);
                    return rows.ToList();
                });

            _logger.LogDebug(
                "Loaded company signatures for {CompanyCd} with cache key {CacheKey}. Count={Count}.",
                normalizedCompanyCd,
                cacheKey,
                items.Count);

            return items;
        }

        public async Task<CompanySignatureInfo?> SetCompanySignatureInfoAsync(
            string companyCd,
            string userId,
            CompanySignatureInfoRequest request)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var saved = await _repository.SetCompanySignatureInfoAsync(normalizedCompanyCd, userId, request);
            if (saved != null)
            {
                await ClearRelatedCachesAsync(normalizedCompanyCd);
            }

            return saved;
        }

        public async Task<int> DeleteCompanySignatureInfoAsync(string companyCd, string userId, List<long> signatureIds)
        {
            if (signatureIds == null || signatureIds.Count == 0)
                throw new ArgumentException("signatureIds is required");

            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var result = await _repository.DeleteCompanySignatureInfoAsync(normalizedCompanyCd, userId, signatureIds.Distinct().ToList());
            if (result > 0)
            {
                await ClearRelatedCachesAsync(normalizedCompanyCd);
            }

            return result;
        }

        public Task ClearCacheAsync(string companyCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            return _cacheService.ClearAsync(CacheScope, normalizedCompanyCd);
        }

        private async Task ClearRelatedCachesAsync(string companyCd)
        {
            await _cacheService.ClearAsync(CacheScope, companyCd);
            await _cacheService.ClearAsync(ReportConfigCacheScope, companyCd);
        }

        private static string NormalizeCompanyCd(string companyCd)
        {
            return Common.NormalizeRequiredText(companyCd);
        }

        private static string BuildQueryCacheKey(long? id, string? signCode, string? isActive)
        {
            var idPart = id.HasValue ? id.Value.ToString() : "null";
            var signCodePart = signCode ?? "null";
            var isActivePart = isActive ?? "null";
            return $"id={idPart};signCode={signCodePart};isActive={isActivePart}";
        }

    }
}

using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Dapper;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Repositories
{
    public class CompanyInfoRepository : ICompanyInfoRepository
    {
        private const string CacheScope = "company-info";
        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;
        private readonly IMasterDataCacheService _cacheService;

        public CompanyInfoRepository(DapperExecutor db, IActivityLogService activityLogService, IMasterDataCacheService cacheService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        public async Task<CompanyInfo?> GetCompanyInfoAsync(string companyCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            return await _cacheService.GetOrCreateAsync<CompanyInfo?>(
                CacheScope,
                normalizedCompanyCd,
                "profile",
                async () =>
                {
                    const string query = "CALL getcompany_info(@p_COMPANY_CD)";
                    var items = await _db.QueryAsync<CompanyInfo>(Net_DB.Net_DB_Manager, query, new
                    {
                        p_COMPANY_CD = normalizedCompanyCd
                    });

                    return items.FirstOrDefault();
                });
        }

        public async Task<int> UpsertCompanyInfoAsync(string companyCd, CompanyInfoRequest request, CompanyInfo? existing = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var currentData = existing ?? await GetCompanyInfoAsync(normalizedCompanyCd);
            var oldData = currentData == null ? null : JsonSerializer.Serialize(currentData);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                const string query = @"CALL setcompany_info(
                    @p_COMPANY_CD,
                    @p_DB_GROUP_ID,
                    @p_COMPANY_NM,
                    @p_COMPANY_NM_EN,
                    @p_COMPANY_NM_KOR,
                    @p_COMPANY_TYPE,
                    @p_COMPANY_KIND,
                    @p_DE_COMPANY_CD,
                    @p_COMPANY_LV,
                    @p_ACCDATE_CD,
                    @p_TAX_CD,
                    @p_CCCDAN,
                    @p_TCQTQLY,
                    @p_MCQTQLY,
                    @p_BRN,
                    @p_CRN,
                    @p_OWNER_NM,
                    @p_ZIP_CODE,
                    @p_ADDRESS_DO,
                    @p_ADDRESS,
                    @p_ADDRESS_ENG,
                    @p_ADDRESS_KOR,
                    @p_CARRYFORWARD_YMD,
                    @p_SIDO,
                    @p_GUMYUN,
                    @p_BUSINESS_TYPE,
                    @p_KIND_BUSINESS,
                    @p_TEL,
                    @p_EMAIL,
                    @p_WEBSITE,
                    @p_FAX,
                    @p_STOCKCALC_TYPE,
                    @p_OPEN_YMD,
                    @p_DECISION,
                    @p_REG_YMD,
                    @p_ISDEL,
                    @p_NOTE
                )";

                var result = await session.ExecuteAsync(query, new
                {
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_DB_GROUP_ID = request.DB_GROUP_ID,
                    p_COMPANY_NM = request.COMPANY_NM,
                    p_COMPANY_NM_EN = request.COMPANY_NM_EN,
                    p_COMPANY_NM_KOR = request.COMPANY_NM_KOR,
                    p_COMPANY_TYPE = request.COMPANY_TYPE,
                    p_COMPANY_KIND = request.COMPANY_KIND,
                    p_DE_COMPANY_CD = request.DE_COMPANY_CD,
                    p_COMPANY_LV = request.COMPANY_LV,
                    p_ACCDATE_CD = request.ACCDATE_CD,
                    p_TAX_CD = request.TAX_CD,
                    p_CCCDAN = request.CCCDan,
                    p_TCQTQLY = request.TCQTQLy,
                    p_MCQTQLY = request.MCQTQLy,
                    p_BRN = request.BRN,
                    p_CRN = request.CRN,
                    p_OWNER_NM = request.OWNER_NM,
                    p_ZIP_CODE = request.ZIP_CODE,
                    p_ADDRESS_DO = request.ADDRESS_DO,
                    p_ADDRESS = request.ADDRESS,
                    p_ADDRESS_ENG = request.ADDRESS_ENG,
                    p_ADDRESS_KOR = request.ADDRESS_KOR,
                    p_CARRYFORWARD_YMD = request.CARRYFORWARD_YMD,
                    p_SIDO = request.SIDO,
                    p_GUMYUN = request.GUMYUN,
                    p_BUSINESS_TYPE = request.BUSINESS_TYPE,
                    p_KIND_BUSINESS = request.KIND_BUSINESS,
                    p_TEL = request.TEL,
                    p_EMAIL = request.EMAIL,
                    p_WEBSITE = request.WEBSITE,
                    p_FAX = request.FAX,
                    p_STOCKCALC_TYPE = request.STOCKCALC_TYPE,
                    p_OPEN_YMD = request.OPEN_YMD,
                    p_DECISION = request.DECISION,
                    p_REG_YMD = request.REG_YMD,
                    p_ISDEL = request.ISDEL,
                    p_NOTE = request.NOTE
                });

                if (result >= 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        normalizedCompanyCd,
                        currentData == null ? "INSERT" : "UPDATE",
                        "CompanyInfo",
                        "company_info",
                        normalizedCompanyCd,
                        oldData ?? string.Empty,
                        JsonSerializer.Serialize(request),
                        "Upsert company_info");
                }

                session.Commit();
                await _cacheService.ClearAsync(CacheScope, normalizedCompanyCd);
                return result;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static string NormalizeCompanyCd(string? companyCd)
        {
            return string.IsNullOrWhiteSpace(companyCd) ? string.Empty : companyCd.Trim();
        }
    }
}

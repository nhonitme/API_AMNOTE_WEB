using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public class CompanySignatureInfoRepository : ICompanySignatureInfoRepository
    {
        private const string GetCompanySignatureInfoQuery = "CALL getCompanySignatureInfo(@p_COMPANY_CD, @p_ID, @p_SIGN_CODE, @p_IS_ACTIVE)";

        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;
        private static readonly HashSet<string> FixedSignCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "01",
            "02",
            "03",
            "04",
            "05",
            "06"
        };

        public CompanySignatureInfoRepository(
            DapperExecutor db,
            IActivityLogService activityLogService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
        }

        public Task<IEnumerable<CompanySignatureInfo>> QueryCompanySignatureInfosAsync(
            string companyCd,
            long? id = null,
            string? signCode = null,
            string? isActive = null)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            return _db.QueryAsync<CompanySignatureInfo>(Net_DB.Net_DB_Manager, GetCompanySignatureInfoQuery, new
            {
                p_COMPANY_CD = normalizedCompanyCd,
                p_ID = id,
                p_SIGN_CODE = Common.NormalizeNullableText(signCode),
                p_IS_ACTIVE = Common.NormalizeNullableText(isActive)
            });
        }

        public async Task<CompanySignatureInfo?> SetCompanySignatureInfoAsync(string companyCd, string userId, CompanySignatureInfoRequest request)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var normalizedRequest = NormalizeRequest(request);
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                var existing = normalizedRequest.ID.HasValue && normalizedRequest.ID.Value > 0
                    ? (await session.QueryAsync<CompanySignatureInfo>(
                        GetCompanySignatureInfoQuery,
                        new
                        {
                            p_COMPANY_CD = normalizedCompanyCd,
                            p_ID = normalizedRequest.ID,
                            p_SIGN_CODE = (string?)null,
                            p_IS_ACTIVE = (string?)null
                        })).FirstOrDefault()
                    : null;

                var oldData = existing == null ? string.Empty : JsonSerializer.Serialize(existing);
                var targetId = await session.QueryFirstOrDefaultAsync<long?>(
                    "CALL setCompanySignatureInfo(@p_ID, @p_COMPANY_CD, @p_SIGN_CODE, @p_DISPLAY_LABEL, @p_SIGN_NAME, @p_SIGN_TITLE, @p_SIGN_IMAGE_URL, @p_SORT_ORDER, @p_IS_ACTIVE, @p_ISDEL, @p_USERID)",
                    new
                    {
                        p_ID = normalizedRequest.ID ?? 0,
                        p_COMPANY_CD = normalizedCompanyCd,
                        p_SIGN_CODE = normalizedRequest.SIGN_CODE,
                        p_DISPLAY_LABEL = normalizedRequest.DISPLAY_LABEL,
                        p_SIGN_NAME = normalizedRequest.SIGN_NAME,
                        p_SIGN_TITLE = normalizedRequest.SIGN_TITLE,
                        p_SIGN_IMAGE_URL = normalizedRequest.SIGN_IMAGE_URL,
                        p_SORT_ORDER = normalizedRequest.SORT_ORDER ?? 0,
                        p_IS_ACTIVE = normalizedRequest.IS_ACTIVE,
                        p_ISDEL = normalizedRequest.ISDEL,
                        p_USERID = userId
                    }) ?? 0L;

                // Fallback when procedure dump has not yet been migrated to return ID.
                if (targetId <= 0 && normalizedRequest.ID.HasValue && normalizedRequest.ID.Value > 0)
                {
                    targetId = normalizedRequest.ID.Value;
                }

                if (targetId <= 0)
                {
                    session.Rollback();
                    return null;
                }

                var saved = (await session.QueryAsync<CompanySignatureInfo>(
                    GetCompanySignatureInfoQuery,
                    new
                    {
                        p_COMPANY_CD = normalizedCompanyCd,
                        p_ID = targetId,
                        p_SIGN_CODE = (string?)null,
                        p_IS_ACTIVE = (string?)null
                    })).FirstOrDefault();

                var operation = existing == null || !string.Equals(existing.COMPANY_CD, normalizedCompanyCd, StringComparison.OrdinalIgnoreCase)
                    ? "INSERT"
                    : "UPDATE";

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    normalizedCompanyCd,
                    operation,
                    "CompanySignatureInfo",
                    "company_signature_info",
                    saved?.SIGN_CODE ?? existing?.SIGN_CODE ?? string.Empty,
                    oldData,
                    saved == null ? string.Empty : JsonSerializer.Serialize(saved),
                    "Upsert company_signature_info");

                session.Commit();
                return saved;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<int> DeleteCompanySignatureInfoAsync(string companyCd, string userId, List<long> signatureIds)
        {
            if (signatureIds == null || signatureIds.Count == 0)
            {
                return 0;
            }

            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                const string deleteQuery = "CALL delCompanySignatureInfo(@p_COMPANY_CD, @p_ID, @p_USERID)";
                var affectedRows = 0;

                foreach (var id in signatureIds.Distinct())
                {
                    var existing = (await session.QueryAsync<CompanySignatureInfo>(
                        GetCompanySignatureInfoQuery,
                        new
                        {
                            p_COMPANY_CD = normalizedCompanyCd,
                            p_ID = id,
                            p_SIGN_CODE = (string?)null,
                            p_IS_ACTIVE = (string?)null
                        })).FirstOrDefault();

                    if (existing == null)
                    {
                        continue;
                    }

                    if (FixedSignCodes.Contains(existing.SIGN_CODE))
                    {
                        throw new InvalidOperationException($"SIGN_CODE {existing.SIGN_CODE} is fixed and cannot be deleted");
                    }

                    if (!string.Equals(existing.COMPANY_CD, normalizedCompanyCd, StringComparison.OrdinalIgnoreCase))
                    {
                        // Deleting an inherited signature creates a disabled company override.
                        // This hides it for this company without changing the shared default.
                        var disabledId = await session.QueryFirstOrDefaultAsync<long?>(
                            "CALL setCompanySignatureInfo(@p_ID, @p_COMPANY_CD, @p_SIGN_CODE, @p_DISPLAY_LABEL, @p_SIGN_NAME, @p_SIGN_TITLE, @p_SIGN_IMAGE_URL, @p_SORT_ORDER, @p_IS_ACTIVE, @p_ISDEL, @p_USERID)",
                            new
                            {
                                p_ID = existing.ID,
                                p_COMPANY_CD = normalizedCompanyCd,
                                p_SIGN_CODE = existing.SIGN_CODE,
                                p_DISPLAY_LABEL = existing.DISPLAY_LABEL,
                                p_SIGN_NAME = existing.SIGN_NAME,
                                p_SIGN_TITLE = existing.SIGN_TITLE,
                                p_SIGN_IMAGE_URL = existing.SIGN_IMAGE_URL,
                                p_SORT_ORDER = existing.SORT_ORDER ?? 0,
                                p_IS_ACTIVE = "0",
                                p_ISDEL = "0",
                                p_USERID = userId
                            }) ?? 0L;

                        if (disabledId > 0)
                        {
                            affectedRows++;
                            await _activityLogService.LogAsync(
                                session.Connection,
                                session.Transaction,
                                normalizedCompanyCd,
                                "INSERT",
                                "CompanySignatureInfo",
                                "company_signature_info",
                                existing.SIGN_CODE,
                                string.Empty,
                                JsonSerializer.Serialize(new { ID = disabledId, existing.SIGN_CODE, IS_ACTIVE = "0" }),
                                "Hide inherited company_signature_info");
                        }
                        continue;
                    }

                    var result = await session.ExecuteAsync(deleteQuery, new
                    {
                        p_COMPANY_CD = normalizedCompanyCd,
                        p_ID = id,
                        p_USERID = userId
                    });

                    if (result > 0)
                    {
                        affectedRows += result;
                        await _activityLogService.LogAsync(
                            session.Connection,
                            session.Transaction,
                            normalizedCompanyCd,
                            "DELETE",
                            "CompanySignatureInfo",
                            "company_signature_info",
                            existing.SIGN_CODE,
                            JsonSerializer.Serialize(existing),
                            string.Empty,
                            "Delete company_signature_info");
                    }
                }

                session.Commit();
                return affectedRows;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        private static string NormalizeCompanyCd(string companyCd)
        {
            return Common.NormalizeRequiredText(companyCd);
        }

        private static CompanySignatureInfoRequest NormalizeRequest(CompanySignatureInfoRequest request)
        {
            return new CompanySignatureInfoRequest
            {
                ID = request.ID,
                SIGN_CODE = Common.NormalizeNullableText(request.SIGN_CODE),
                DISPLAY_LABEL = Common.NormalizeNullableText(request.DISPLAY_LABEL),
                SIGN_NAME = Common.NormalizeRequiredText(request.SIGN_NAME),
                SIGN_TITLE = Common.NormalizeNullableText(request.SIGN_TITLE),
                SIGN_IMAGE_URL = Common.NormalizeNullableText(request.SIGN_IMAGE_URL),
                SORT_ORDER = Common.NormalizeNullablePositiveInt(request.SORT_ORDER) ?? 0,
                IS_ACTIVE = Common.NormalizeFlagString(request.IS_ACTIVE, "1"),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0")
            };
        }
    }
}

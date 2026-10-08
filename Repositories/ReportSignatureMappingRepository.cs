using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reports;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed class ReportSignatureMappingRepository : IReportSignatureMappingRepository
    {
        private readonly DapperExecutor _db;
        private readonly IReportConfigurationRepository _reportConfigurationRepository;
        private readonly ICompanySignatureInfoService _companySignatureInfoService;
        private readonly IActivityLogService _activityLogService;

        public ReportSignatureMappingRepository(
            DapperExecutor db,
            IReportConfigurationRepository reportConfigurationRepository,
            ICompanySignatureInfoService companySignatureInfoService,
            IActivityLogService activityLogService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _reportConfigurationRepository = reportConfigurationRepository ?? throw new ArgumentNullException(nameof(reportConfigurationRepository));
            _companySignatureInfoService = companySignatureInfoService ?? throw new ArgumentNullException(nameof(companySignatureInfoService));
            _activityLogService = activityLogService ?? throw new ArgumentNullException(nameof(activityLogService));
        }

        public async Task<ReportSignatureMappingInfo?> GetReportSignatureMappingAsync(string companyCd, string reportKey, string? reportCode = null)
        {
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var normalizedReportKey = Common.NormalizeRequiredText(reportKey);
            var normalizedReportCode = Common.NormalizeNullableText(reportCode) ?? normalizedReportKey;

            if (normalizedCompanyCd.Length == 0)
            {
                throw new ArgumentException("COMPANY_CD is required.", nameof(companyCd));
            }

            if (normalizedReportKey.Length == 0)
            {
                throw new ArgumentException("REPORT_KEY is required.", nameof(reportKey));
            }

            var config = await _reportConfigurationRepository.GetCompanyReportConfigurationAsync(
                normalizedCompanyCd,
                normalizedReportCode,
                normalizedReportKey);

            if (config == null)
            {
                return null;
            }

            var signCodeOrder = ParseSignCodes(config.SIGN_IDS);
            var signOrderLookup = signCodeOrder
                .Select((value, index) => new { Value = value, Order = index + 1 })
                .ToDictionary(item => item.Value, item => item.Order, StringComparer.OrdinalIgnoreCase);

            var signatures = (await _companySignatureInfoService.GetCompanySignatureInfosAsync(
                    normalizedCompanyCd,
                    null,
                    null,
                    null))
                .Where(item => !string.Equals(item.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.SORT_ORDER ?? int.MaxValue)
                .ThenBy(item => item.SIGN_CODE, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ID)
                .Select(item => new ReportSignatureMappingSignatureInfo
                {
                    ID = item.ID,
                    COMPANY_CD = item.COMPANY_CD,
                    SIGN_CODE = item.SIGN_CODE,
                    DISPLAY_LABEL = Common.NormalizeNullableText(item.DISPLAY_LABEL),
                    SIGN_NAME = item.SIGN_NAME,
                    SIGN_TITLE = Common.NormalizeNullableText(item.SIGN_TITLE),
                    SIGN_IMAGE_URL = Common.NormalizeNullableText(item.SIGN_IMAGE_URL),
                    SORT_ORDER = item.SORT_ORDER,
                    IS_ACTIVE = Common.NormalizeFlagString(item.IS_ACTIVE, "1"),
                    IS_SELECTED = signOrderLookup.ContainsKey(item.SIGN_CODE),
                    SELECTED_ORDER = signOrderLookup.TryGetValue(item.SIGN_CODE, out var selectedOrder)
                        ? selectedOrder
                        : null
                })
                .ToList();

            return new ReportSignatureMappingInfo
            {
                MAPPING_ID = config.MAPPING_ID,
                COMPANY_CD = config.COMPANY_CD,
                REPORT_KEY = Common.NormalizeNullableText(config.REPORT_KEY) ?? normalizedReportKey,
                REPORT_CODE = Common.NormalizeNullableText(config.REPORT_CODE) ?? normalizedReportCode,
                REPORT_NAME = ReportLanguageHelper.ResolveDisplayLabel(config.LABEL_TEXT, null),
                REPORT_ID = config.REPORT_ID,
                SIGN_IDS = NormalizeSignIds(signCodeOrder),
                SIGNATURES = signatures
            };
        }

        public async Task<ReportSignatureMappingInfo> SaveReportSignatureMappingAsync(
            string companyCd,
            string reportKey,
            string? reportCode,
            IReadOnlyCollection<string> signCodes,
            string userId)
        {
            var current = await GetReportSignatureMappingAsync(companyCd, reportKey, reportCode)
                ?? throw new KeyNotFoundException("Report signature mapping not found.");

            if (current.REPORT_ID <= 0)
            {
                throw new KeyNotFoundException("Report configuration not found.");
            }

            var normalizedSignCodes = NormalizeSignCodes(signCodes);
            if (normalizedSignCodes.Count > 0)
            {
                var availableSignCodes = current.SIGNATURES
                    .Select(item => item.SIGN_CODE)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var unknownSignCodes = normalizedSignCodes
                    .Where(item => !availableSignCodes.Contains(item))
                    .ToList();

                if (unknownSignCodes.Count > 0)
                {
                    throw new ArgumentException($"Unknown SIGN_CODE: {string.Join(", ", unknownSignCodes)}");
                }
            }

            var normalizedSignIds = NormalizeSignIds(normalizedSignCodes);
            if (!current.MAPPING_ID.HasValue && normalizedSignIds == null)
            {
                current.SIGN_IDS = null;
                current.SIGNATURES = ApplySelection(current.SIGNATURES, normalizedSignCodes);
                return current;
            }

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                await session.ExecuteAsync(
                    "CALL set_company_report_mapping(@p_ID, @p_COMPANY_CD, @p_REPORT_KEY, @p_REPORT_ID, @p_SIGN_IDS, @p_IS_DEFAULT, @p_IS_ACTIVE)",
                    new
                    {
                        p_ID = current.MAPPING_ID ?? 0,
                        p_COMPANY_CD = current.COMPANY_CD,
                        p_REPORT_KEY = current.REPORT_KEY,
                        p_REPORT_ID = current.REPORT_ID,
                        p_SIGN_IDS = normalizedSignIds,
                        p_IS_DEFAULT = "1",
                        p_IS_ACTIVE = "1"
                    });

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    current.COMPANY_CD,
                    current.MAPPING_ID.HasValue ? "UPDATE" : "INSERT",
                    "ReportSignatureMapping",
                    "company_report_mapping",
                    current.REPORT_KEY,
                    JsonSerializer.Serialize(new { current.SIGN_IDS }),
                    JsonSerializer.Serialize(new { SIGN_IDS = normalizedSignIds }),
                    "Upsert company_report_mapping signatures");

                session.Commit();
                await _reportConfigurationRepository.ClearCompanyReportConfigurationCacheAsync(current.COMPANY_CD);
            }
            catch
            {
                session.Rollback();
                throw;
            }

            return await GetReportSignatureMappingAsync(companyCd, reportKey, reportCode)
                ?? throw new KeyNotFoundException("Report signature mapping not found after save.");
        }

        private static List<ReportSignatureMappingSignatureInfo> ApplySelection(
            IEnumerable<ReportSignatureMappingSignatureInfo> signatures,
            IReadOnlyList<string> signCodes)
        {
            var selectedLookup = signCodes
                .Select((value, index) => new { Value = value, Order = index + 1 })
                .ToDictionary(item => item.Value, item => item.Order, StringComparer.OrdinalIgnoreCase);

            return signatures
                .Select(item => new ReportSignatureMappingSignatureInfo
                {
                    ID = item.ID,
                    COMPANY_CD = item.COMPANY_CD,
                    SIGN_CODE = item.SIGN_CODE,
                    DISPLAY_LABEL = item.DISPLAY_LABEL,
                    SIGN_NAME = item.SIGN_NAME,
                    SIGN_TITLE = item.SIGN_TITLE,
                    SIGN_IMAGE_URL = item.SIGN_IMAGE_URL,
                    SORT_ORDER = item.SORT_ORDER,
                    IS_ACTIVE = item.IS_ACTIVE,
                    IS_SELECTED = selectedLookup.ContainsKey(item.SIGN_CODE),
                    SELECTED_ORDER = selectedLookup.TryGetValue(item.SIGN_CODE, out var selectedOrder)
                        ? selectedOrder
                        : null
                })
                .ToList();
        }

        private static List<string> ParseSignCodes(string? signIds)
        {
            return NormalizeSignCodes((signIds ?? string.Empty)
                .Split(['|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static List<string> NormalizeSignCodes(IEnumerable<string>? signCodes)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var signCode in signCodes ?? Array.Empty<string>())
            {
                var normalized = Common.NormalizeNullableText(signCode);
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
                {
                    continue;
                }

                results.Add(normalized);
            }

            return results;
        }

        private static string? NormalizeSignIds(IReadOnlyCollection<string> signCodes)
        {
            return signCodes.Count == 0 ? null : string.Join("|", signCodes);
        }
    }
}

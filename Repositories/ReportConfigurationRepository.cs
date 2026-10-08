using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reports;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    public class ReportConfigurationRepository : IReportConfigurationRepository
    {
        private const string CacheScope = "company-report-configuration";
        private const string CacheKey = "all";
        private const string GetAllConfigurationQuery = "CALL getall_company_report_config(@p_COMPANY_CD)";
        private const string GetAllElementsQuery = "CALL getall_company_report_elements(@p_COMPANY_CD)";
        private const string GetAllColumnLayoutQuery = "CALL getall_sys_report_column_layout(@p_COMPANY_CD)";

        private readonly DapperExecutor _db;
        private readonly IMasterDataCacheService _cacheService;

        public ReportConfigurationRepository(DapperExecutor db, IMasterDataCacheService cacheService)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        }

        public async Task<ReportConfigurationInfo?> GetCompanyReportConfigurationAsync(string companyCd, string? reportCode = null, string? menuCode = null)
        {
            var normalizedCompanyCd = NormalizeRequired(companyCd, nameof(companyCd));
            var normalizedReportCode = Common.NormalizeNullableText(reportCode);
            var normalizedMenuCode = Common.NormalizeNullableText(menuCode);
            var data = await QueryAllReportConfigurationAsync(normalizedCompanyCd);
            var resolved = ResolveReportConfiguration(
                data.Configurations,
                normalizedReportCode,
                normalizedMenuCode);

            return resolved;
        }

        public async Task<IEnumerable<CompanyReportElementInfo>> GetCompanyReportElementsAsync(string companyCd, string reportKey, string? reportCode = null)
        {
            var normalizedCompanyCd = NormalizeRequired(companyCd, nameof(companyCd));
            var normalizedReportKey = NormalizeRequired(reportKey, nameof(reportKey));
            var normalizedReportCode = Common.NormalizeNullableText(reportCode);
            var data = await QueryAllReportConfigurationAsync(normalizedCompanyCd);

            return ResolveReportElements(
                data.Elements,
                normalizedCompanyCd,
                normalizedReportKey,
                normalizedReportCode);
        }

        public async Task<IEnumerable<ReportColumnLayoutInfo>> GetReportColumnLayoutAsync(string companyCd, string reportKey, string? reportCode = null)
        {
            var normalizedCompanyCd = NormalizeRequired(companyCd, nameof(companyCd));
            var normalizedReportKey = Common.NormalizeNullableText(reportKey);
            var normalizedReportCode = Common.NormalizeNullableText(reportCode);

            if (normalizedReportKey == null && normalizedReportCode == null)
            {
                throw new ArgumentException("reportKey or reportCode is required.", nameof(reportKey));
            }

            var data = await QueryAllReportConfigurationAsync(normalizedCompanyCd);

            return ResolveReportColumnLayouts(
                data.ColumnLayouts,
                normalizedCompanyCd,
                normalizedReportKey,
                normalizedReportCode);
        }

        public async Task ClearCompanyReportConfigurationCacheAsync(string companyCd)
        {
            var normalizedCompanyCd = NormalizeRequired(companyCd, nameof(companyCd));
            await _cacheService.ClearAsync(CacheScope, normalizedCompanyCd);
        }

        private async Task<ReportConfigurationCacheData> QueryAllReportConfigurationAsync(string companyCd)
        {
            return await _cacheService.GetOrCreateAsync(
                CacheScope,
                companyCd,
                CacheKey,
                () => QueryAllReportConfigurationFromDatabaseAsync(companyCd));
        }

        private async Task<ReportConfigurationCacheData> QueryAllReportConfigurationFromDatabaseAsync(string companyCd)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            List<ReportConfigurationInfo> configurations;
            List<ReportConfigurationSignatureRecord> signatures;

            using (var grid = await session.QueryMultipleAsync(GetAllConfigurationQuery, new
            {
                p_COMPANY_CD = companyCd
            }))
            {
                configurations = (await grid.ReadAsync<ReportConfigurationInfo>()).ToList();
                signatures = grid.IsConsumed
                    ? new List<ReportConfigurationSignatureRecord>()
                    : (await grid.ReadAsync<ReportConfigurationSignatureRecord>()).ToList();
            }

            var elements = (await session.QueryAsync<CompanyReportElementInfo>(GetAllElementsQuery, new
            {
                p_COMPANY_CD = companyCd
            })).ToList();
            var columnLayouts = (await session.QueryAsync<ReportColumnLayoutCacheRecord>(GetAllColumnLayoutQuery, new
            {
                p_COMPANY_CD = companyCd
            })).ToList();

            return new ReportConfigurationCacheData(
                NormalizeConfigurations(configurations, signatures),
                NormalizeElements(elements),
                NormalizeColumnLayouts(columnLayouts));
        }

        private static IReadOnlyList<ReportConfigurationInfo> NormalizeConfigurations(
            IEnumerable<ReportConfigurationInfo>? configurations,
            IEnumerable<ReportConfigurationSignatureRecord>? signatures)
        {
            var signaturesByMappingId = (signatures ?? Enumerable.Empty<ReportConfigurationSignatureRecord>())
                .Where(item => item.MAPPING_ID.HasValue && item.ID > 0 && !string.IsNullOrWhiteSpace(item.SIGN_CODE))
                .GroupBy(item => item.MAPPING_ID!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(item => item.SIGN_SEQUENCE ?? int.MaxValue)
                        .ThenBy(item => item.SORT_ORDER ?? int.MaxValue)
                        .ThenBy(item => item.SIGN_CODE, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(item => item.ID)
                        .Select(MapSignature)
                        .ToList());

            return (configurations ?? Enumerable.Empty<ReportConfigurationInfo>())
                .Where(item => item.REPORT_ID > 0 && !string.IsNullOrWhiteSpace(item.REPORT_CODE))
                .Select(item =>
                {
                    var config = CloneConfiguration(item);
                    config.COMPANY_CD = Common.NormalizeRequiredText(config.COMPANY_CD);
                    config.REPORT_KEY = Common.NormalizeRequiredText(config.REPORT_KEY);
                    config.REPORT_CODE = Common.NormalizeRequiredText(config.REPORT_CODE);
                    config.LABEL_TEXT = Common.NormalizeNullableText(config.LABEL_TEXT);
                    config.CAPTION = Common.NormalizeNullableText(config.CAPTION);
                    config.REPORT_TYPE = Common.NormalizeRequiredText(config.REPORT_TYPE);
                    config.REPORT_SOURCE = Common.NormalizeRequiredText(config.REPORT_SOURCE);
                    config.SIGN_IDS = Common.NormalizeNullableText(config.SIGN_IDS);
                    config.DATA_SOURCE_TYPE = Common.NormalizeNullableText(config.DATA_SOURCE_TYPE);
                    config.DATA_SOURCE_REF = Common.NormalizeNullableText(config.DATA_SOURCE_REF);
                    config.DATA_SET_NAME = Common.NormalizeNullableText(config.DATA_SET_NAME);
                    config.PARAM_MODE = Common.NormalizeNullableText(config.PARAM_MODE);
                    config.IS_DEFAULT = Common.NormalizeFlagString(config.IS_DEFAULT, "1");
                    config.MAPPING_IS_ACTIVE = Common.NormalizeFlagString(config.MAPPING_IS_ACTIVE, "1");
                    config.REPORT_IS_ACTIVE = Common.NormalizeFlagString(config.REPORT_IS_ACTIVE, "1");
                    config.PAGE_ORIENTATION = Common.NormalizeNullableText(config.PAGE_ORIENTATION) ?? "AUTO";
                    config.PAPER_KIND = Common.NormalizeNullableText(config.PAPER_KIND) ?? "AUTO";
                    config.FONT_FAMILY = Common.NormalizeNullableText(config.FONT_FAMILY) ?? "Arial";
                    config.SIGNATURES = config.MAPPING_ID.HasValue && signaturesByMappingId.TryGetValue(config.MAPPING_ID.Value, out var mappedSignatures)
                        ? mappedSignatures.Select(CloneSignature).ToList()
                        : new List<CompanyReportSignatureInfo>();
                    config.ELEMENTS = new List<CompanyReportElementInfo>();

                    return config;
                })
                .OrderBy(item => item.REPORT_CODE, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.MAPPING_ID.HasValue ? 0 : 1)
                .ThenBy(item => item.REPORT_KEY, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.MAPPING_ID ?? 0)
                .ToList();
        }

        private static IReadOnlyList<CompanyReportElementInfo> NormalizeElements(IEnumerable<CompanyReportElementInfo>? elements)
        {
            return (elements ?? Enumerable.Empty<CompanyReportElementInfo>())
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.REPORT_KEY) &&
                    !string.Equals(Common.NormalizeFlagString(item.ISDEL, "0"), "1", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Common.NormalizeFlagString(item.IS_VISIBLE, "1"), "1", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(Common.NormalizeRequiredText(item.ELEMENT_TYPE), "SIGNATURE", StringComparison.OrdinalIgnoreCase))
                .Select(item =>
                {
                    var element = CloneElement(item);
                    element.COMPANY_CD = Common.NormalizeRequiredText(element.COMPANY_CD);
                    element.REPORT_KEY = Common.NormalizeRequiredText(element.REPORT_KEY);
                    element.SECTION_TYPE = Common.NormalizeRequiredText(element.SECTION_TYPE);
                    element.ELEMENT_TYPE = Common.NormalizeRequiredText(element.ELEMENT_TYPE);
                    element.AREA_CODE = Common.NormalizeNullableText(element.AREA_CODE) ?? "LEFT";
                    element.ITEM_KEY = Common.NormalizeRequiredText(element.ITEM_KEY);
                    element.LABEL_TEXT = Common.NormalizeNullableText(element.LABEL_TEXT);
                    element.CAPTION = Common.NormalizeNullableText(element.CAPTION);
                    element.VALUE_SOURCE = Common.NormalizeNullableText(element.VALUE_SOURCE) ?? "FIXED_TEXT";
                    element.VALUE_FIELD = Common.NormalizeNullableText(element.VALUE_FIELD);
                    element.ALIGN_HEADER = Common.NormalizeNullableText(element.ALIGN_HEADER) ?? "CENTER";
                    element.ALIGN_DATA = Common.NormalizeNullableText(element.ALIGN_DATA) ?? "LEFT";
                    element.ALIGN = Common.NormalizeNullableText(element.ALIGN) ?? "LEFT";
                    element.DATA_TYPE = Common.NormalizeNullableText(element.DATA_TYPE) ?? "TEXT";
                    element.FORMAT_STRING = Common.NormalizeNullableText(element.FORMAT_STRING);
                    element.IS_SUMMARY = Common.NormalizeFlagString(element.IS_SUMMARY, "0");
                    element.SUMMARY_TYPE = Common.NormalizeNullableText(element.SUMMARY_TYPE);
                    element.IS_BOLD = Common.NormalizeFlagString(element.IS_BOLD, "0");
                    element.IS_ITALIC = Common.NormalizeFlagString(element.IS_ITALIC, "0");
                    element.IS_VISIBLE = Common.NormalizeFlagString(element.IS_VISIBLE, "1");
                    element.ISDEL = Common.NormalizeFlagString(element.ISDEL, "0");

                    return element;
                })
                .OrderBy(item => item.REPORT_KEY, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SECTION_TYPE, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ROW_NO)
                .ThenBy(item => item.COL_NO)
                .ThenBy(item => item.SORT_ORDER)
                .ThenBy(item => item.ID)
                .ToList();
        }

        private static ReportConfigurationInfo? ResolveReportConfiguration(
            IEnumerable<ReportConfigurationInfo> configurations,
            string? reportCode,
            string? menuCode)
        {
            var mapped = configurations
                .Where(item => item.MAPPING_ID.HasValue)
                .Select(item => new
                {
                    Item = item,
                    Priority = GetReportConfigurationPriority(item, reportCode, menuCode)
                })
                .Where(item => item.Priority < 99)
                .OrderBy(item => item.Priority)
                .ThenBy(item => string.Equals(item.Item.IS_DEFAULT, "1", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenByDescending(item => item.Item.MAPPING_ID ?? 0)
                .Select(item => item.Item)
                .FirstOrDefault();

            if (mapped != null)
            {
                return CloneConfiguration(mapped);
            }

            if (reportCode == null)
            {
                return null;
            }

            var catalog = configurations
                .Where(item => !item.MAPPING_ID.HasValue && SameText(item.REPORT_CODE, reportCode))
                .OrderByDescending(item => item.REPORT_ID)
                .FirstOrDefault();

            if (catalog == null)
            {
                return null;
            }

            var resolved = CloneConfiguration(catalog);
            resolved.REPORT_KEY = menuCode ?? reportCode;

            return resolved;
        }

        private static IReadOnlyList<ReportColumnLayoutCacheRecord> NormalizeColumnLayouts(IEnumerable<ReportColumnLayoutCacheRecord>? layouts)
        {
            return (layouts ?? Enumerable.Empty<ReportColumnLayoutCacheRecord>())
                .Where(item =>
                    !string.IsNullOrWhiteSpace(item.REPORT_CODE) &&
                    !string.IsNullOrWhiteSpace(item.COLUMN_KEY) &&
                    !string.Equals(Common.NormalizeFlagString(item.ISDEL, "0"), "1", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Common.NormalizeFlagString(item.IS_ACTIVE, "1"), "1", StringComparison.OrdinalIgnoreCase))
                .Select(item => new ReportColumnLayoutCacheRecord
                {
                    ID = item.ID,
                    COMPANY_CD = Common.NormalizeRequiredText(item.COMPANY_CD),
                    REPORT_CODE = Common.NormalizeRequiredText(item.REPORT_CODE),
                    COLUMN_KEY = Common.NormalizeRequiredText(item.COLUMN_KEY),
                    PARENT_KEY = Common.NormalizeNullableText(item.PARENT_KEY),
                    FIELD_NAME = Common.NormalizeNullableText(item.FIELD_NAME),
                    LABEL_TEXT = Common.NormalizeNullableText(item.LABEL_TEXT),
                    CAPTION = Common.NormalizeRequiredText(item.CAPTION),
                    ROW_INDEX = item.ROW_INDEX,
                    COL_INDEX = item.COL_INDEX,
                    COL_SPAN = item.COL_SPAN <= 0 ? 1 : item.COL_SPAN,
                    ROW_SPAN = item.ROW_SPAN <= 0 ? 1 : item.ROW_SPAN,
                    WIDTH = item.WIDTH <= 0 ? 1m : item.WIDTH,
                    ALIGN = Common.NormalizeNullableText(item.ALIGN) ?? "LEFT",
                    FORMAT_TYPE = Common.NormalizeNullableText(item.FORMAT_TYPE) ?? "TEXT",
                    SORT_ORDER = item.SORT_ORDER,
                    IS_ACTIVE = Common.NormalizeFlagString(item.IS_ACTIVE, "1"),
                    ISDEL = Common.NormalizeFlagString(item.ISDEL, "0")
                })
                .OrderBy(item => item.REPORT_CODE, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SORT_ORDER)
                .ThenBy(item => item.ROW_INDEX)
                .ThenBy(item => item.COL_INDEX)
                .ThenBy(item => item.ID)
                .ToList();
        }

        private static IReadOnlyList<CompanyReportElementInfo> ResolveReportElements(
            IEnumerable<CompanyReportElementInfo> elements,
            string companyCd,
            string reportKey,
            string? reportCode)
        {
            var candidates = elements
                .Select(item => new
                {
                    Item = item,
                    Priority = GetReportElementPriority(item, companyCd, reportKey, reportCode)
                })
                .Where(item => item.Priority < 99)
                .ToList();

            if (candidates.Count == 0)
            {
                return Array.Empty<CompanyReportElementInfo>();
            }

            var bestPriority = candidates.Min(item => item.Priority);

            return candidates
                .Where(item => item.Priority == bestPriority)
                .Select(item => CloneElement(item.Item))
                .OrderBy(item => item.SECTION_TYPE, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ROW_NO)
                .ThenBy(item => item.COL_NO)
                .ThenBy(item => item.SORT_ORDER)
                .ThenBy(item => item.ID)
                .ToList();
        }

        private static IReadOnlyList<ReportColumnLayoutInfo> ResolveReportColumnLayouts(
            IEnumerable<ReportColumnLayoutCacheRecord> layouts,
            string companyCd,
            string? reportKey,
            string? reportCode)
        {
            var candidates = layouts
                .Select(item => new
                {
                    Item = item,
                    Priority = GetReportColumnLayoutPriority(item, companyCd, reportKey, reportCode)
                })
                .Where(item => item.Priority < 99)
                .ToList();

            if (candidates.Count == 0)
            {
                return Array.Empty<ReportColumnLayoutInfo>();
            }

            var bestPriority = candidates.Min(item => item.Priority);

            return candidates
                .Where(item => item.Priority == bestPriority)
                .Select(item => CloneColumnLayout(item.Item))
                .OrderBy(item => item.SORT_ORDER)
                .ThenBy(item => item.ROW_INDEX)
                .ThenBy(item => item.COL_INDEX)
                .ToList();
        }

        private static int GetReportConfigurationPriority(ReportConfigurationInfo config, string? reportCode, string? menuCode)
        {
            // Prefer exact REPORT_CODE when the caller requests a specific report.
            // Shared menu keys (e.g. GL_FS_PL) must not override a different report's store.
            if (reportCode != null && SameText(config.REPORT_CODE, reportCode))
            {
                if (menuCode != null && SameText(config.REPORT_KEY, menuCode))
                {
                    return 0;
                }

                if (SameText(config.REPORT_KEY, reportCode))
                {
                    return 1;
                }

                return 2;
            }

            if (menuCode != null && SameText(config.REPORT_KEY, menuCode))
            {
                // Menu match is only valid when no reportCode was given, or it matches.
                if (reportCode == null || SameText(config.REPORT_CODE, reportCode))
                {
                    return 3;
                }

                return 99;
            }

            if (reportCode != null && SameText(config.REPORT_KEY, reportCode))
            {
                return 4;
            }

            return 99;
        }

        private static int GetReportColumnLayoutPriority(ReportColumnLayoutCacheRecord layout, string companyCd, string? reportKey, string? reportCode)
        {
            if (reportKey != null && SameText(layout.COMPANY_CD, companyCd) && SameText(layout.REPORT_CODE, reportKey))
            {
                return 0;
            }

            if (reportCode != null && SameText(layout.COMPANY_CD, companyCd) && SameText(layout.REPORT_CODE, reportCode))
            {
                return 1;
            }

            if (reportKey != null && string.IsNullOrWhiteSpace(layout.COMPANY_CD) && SameText(layout.REPORT_CODE, reportKey))
            {
                return 2;
            }

            if (reportCode != null && string.IsNullOrWhiteSpace(layout.COMPANY_CD) && SameText(layout.REPORT_CODE, reportCode))
            {
                return 3;
            }

            return 99;
        }

        private static int GetReportElementPriority(CompanyReportElementInfo element, string companyCd, string reportKey, string? reportCode)
        {
            if (SameText(element.COMPANY_CD, companyCd) && SameText(element.REPORT_KEY, reportKey))
            {
                return 0;
            }

            if (reportCode != null && SameText(element.COMPANY_CD, companyCd) && SameText(element.REPORT_KEY, reportCode))
            {
                return 1;
            }

            if (string.IsNullOrWhiteSpace(element.COMPANY_CD) && SameText(element.REPORT_KEY, reportKey))
            {
                return 2;
            }

            if (reportCode != null && string.IsNullOrWhiteSpace(element.COMPANY_CD) && SameText(element.REPORT_KEY, reportCode))
            {
                return 3;
            }

            return 99;
        }

        private static CompanyReportSignatureInfo MapSignature(ReportConfigurationSignatureRecord item)
        {
            return new CompanyReportSignatureInfo
            {
                ID = item.ID,
                COMPANY_CD = Common.NormalizeRequiredText(item.COMPANY_CD),
                SIGN_CODE = Common.NormalizeRequiredText(item.SIGN_CODE),
                DISPLAY_LABEL = Common.NormalizeNullableText(item.DISPLAY_LABEL),
                SIGN_NAME = Common.NormalizeNullableText(item.SIGN_NAME),
                SIGN_TITLE = Common.NormalizeNullableText(item.SIGN_TITLE),
                SIGN_IMAGE_URL = Common.NormalizeNullableText(item.SIGN_IMAGE_URL),
                SORT_ORDER = item.SORT_ORDER,
                SIGN_SEQUENCE = item.SIGN_SEQUENCE
            };
        }

        private static ReportConfigurationInfo CloneConfiguration(ReportConfigurationInfo item)
        {
            return new ReportConfigurationInfo
            {
                MAPPING_ID = item.MAPPING_ID,
                COMPANY_CD = item.COMPANY_CD,
                REPORT_KEY = item.REPORT_KEY,
                REPORT_ID = item.REPORT_ID,
                SIGN_IDS = item.SIGN_IDS,
                REPORT_CODE = item.REPORT_CODE,
                LABEL_TEXT = item.LABEL_TEXT,
                CAPTION = item.CAPTION,
                REPORT_TYPE = item.REPORT_TYPE,
                REPORT_SOURCE = item.REPORT_SOURCE,
                DATA_SOURCE_TYPE = item.DATA_SOURCE_TYPE,
                DATA_SOURCE_REF = item.DATA_SOURCE_REF,
                DATA_SET_NAME = item.DATA_SET_NAME,
                PARAM_MODE = item.PARAM_MODE,
                IS_DEFAULT = item.IS_DEFAULT,
                MAPPING_IS_ACTIVE = item.MAPPING_IS_ACTIVE,
                REPORT_IS_ACTIVE = item.REPORT_IS_ACTIVE,
                PAGE_ORIENTATION = item.PAGE_ORIENTATION,
                PAPER_KIND = item.PAPER_KIND,
                FONT_FAMILY = item.FONT_FAMILY,
                FONT_SIZE = item.FONT_SIZE,
                TITLE_FONT_SIZE = item.TITLE_FONT_SIZE,
                INFO_FONT_SIZE = item.INFO_FONT_SIZE,
                HEADER_FONT_SIZE = item.HEADER_FONT_SIZE,
                DETAIL_FONT_SIZE = item.DETAIL_FONT_SIZE,
                FOOTER_FONT_SIZE = item.FOOTER_FONT_SIZE,
                MARGIN_LEFT = item.MARGIN_LEFT,
                MARGIN_RIGHT = item.MARGIN_RIGHT,
                MARGIN_TOP = item.MARGIN_TOP,
                MARGIN_BOTTOM = item.MARGIN_BOTTOM,
                SIGNATURES = item.SIGNATURES.Select(CloneSignature).ToList(),
                ELEMENTS = item.ELEMENTS.Select(CloneElement).ToList()
            };
        }

        private static CompanyReportSignatureInfo CloneSignature(CompanyReportSignatureInfo item)
        {
            return new CompanyReportSignatureInfo
            {
                ID = item.ID,
                COMPANY_CD = item.COMPANY_CD,
                SIGN_CODE = item.SIGN_CODE,
                DISPLAY_LABEL = item.DISPLAY_LABEL,
                SIGN_NAME = item.SIGN_NAME,
                SIGN_TITLE = item.SIGN_TITLE,
                SIGN_IMAGE_URL = item.SIGN_IMAGE_URL,
                SORT_ORDER = item.SORT_ORDER,
                SIGN_SEQUENCE = item.SIGN_SEQUENCE
            };
        }

        private static CompanyReportElementInfo CloneElement(CompanyReportElementInfo item)
        {
            return new CompanyReportElementInfo
            {
                ID = item.ID,
                COMPANY_CD = item.COMPANY_CD,
                REPORT_KEY = item.REPORT_KEY,
                SECTION_TYPE = item.SECTION_TYPE,
                ELEMENT_TYPE = item.ELEMENT_TYPE,
                AREA_CODE = item.AREA_CODE,
                ITEM_KEY = item.ITEM_KEY,
                LABEL_TEXT = item.LABEL_TEXT,
                CAPTION = item.CAPTION,
                VALUE_SOURCE = item.VALUE_SOURCE,
                VALUE_FIELD = item.VALUE_FIELD,
                ROW_NO = item.ROW_NO,
                COL_NO = item.COL_NO,
                COL_SPAN = item.COL_SPAN,
                SORT_ORDER = item.SORT_ORDER,
                VISIBLE_INDEX = item.VISIBLE_INDEX,
                WIDTH = item.WIDTH,
                WIDTH_PERCENT = item.WIDTH_PERCENT,
                ALIGN_HEADER = item.ALIGN_HEADER,
                ALIGN_DATA = item.ALIGN_DATA,
                ALIGN = item.ALIGN,
                DATA_TYPE = item.DATA_TYPE,
                FORMAT_STRING = item.FORMAT_STRING,
                IS_SUMMARY = item.IS_SUMMARY,
                SUMMARY_TYPE = item.SUMMARY_TYPE,
                FONT_SIZE = item.FONT_SIZE,
                IS_BOLD = item.IS_BOLD,
                IS_ITALIC = item.IS_ITALIC,
                IS_VISIBLE = item.IS_VISIBLE,
                ISDEL = item.ISDEL,
                CREATE_AT = item.CREATE_AT,
                UPDATE_AT = item.UPDATE_AT
            };
        }

        private static ReportColumnLayoutInfo CloneColumnLayout(ReportColumnLayoutInfo item)
        {
            return new ReportColumnLayoutInfo
            {
                COLUMN_KEY = item.COLUMN_KEY,
                PARENT_KEY = item.PARENT_KEY,
                FIELD_NAME = item.FIELD_NAME,
                LABEL_TEXT = item.LABEL_TEXT,
                CAPTION = item.CAPTION,
                ROW_INDEX = item.ROW_INDEX,
                COL_INDEX = item.COL_INDEX,
                COL_SPAN = item.COL_SPAN,
                ROW_SPAN = item.ROW_SPAN,
                WIDTH = item.WIDTH,
                ALIGN = item.ALIGN,
                FORMAT_TYPE = item.FORMAT_TYPE,
                SORT_ORDER = item.SORT_ORDER
            };
        }

        private static bool SameText(string? left, string? right)
        {
            return string.Equals(
                Common.NormalizeRequiredText(left),
                Common.NormalizeRequiredText(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRequired(string? value, string parameterName)
        {
            var normalized = Common.NormalizeRequiredText(value);
            if (normalized.Length == 0)
            {
                throw new ArgumentException($"{parameterName} is required.", parameterName);
            }

            return normalized;
        }

        private sealed record ReportConfigurationCacheData(
            IReadOnlyList<ReportConfigurationInfo> Configurations,
            IReadOnlyList<CompanyReportElementInfo> Elements,
            IReadOnlyList<ReportColumnLayoutCacheRecord> ColumnLayouts);

        private sealed class ReportConfigurationSignatureRecord
        {
            public long? MAPPING_ID { get; set; }
            public long ID { get; set; }
            public string COMPANY_CD { get; set; } = string.Empty;
            public string SIGN_CODE { get; set; } = string.Empty;
            public string? DISPLAY_LABEL { get; set; }
            public string? SIGN_NAME { get; set; }
            public string? SIGN_TITLE { get; set; }
            public string? SIGN_IMAGE_URL { get; set; }
            public int? SORT_ORDER { get; set; }
            public int? SIGN_SEQUENCE { get; set; }
        }

        private sealed class ReportColumnLayoutCacheRecord : ReportColumnLayoutInfo
        {
            public long ID { get; set; }
            public string COMPANY_CD { get; set; } = string.Empty;
            public string REPORT_CODE { get; set; } = string.Empty;
            public string IS_ACTIVE { get; set; } = "1";
            public string ISDEL { get; set; } = "0";
        }
    }
}

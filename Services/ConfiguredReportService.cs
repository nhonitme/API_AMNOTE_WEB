using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Reporting;
using API_AMNOTE_WEB.Reports;
using Dapper;
using DevExpress.XtraReports.UI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Services
{
    public class ConfiguredReportService : IConfiguredReportService
    {
        private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
        private const string PreviewModeFlatReport = "FLAT_REPORT";
        private const string PreviewModeOutlineGrid = "OUTLINE_GRID";
        private static readonly IReadOnlyList<string> OutlinePreviewRequiredFields = new[]
        {
            "__ROW_TYPE",
            "__ROW_LEVEL",
            "__ROW_SORT"
        };

        private static readonly IReadOnlySet<string> VoucherDemoBlankFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CHIT_ID",
            "CHIT_CD",
            "CHIT_NO",
            "CHIT_YMD",
            "HEADER_DATE_TEXT",
            "SIGNATURE_DATE_TEXT",
            "PARTNER_NAME",
            "PARTNER_ADDRESS",
            "REASON",
            "AMOUNT",
            "AMOUNT_TEXT",
            "AMOUNT_IN_WORDS",
            "FC_AMOUNT",
            "FC_TYPE",
            "FC_RATE",
            "EXCHANGE_RATE_TEXT",
            "CONVERTED_AMOUNT_TEXT",
            "DEBIT_ACCOUNT",
            "CREDIT_ACCOUNT",
            "DETAIL_COUNT",
            "ATTACHMENT_TEXT",
            "REFERENCE_TEXT",
            "NOTE",
            "NOTE_TEXT",
            "CREATE_BY",
            "PAYER_INFO",
            "CUSTOMER_NAME",
            "DESCRIPTION"
        };

        private static readonly IReadOnlySet<string> VoucherFormattedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HEADER_DATE_TEXT",
            "SIGNATURE_DATE_TEXT",
            "AMOUNT_TEXT",
            "AMOUNT_IN_WORDS",
            "EXCHANGE_RATE_TEXT",
            "CONVERTED_AMOUNT_TEXT",
            "ATTACHMENT_TEXT",
            "REFERENCE_TEXT",
            "NOTE_TEXT"
        };

        private static readonly IReadOnlySet<string> ReportDateFormattedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "REPORT_PERIOD_KEY",
            "REPORT_PERIOD_TEXT",
            "REPORT_YEAR_TEXT",
            "REPORT_MONTH_TEXT",
            "REPORT_QUARTER_TEXT",
            "REPORT_DATE_RANGE_TEXT",
            "REPORT_AS_OF_DATE_TEXT",
            "REPORT_DATE_TEXT",
            "REPORT_DATE_TYPE"
        };

        private static readonly IReadOnlySet<string> ReportDateItemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "REPORT_PERIOD",
            "VOUCHER_DATE",
            "HEADER_DATE",
            "SIGN_DATE",
            "NONE"
        };

        private readonly IReportConfigurationRepository _reportConfigurationRepository;
        private readonly ICompanyInfoRepository _companyInfoRepository;
        private readonly ICompanySignatureInfoService _companySignatureInfoService;
        private readonly ISysGridColumnSettingRepository _sysGridColumnSettingRepository;
        private readonly ISystemService _systemService;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private readonly IServiceProvider _serviceProvider;
        private readonly DapperExecutor _db;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ConfiguredReportService> _logger;

        /// <summary>DataTable column → sys_code.CODE_TYPE for master-grid PDF (replace in place).</summary>
        private static readonly IReadOnlyDictionary<string, string> MasterGridSysCodeColumns =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CATEGORY_CD"] = "CATEGORY_CD",
                ["CUSTOMER_TYPE"] = "CUSTOMER_TYPE",
            };

        /// <summary>
        /// Display column ← (code column, CODE_TYPE).
        /// SP may seed empty STATUS_TEXT; API fills localized FA_STATUS text from STATUS.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, (string CodeField, string CodeType)> MasterGridSysCodeDisplayColumns =
            new Dictionary<string, (string CodeField, string CodeType)>(StringComparer.OrdinalIgnoreCase)
            {
                ["STATUS_TEXT"] = ("STATUS", "FA_STATUS"),
            };

        /// <summary>
        /// These reports return STATUS + empty STATUS_TEXT. Preview must localize STATUS_TEXT
        /// even when printLayout is omitted (on-screen grid and Excel).
        /// </summary>
        private static readonly HashSet<string> FaStatusTextReports = new(StringComparer.OrdinalIgnoreCase)
        {
            "FA_ASSET_BOOK_REPORT",
            "FA_REPORT_ASSET_BOOK",
            "FA_DEPRECIATION_PERIOD_REPORT",
            "FA_REPORT_DEPRECIATION_PERIOD",
        };

        public ConfiguredReportService(
            IReportConfigurationRepository reportConfigurationRepository,
            ICompanyInfoRepository companyInfoRepository,
            ICompanySignatureInfoService companySignatureInfoService,
            ISysGridColumnSettingRepository sysGridColumnSettingRepository,
            ISystemService systemService,
            ICompanyDatabaseResolver companyDatabaseResolver,
            IServiceProvider serviceProvider,
            DapperExecutor db,
            IWebHostEnvironment environment,
            ILogger<ConfiguredReportService> logger)
        {
            _reportConfigurationRepository = reportConfigurationRepository ?? throw new ArgumentNullException(nameof(reportConfigurationRepository));
            _companyInfoRepository = companyInfoRepository ?? throw new ArgumentNullException(nameof(companyInfoRepository));
            _companySignatureInfoService = companySignatureInfoService ?? throw new ArgumentNullException(nameof(companySignatureInfoService));
            _sysGridColumnSettingRepository = sysGridColumnSettingRepository ?? throw new ArgumentNullException(nameof(sysGridColumnSettingRepository));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _companyDatabaseResolver = companyDatabaseResolver ?? throw new ArgumentNullException(nameof(companyDatabaseResolver));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<XtraReport> BuildReportAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query,
            CancellationToken cancellationToken = default)
        {
            var context = await BuildContextAsync(companyCd, reportCode, menuCode, query);
            var printLayoutMode = ResolvePrintLayoutMode(query);
            var includeTableLayoutMetadata = printLayoutMode != PrintLayoutMode.Grid;
            var dataSource = await ResolveDataSourceAsync(context, includeTableLayoutMetadata, cancellationToken);
            var table = Common.ConvertToDataTable(
                dataSource.Data,
                Common.NormalizeNullableText(context.Config.DATA_SET_NAME) ?? context.Config.REPORT_CODE);

            if (string.Equals(context.ReportCode, "TAX_VAT_REDUCTION_APPENDIX", StringComparison.OrdinalIgnoreCase))
                table = TaxReductionAppendixCalculator.Process(table);

            // DEFAULT_GRID print mirrors sys grid only — do not inject REPORT_* / voucher enrichment columns.
            if (printLayoutMode != PrintLayoutMode.Grid)
            {
                EnrichReportDataTable(table, context);
                RefreshConfiguredTableAvailability(table);
            }

            // Replace sys-code raw values with localized display text (same as grid Lookup).
            await ApplySysCodeDisplayValuesAsync(table, context);

            if (ShouldBlankVoucherData(query))
            {
                BlankVoucherDataTable(table);
            }

            if (printLayoutMode == PrintLayoutMode.Grid)
            {
                await ApplyPreviewGridPrintLayoutAsync(context, table);
            }
            else
            {
                MaterializeBookCoalesceFields(table, context);
            }

            var signatures = await ResolveReportSignaturesAsync(context.CompanyCd, context.Config.SIGNATURES, query);
            var companyContext = ReportCompanyContextFactory.Create(context.CompanyInfo, signatures, context.CompanyCd, context.ReportLanguage);

            if (context.ReportCode.Equals("TAX_VAT_REDUCTION_APPENDIX", StringComparison.OrdinalIgnoreCase)
                && printLayoutMode != PrintLayoutMode.Grid)
                return new TaxReductionAppendixReport(context.Config, table, companyContext, query);

            return new DynamicConfiguredReport(
                context.Config,
                table,
                companyContext,
                context.ReportLanguage,
                _environment,
                context.NamedValues,
                context.NormalizedValues);
        }

        public async Task<ConfiguredReportPreviewDto> BuildPreviewAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query,
            CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveReportTableAsync(companyCd, reportCode, menuCode, query, false, false, cancellationToken);
            var printLayoutMode = ResolvePrintLayoutMode(query);
            IReadOnlyList<ConfiguredReportPreviewColumnDto>? columns = null;
            if (printLayoutMode == PrintLayoutMode.Grid || IsFaStatusTextReport(resolved.Context))
            {
                await ApplySysCodeDisplayValuesAsync(resolved.Table, resolved.Context);
            }

            if (printLayoutMode == PrintLayoutMode.Grid)
            {
                columns = await ResolvePreviewGridColumnsAsync(resolved.Context, resolved.Table);
            }

            return BuildPreviewDto(resolved.Context, resolved.Table, columns);
        }

        public async Task<DataTable> ExecuteReportDataTableInSessionAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query,
            IDbConnection connection,
            IDbTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            var context = await BuildContextAsync(companyCd, reportCode, menuCode, query);
            var dataSourceRef = ResolveDataSourceReference(context.Config, out var dataSourceType);
            if (dataSourceRef == null)
            {
                throw new InvalidOperationException("Report data source is not configured.");
            }

            var dataSourceTypeToken = Common.NormalizeRequiredText(dataSourceType);
            if (!dataSourceTypeToken.Equals("SP", StringComparison.OrdinalIgnoreCase) &&
                !dataSourceTypeToken.Equals("STOREDPROC", StringComparison.OrdinalIgnoreCase) &&
                !dataSourceTypeToken.Equals("STORED_PROCEDURE", StringComparison.OrdinalIgnoreCase) &&
                !dataSourceTypeToken.Equals("SQL", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Report data source type '{dataSourceType}' is not supported for formula draft preview.");
            }

            var (_, sql) = Common.ParseCommandReference(dataSourceRef);
            sql = NormalizeSpCall(dataSourceType, sql);
            var parameters = BuildCommandParameters(sql, context);
            var tableName = Common.NormalizeNullableText(context.Config.DATA_SET_NAME)
                            ?? context.Config.REPORT_CODE;

            cancellationToken.ThrowIfCancellationRequested();

            var sw = Stopwatch.StartNew();
            var command = new CommandDefinition(
                sql,
                parameters,
                transaction,
                cancellationToken: cancellationToken);
            using var reader = await connection.ExecuteReaderAsync(command);
            var dataTable = new DataTable(tableName);
            dataTable.Load(reader);
            sw.Stop();
            Common.LogQuery(sql, parameters, sw.ElapsedMilliseconds);

            return dataTable;
        }

        private async Task<ResolvedReportData> ResolveReportTableAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query,
            bool includeTableLayoutMetadata,
            bool enrichReportData,
            CancellationToken cancellationToken)
        {
            var context = await BuildContextAsync(companyCd, reportCode, menuCode, query);
            var dataSource = await ResolveDataSourceAsync(context, includeTableLayoutMetadata, cancellationToken);
            var rawTable = Common.ConvertToDataTable(dataSource.Data, Common.NormalizeNullableText(context.Config.DATA_SET_NAME) ?? context.Config.REPORT_CODE);
            if (string.Equals(context.ReportCode, "TAX_VAT_REDUCTION_APPENDIX", StringComparison.OrdinalIgnoreCase))
                rawTable = TaxReductionAppendixCalculator.Process(rawTable);

            if (enrichReportData)
            {
                EnrichReportDataTable(rawTable, context);
            }

            if (ShouldBlankVoucherData(query))
            {
                BlankVoucherDataTable(rawTable);
            }

            return new ResolvedReportData(context, rawTable);
        }

        private static PrintLayoutMode ResolvePrintLayoutMode(IReadOnlyDictionary<string, string> query)
        {
            var printLayout = GetQueryValue(query, "printLayout");
            var normalized = Common.NormalizeToken(printLayout);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return PrintLayoutMode.Document;
            }

            return normalized switch
            {
                "defaultgrid" => PrintLayoutMode.Grid,
                "book" => PrintLayoutMode.Document,
                _ => throw new ArgumentException(
                    $"Unsupported printLayout '{printLayout}'. Use DEFAULT_GRID, BOOK, or omit for document print.")
            };
        }

        private static void MaterializeBookCoalesceFields(DataTable table, ExecutionContext context)
        {
            if (table == null)
            {
                return;
            }

            if (!table.ExtendedProperties.Contains(ReportDataTableProperties.TableLayoutMetadata) ||
                table.ExtendedProperties[ReportDataTableProperties.TableLayoutMetadata] is not DataTable metadataTable)
            {
                return;
            }

            ReportFieldCoalesceHelper.MaterializeFromLayout(table, metadataTable);
        }

        private static void RefreshConfiguredTableAvailability(DataTable table)
        {
            if (table.ExtendedProperties.Contains(ReportDataTableProperties.TableLayoutMetadata) &&
                table.ExtendedProperties[ReportDataTableProperties.TableLayoutMetadata] is DataTable metadataTable)
            {
                table.ExtendedProperties[ReportDataTableProperties.HasDetailTable] =
                    HasRenderableTableField(metadataTable, table);
            }
        }

        private static bool ShouldUseAllCompanySignatures(IReadOnlyDictionary<string, string> query)
        {
            var flag =
                GetQueryValue(query, "allSignatures") ??
                GetQueryValue(query, "ALL_SIGNATURES") ??
                GetQueryValue(query, "signatureMode") ??
                GetQueryValue(query, "SIGNATURE_MODE");

            var normalized = Common.NormalizeToken(flag);
            return normalized is "1" or "true" or "y" or "yes" or "all";
        }

        private static bool ShouldBlankVoucherData(IReadOnlyDictionary<string, string> query)
        {
            var flag =
                GetQueryValue(query, "blankVoucherData") ??
                GetQueryValue(query, "BLANK_VOUCHER_DATA") ??
                GetQueryValue(query, "hideVoucherData") ??
                GetQueryValue(query, "HIDE_VOUCHER_DATA");

            var normalized = Common.NormalizeToken(flag);
            return normalized is "1" or "true" or "y" or "yes";
        }

        private static void BlankVoucherDataTable(DataTable table)
        {
            if (table == null || table.Rows.Count == 0 || table.Columns.Count == 0)
            {
                return;
            }

            // Keep one layout row so voucher template still renders; wipe transactional values.
            while (table.Rows.Count > 1)
            {
                table.Rows.RemoveAt(table.Rows.Count - 1);
            }

            var row = table.Rows[0];
            foreach (DataColumn column in table.Columns)
            {
                if (string.Equals(column.ColumnName, "COMPANY_CD", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var shouldBlank =
                    VoucherDemoBlankFields.Contains(column.ColumnName)
                    || column.ColumnName.EndsWith("_TEXT", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("AMOUNT", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("REASON", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("PARTNER", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("NOTE", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("ACCOUNT", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.StartsWith("CHIT_", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("CUSTOMER", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("DESCRIPTION", StringComparison.OrdinalIgnoreCase)
                    || column.ColumnName.Contains("CREATED", StringComparison.OrdinalIgnoreCase);

                if (!shouldBlank)
                {
                    continue;
                }

                if (column.DataType == typeof(string))
                {
                    row[column] = string.Empty;
                }
                else if (column.DataType == typeof(decimal)
                    || column.DataType == typeof(double)
                    || column.DataType == typeof(float)
                    || column.DataType == typeof(int)
                    || column.DataType == typeof(long)
                    || column.DataType == typeof(short)
                    || column.DataType == typeof(byte))
                {
                    row[column] = Convert.ChangeType(0, column.DataType);
                }
                else if (column.AllowDBNull)
                {
                    row[column] = DBNull.Value;
                }
            }
        }

        private async Task<IReadOnlyList<CompanyReportSignatureInfo>> ResolveReportSignaturesAsync(
            string companyCd,
            IEnumerable<CompanyReportSignatureInfo>? mappedSignatures,
            IReadOnlyDictionary<string, string> query)
        {
            if (!ShouldUseAllCompanySignatures(query))
            {
                return (mappedSignatures ?? Array.Empty<CompanyReportSignatureInfo>()).ToList();
            }

            // Demo / allSignatures: always refresh so upload → preview is not stale-cached.
            await _companySignatureInfoService.ClearCacheAsync(companyCd);

            var rows = await _companySignatureInfoService.GetCompanySignatureInfosAsync(
                companyCd,
                id: null,
                signCode: null,
                isActive: "1");

            var resolved = rows
                .Where(item => string.Equals(Common.NormalizeFlagString(item.ISDEL, "0"), "0", StringComparison.OrdinalIgnoreCase))
                .Where(item => !string.IsNullOrWhiteSpace(Common.NormalizeNullableText(item.SIGN_CODE)))
                .OrderBy(item => item.SORT_ORDER ?? int.MaxValue)
                .ThenBy(item => item.SIGN_CODE, StringComparer.OrdinalIgnoreCase)
                .Select((item, index) => new CompanyReportSignatureInfo
                {
                    ID = item.ID,
                    COMPANY_CD = Common.NormalizeRequiredText(item.COMPANY_CD),
                    SIGN_CODE = Common.NormalizeRequiredText(item.SIGN_CODE),
                    DISPLAY_LABEL = Common.NormalizeNullableText(item.DISPLAY_LABEL),
                    SIGN_NAME = Common.NormalizeNullableText(item.SIGN_NAME),
                    SIGN_TITLE = Common.NormalizeNullableText(item.SIGN_TITLE),
                    SIGN_IMAGE_URL = Common.NormalizeNullableText(item.SIGN_IMAGE_URL),
                    SORT_ORDER = item.SORT_ORDER,
                    SIGN_SEQUENCE = index + 1
                })
                .ToList();

            return resolved;
        }

        private async Task ApplySysCodeDisplayValuesAsync(DataTable table, ExecutionContext context)
        {
            if (table == null || table.Rows.Count == 0 || table.Columns.Count == 0)
            {
                return;
            }

            var inPlaceTargets = MasterGridSysCodeColumns
                .Select(pair => new
                {
                    TargetField = pair.Key,
                    SourceField = pair.Key,
                    CodeType = pair.Value
                })
                .Where(item => ResolveColumn(table, item.SourceField) != null)
                .ToList();

            var displayTargets = MasterGridSysCodeDisplayColumns
                .Select(pair => new
                {
                    TargetField = pair.Key,
                    SourceField = pair.Value.CodeField,
                    CodeType = pair.Value.CodeType
                })
                .Where(item => ResolveColumn(table, item.SourceField) != null)
                .ToList();

            // Ensure display columns exist / are writable (SP may seed '' AS STATUS_TEXT with MaxLength=0).
            foreach (var displayTarget in displayTargets)
            {
                ReportDataTableHelper.EnsureColumn(table, displayTarget.TargetField, typeof(string));
            }

            var targets = inPlaceTargets
                .Concat(displayTargets)
                .Select(item => new
                {
                    TargetColumn = ResolveColumn(table, item.TargetField),
                    SourceColumn = ResolveColumn(table, item.SourceField),
                    item.CodeType
                })
                .Where(item => item.TargetColumn != null && item.SourceColumn != null)
                .ToList();

            if (targets.Count == 0)
            {
                return;
            }

            var codeTypes = targets
                .Select(item => item.CodeType)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var displayByTypeAndCd = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var codeType in codeTypes)
            {
                var codes = await _systemService.GetSysCodesAsync(context.CompanyCd, codeType);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var code in codes ?? Enumerable.Empty<SysCodeInfo>())
                {
                    var codeCd = Common.NormalizeNullableText(code.CODE_CD);
                    if (codeCd == null || map.ContainsKey(codeCd))
                    {
                        continue;
                    }

                    map[codeCd] = string.Equals(codeType, "FA_STATUS", StringComparison.OrdinalIgnoreCase)
                        ? ReportLanguageHelper.ResolveFaStatusDisplayText(codeCd, code.CODE_NAME, context.ReportLanguage)
                        : ReportLanguageHelper.ResolveSysCodeDisplayText(code.CODE_NAME, context.ReportLanguage);
                }

                displayByTypeAndCd[codeType] = map;
            }

            foreach (var target in targets)
            {
                var isFaStatus = string.Equals(target.CodeType, "FA_STATUS", StringComparison.OrdinalIgnoreCase);
                displayByTypeAndCd.TryGetValue(target.CodeType, out var displayByCd);
                if ((displayByCd == null || displayByCd.Count == 0) && !isFaStatus)
                {
                    continue;
                }

                displayByCd ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var sourceColumn = table.Columns[target.SourceColumn!]!;
                var targetColumn = table.Columns[target.TargetColumn!]!;
                foreach (DataRow row in table.Rows)
                {
                    if (row.IsNull(sourceColumn))
                    {
                        continue;
                    }

                    var raw = Common.NormalizeNullableText(Convert.ToString(row[sourceColumn], CultureInfo.InvariantCulture));
                    if (raw == null)
                    {
                        continue;
                    }

                    var lookupKey = isFaStatus
                        ? ReportLanguageHelper.NormalizeFaStatusCode(raw) ?? raw
                        : raw;
                    if (displayByCd.TryGetValue(lookupKey, out var display) && !string.IsNullOrWhiteSpace(display))
                    {
                        row[targetColumn] = display;
                        continue;
                    }

                    if (!isFaStatus)
                    {
                        continue;
                    }

                    var translatedStatus = ReportLanguageHelper.ResolveFaStatusDisplayText(
                        lookupKey,
                        null,
                        context.ReportLanguage);
                    if (!string.IsNullOrWhiteSpace(translatedStatus))
                    {
                        row[targetColumn] = translatedStatus;
                    }
                }
            }
        }

        private async Task ApplyPreviewGridPrintLayoutAsync(ExecutionContext context, DataTable table)
        {
            var columns = await ResolvePreviewGridColumnsAsync(context, table);
            var metadataTable = BuildPreviewGridTableLayoutMetadata(table.TableName, columns);
            table.ExtendedProperties[ReportDataTableProperties.HasDetailTable] = HasRenderableTableField(metadataTable, table);
            table.ExtendedProperties[ReportDataTableProperties.TableLayoutMetadata] = metadataTable;
        }

        private async Task<List<ConfiguredReportPreviewColumnDto>> ResolvePreviewGridColumnsAsync(
            ExecutionContext context,
            DataTable table)
        {
            var gridId = ResolvePreviewGridId(context);
            if (string.IsNullOrWhiteSpace(gridId))
            {
                throw new ArgumentException("printGridId is required when printing from sys grid.");
            }

            var columnSettings = await ResolvePreviewColumnSettingsAsync(context, gridId);
            var columns = BuildPreviewColumns(
                table,
                columnSettings,
                applyVisibilityFilter: true);

            return columns.Count > 0 ? columns : BuildPreviewColumns(table);
        }

        private async Task<ExecutionContext> BuildContextAsync(
            string companyCd,
            string? reportCode,
            string? menuCode,
            IReadOnlyDictionary<string, string> query)
        {
            var effectiveCompanyCd = Common.NormalizeRequiredText(companyCd);
            if (effectiveCompanyCd.Length == 0)
            {
                throw new ArgumentException("companyCd is required.", nameof(companyCd));
            }

            var effectiveReportCode = Common.NormalizeNullableText(reportCode);
            var effectiveMenuCode = Common.NormalizeNullableText(menuCode);
            var config = await _reportConfigurationRepository.GetCompanyReportConfigurationAsync(effectiveCompanyCd, effectiveReportCode, effectiveMenuCode)
                ?? throw new KeyNotFoundException("Report configuration not found.");
            var reportLanguage = ReportLanguageHelper.NormalizeLanguage(
                GetQueryValue(query, "language") ??
                GetQueryValue(query, "LANGUAGE") ??
                GetQueryValue(query, "lang") ??
                GetQueryValue(query, "LANG") ??
                GetQueryValue(query, "reportLanguage") ??
                GetQueryValue(query, "REPORT_LANGUAGE"));
            var elementReportKey = Common.NormalizeNullableText(config.REPORT_KEY) ?? effectiveMenuCode ?? effectiveReportCode ?? config.REPORT_CODE;
            if (!string.IsNullOrWhiteSpace(elementReportKey))
            {
                config.ELEMENTS = (await _reportConfigurationRepository.GetCompanyReportElementsAsync(
                    effectiveCompanyCd,
                    elementReportKey,
                    effectiveReportCode ?? config.REPORT_CODE)).ToList();
            }

            var namedValues = new Dictionary<string, object?>(Comparer);
            var normalizedValues = new Dictionary<string, object?>(Comparer);
            var reportOptionCode =
                GetQueryValue(query, "reportOptionCode") ??
                GetQueryValue(query, "REPORT_OPTION_CODE") ??
                string.Empty;

            AddValue(namedValues, normalizedValues, "companyCd", effectiveCompanyCd);
            AddValue(namedValues, normalizedValues, "COMPANY_CD", effectiveCompanyCd);
            AddValue(namedValues, normalizedValues, "p_COMPANY_CD", effectiveCompanyCd);
            AddValue(namedValues, normalizedValues, "company_cd", effectiveCompanyCd);
            AddValue(namedValues, normalizedValues, "reportCode", effectiveReportCode);
            AddValue(namedValues, normalizedValues, "REPORT_CODE", effectiveReportCode);
            AddValue(namedValues, normalizedValues, "p_REPORT_CODE", effectiveReportCode);
            AddValue(namedValues, normalizedValues, "reportOptionCode", reportOptionCode);
            AddValue(namedValues, normalizedValues, "REPORT_OPTION_CODE", reportOptionCode);
            AddValue(namedValues, normalizedValues, "p_REPORT_OPTION_CODE", reportOptionCode);
            AddValue(namedValues, normalizedValues, "menuCode", effectiveMenuCode);
            AddValue(namedValues, normalizedValues, "MENU_CODE", effectiveMenuCode);
            AddValue(namedValues, normalizedValues, "p_MENU_CODE", effectiveMenuCode);
            AddValue(namedValues, normalizedValues, "reportKey", effectiveMenuCode);
            AddValue(namedValues, normalizedValues, "REPORT_KEY", effectiveMenuCode);
            AddValue(namedValues, normalizedValues, "signIds", config.SIGN_IDS);
            AddValue(namedValues, normalizedValues, "SIGN_IDS", config.SIGN_IDS);
            AddValue(namedValues, normalizedValues, "signatures", config.SIGNATURES);
            AddValue(namedValues, normalizedValues, "SIGNATURES", config.SIGNATURES);
            AddValue(namedValues, normalizedValues, "reportElements", config.ELEMENTS);
            AddValue(namedValues, normalizedValues, "REPORT_ELEMENTS", config.ELEMENTS);

            var companyInfo = await _companyInfoRepository.GetCompanyInfoAsync(effectiveCompanyCd);
            AddCompanyInfoValues(namedValues, normalizedValues, companyInfo);

            foreach (var item in query)
            {
                var token = Common.NormalizeToken(item.Key);
                if (token is "fromdate" or "todate" or "pfromdate" or "ptodate")
                {
                    continue;
                }

                AddValue(namedValues, normalizedValues, item.Key, item.Value, false);
                AddValue(namedValues, normalizedValues, Common.NormalizeSqlParameterName(item.Key), item.Value, false);
            }

            AddReportPeriodValues(query, namedValues, normalizedValues);

            AddValue(namedValues, normalizedValues, "keyword", string.Empty, false);
            AddValue(namedValues, normalizedValues, "KEYWORD", string.Empty, false);
            AddValue(namedValues, normalizedValues, "p_KEYWORD", string.Empty, false);
            AddValue(namedValues, normalizedValues, "printLayout", string.Empty, false);
            AddValue(namedValues, normalizedValues, "PRINT_LAYOUT", string.Empty, false);
            AddValue(namedValues, normalizedValues, "p_PRINT_LAYOUT", string.Empty, false);

            AddReportLanguageValues(namedValues, normalizedValues, reportLanguage);

            return new ExecutionContext(
                config,
                effectiveCompanyCd,
                effectiveReportCode,
                effectiveMenuCode,
                reportLanguage,
                query,
                namedValues,
                normalizedValues,
                companyInfo);
        }

        private static void AddReportPeriodValues(
            IReadOnlyDictionary<string, string> query,
            IDictionary<string, object?> namedValues,
            IDictionary<string, object?> normalizedValues)
        {
            var fromYmd = Common.NormalizeNullableYmdText(GetQueryValue(query, "fromYmd"), "fromYmd");
            var toYmd = Common.NormalizeNullableYmdText(GetQueryValue(query, "toYmd"), "toYmd");
            Common.ValidateYmdRange(fromYmd, toYmd, "fromYmd", "toYmd");

            AddReportPeriodValue(
                fromYmd,
                "fromYmd",
                "p_FROM_YMD",
                "FROM_DATE",
                "p_FROM_DATE",
                namedValues,
                normalizedValues);
            AddReportPeriodValue(
                toYmd,
                "toYmd",
                "p_TO_YMD",
                "TO_DATE",
                "p_TO_DATE",
                namedValues,
                normalizedValues);
        }

        private static void AddReportPeriodValue(
            string? rawYmd,
            string ymdName,
            string ymdParameterName,
            string dateName,
            string dateParameterName,
            IDictionary<string, object?> namedValues,
            IDictionary<string, object?> normalizedValues)
        {
            if (rawYmd == null)
            {
                return;
            }

            AddValue(namedValues, normalizedValues, ymdName, rawYmd);
            AddValue(namedValues, normalizedValues, ymdParameterName, rawYmd);
            // Giữ duy nhất chuẩn wire YYYYMMDD cho cả SP dùng p_*_YMD và SP cũ
            // còn đặt tên p_*_DATE. MySQL tự ép chuỗi YYYYMMDD vào DATE nếu chữ ký
            // procedure vẫn là DATE; không chuyển thành DateTime vì sẽ làm CALL/log
            // quay lại dạng yyyy-MM-dd HH:mm:ss.fff.
            AddValue(namedValues, normalizedValues, dateName, rawYmd);
            AddValue(namedValues, normalizedValues, dateParameterName, rawYmd);
        }

        private async Task<ReportDataSourceResult> ResolveDataSourceAsync(
            ExecutionContext context,
            bool includeTableLayoutMetadata,
            CancellationToken cancellationToken)
        {
            var dataSourceRef = ResolveDataSourceReference(context.Config, out var dataSourceType);
            if (dataSourceRef == null)
            {
                return ReportDataSourceResult.Empty;
            }

            return Common.NormalizeRequiredText(dataSourceType) switch
            {
                "SP" or "STOREDPROC" or "STORED_PROCEDURE" or "SQL" => await ExecuteCommandSourceAsync(
                    dataSourceRef,
                    context,
                    dataSourceType,
                    includeTableLayoutMetadata,
                    cancellationToken),
                _ => new ReportDataSourceResult
                {
                    Data = await InvokeConfiguredSourceAsync(dataSourceRef, context, cancellationToken)
                }
            };
        }

        private static string? ResolveDataSourceReference(ReportConfigurationInfo config, out string? dataSourceType)
        {
            dataSourceType = config.DATA_SOURCE_TYPE;
            var dataSourceRef = Common.NormalizeNullableText(config.DATA_SOURCE_REF);
            if (dataSourceRef != null)
            {
                return dataSourceRef;
            }

            var reportSource = Common.NormalizeNullableText(config.REPORT_SOURCE);
            if (reportSource == null || Common.ResolveType(reportSource) != null)
            {
                return null;
            }

            var reportCode = Common.NormalizeNullableText(config.REPORT_CODE) ?? reportSource;
            throw new InvalidOperationException(
                $"Procedure chưa khai báo cho report '{reportCode}'. DATA_SOURCE_REF trống.");
        }

        private async Task<IReadOnlyList<SysGridColumn>> ResolvePreviewColumnSettingsAsync(
            ExecutionContext context,
            string gridId)
        {
            var userId = Common.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("USER_ID is required.");
            }

            long? templateId = null;
            var rawTemplateId = ResolvePreviewTemplateId(context);
            if (long.TryParse(rawTemplateId, out var parsedTemplateId) && parsedTemplateId > 0)
            {
                templateId = parsedTemplateId;
            }

            return await _sysGridColumnSettingRepository.GetMergedLayoutAsync(
                context.CompanyCd,
                userId,
                gridId,
                templateId);
        }

        /// <summary>Only explicit printGridId — GRID_ID is globally unique.</summary>
        private static string ResolvePreviewGridId(ExecutionContext context)
        {
            return GetQueryValue(context.Query, "printGridId") ?? string.Empty;
        }

        private static string? ResolvePreviewTemplateId(ExecutionContext context)
        {
            return GetQueryValue(context.Query, "printTemplateId");
        }

        private static bool IsVisibleGridColumnSetting(SysGridColumn setting)
        {
            return Common.NormalizeFlagString(setting.IS_VISIBLE, "1") == "1";
        }

        private static DataTable BuildPreviewGridTableLayoutMetadata(
            string tableName,
            IReadOnlyList<ConfiguredReportPreviewColumnDto> columns)
        {
            var metadataTable = new DataTable($"{Common.NormalizeNullableText(tableName) ?? "REPORT"}_PREVIEW_GRID_LAYOUT");
            metadataTable.Columns.Add("COLUMN_KEY", typeof(string));
            metadataTable.Columns.Add("PARENT_KEY", typeof(string));
            metadataTable.Columns.Add("FIELD_NAME", typeof(string));
            metadataTable.Columns.Add("CAPTION", typeof(string));
            metadataTable.Columns.Add("LABEL_TEXT", typeof(string));
            metadataTable.Columns.Add("ROW_INDEX", typeof(int));
            metadataTable.Columns.Add("COL_INDEX", typeof(int));
            metadataTable.Columns.Add("COL_SPAN", typeof(int));
            metadataTable.Columns.Add("ROW_SPAN", typeof(int));
            metadataTable.Columns.Add("WIDTH", typeof(float));
            metadataTable.Columns.Add("ALIGN", typeof(string));
            metadataTable.Columns.Add("FORMAT_TYPE", typeof(string));
            metadataTable.Columns.Add("SORT_ORDER", typeof(int));

            for (var index = 0; index < columns.Count; index++)
            {
                var column = columns[index];
                var row = metadataTable.NewRow();
                row["COLUMN_KEY"] = column.COLUMN_KEY;
                row["PARENT_KEY"] = DBNull.Value;
                row["FIELD_NAME"] = column.FIELD_NAME;
                row["CAPTION"] = column.CAPTION;
                row["LABEL_TEXT"] = string.IsNullOrWhiteSpace(column.LABEL_TEXT) ? DBNull.Value : column.LABEL_TEXT;
                row["ROW_INDEX"] = 0;
                row["COL_INDEX"] = index;
                row["COL_SPAN"] = 1;
                row["ROW_SPAN"] = 1;
                row["WIDTH"] = (float)Math.Max(40d, column.WIDTH);
                row["ALIGN"] = column.ALIGN;
                row["FORMAT_TYPE"] = ResolvePreviewGridFormatType(column);
                row["SORT_ORDER"] = index;
                metadataTable.Rows.Add(row);
            }

            return metadataTable;
        }

        private static string? ResolvePreviewGridFormatType(ConfiguredReportPreviewColumnDto column)
        {
            var format = Common.NormalizeNullableText(column.FORMAT)
                ?? InferPreviewFormatTypeFromFieldName(column.FIELD_NAME);
            if (format != null)
            {
                var normalizedFormat = Common.NormalizeToken(format);
                return normalizedFormat switch
                {
                    "0" or "n0" or "number0" or "integer" or "#,##0" => "number0",
                    "00" or "n1" or "number1" or "#,##0.0" => "number1",
                    "000" or "n2" or "number2" or "amount" or "quantity" or "unitprice" or "#,##0.00" => "number2",
                    "0000" or "n3" or "number3" or "#,##0.000" => "number3",
                    "00000" or "n4" or "number4" or "#,##0.0000" => "number4",
                    "date" or "dd/mm/yyyy" or "yyyy-mm-dd" => "date",
                    "datetime" or "dd/mm/yyyy hh:mm" => "datetime",
                    "text" or "string" => "text",
                    _ => column.DATA_TYPE == "number"
                        ? "number2"
                        : column.DATA_TYPE == "date"
                            ? "date"
                            : column.DATA_TYPE
                };
            }

            return column.DATA_TYPE == "number" ? "number2" : column.DATA_TYPE == "date" ? "date" : column.DATA_TYPE;
        }

        private static string? InferPreviewFormatTypeFromFieldName(string? fieldName)
        {
            var token = Common.NormalizeToken(fieldName);
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            if (token.EndsWith("ymd", StringComparison.Ordinal) ||
                token.EndsWith("date", StringComparison.Ordinal) ||
                token.EndsWith("ym", StringComparison.Ordinal))
            {
                return "date";
            }

            if (token.EndsWith("amt", StringComparison.Ordinal) ||
                token.EndsWith("amount", StringComparison.Ordinal) ||
                token.EndsWith("price", StringComparison.Ordinal) ||
                token.EndsWith("qty", StringComparison.Ordinal) ||
                token.EndsWith("quantity", StringComparison.Ordinal))
            {
                return "number2";
            }

            return null;
        }

        private static ConfiguredReportPreviewDto BuildPreviewDto(
            ExecutionContext context,
            DataTable table,
            IReadOnlyList<ConfiguredReportPreviewColumnDto>? columns = null)
        {
            columns ??= BuildPreviewColumns(table);
            var previewMode = ResolvePreviewMode(table);
            var fields = columns
                .Select(column => column.FIELD_NAME)
                .Concat(EnumerateSystemFieldsFromTable(table))
                .Where(field => HasColumn(table, field))
                .Distinct(Comparer)
                .ToList();

            var resolvedFieldColumns = fields
                .Select(field => new
                {
                    Field = field,
                    ColumnName = ResolveColumn(table, field)
                })
                .Where(x => x.ColumnName != null)
                .Select(x => new
                {
                    x.Field,
                    Column = table.Columns[x.ColumnName!]
                })
                .Where(x => x.Column != null)
                .Select(x => (x.Field, Column: x.Column!))
                .ToList();

            var rowKeyColumnName = ResolveColumn(table, "__ROW_KEY");
            var rows = new List<ConfiguredReportPreviewRowDto>();
            var sourceRows = ResolvePreviewRows(table);

            for (var index = 0; index < sourceRows.Count; index++)
            {
                var sourceRow = sourceRows[index].Row;
                var rowKey = rowKeyColumnName == null
                    ? null
                    : Common.NormalizeNullableText(sourceRow[rowKeyColumnName]?.ToString());

                var row = new ConfiguredReportPreviewRowDto
                {
                    ROW_KEY = rowKey ?? (index + 1).ToString(CultureInfo.InvariantCulture)
                };

                foreach (var (field, column) in resolvedFieldColumns)
                {
                    row.VALUES[field] = NormalizePreviewValue(sourceRow[column]);
                }

                rows.Add(row);
            }

            return new ConfiguredReportPreviewDto
            {
                COMPANY_CD = context.CompanyCd,
                REPORT_CODE = context.Config.REPORT_CODE ?? context.ReportCode ?? string.Empty,
                REPORT_NAME = ReportLanguageHelper.ResolveDisplayLabel(
                    context.Config.LABEL_TEXT,
                    context.ReportLanguage),
                PREVIEW_MODE = previewMode,
                COLUMNS = columns.ToList(),
                ROWS = rows
            };
        }

        private static string ResolvePreviewMode(DataTable table)
        {
            return OutlinePreviewRequiredFields.All(field => HasColumn(table, field))
                ? PreviewModeOutlineGrid
                : PreviewModeFlatReport;
        }

        private static List<PreviewSourceRow> ResolvePreviewRows(DataTable table)
        {
            return table.Rows
                .Cast<DataRow>()
                .Select((row, index) => new PreviewSourceRow(row, index))
                .ToList();
        }

        private static List<ConfiguredReportPreviewColumnDto> BuildPreviewColumns(
            DataTable table,
            IReadOnlyList<SysGridColumn>? columnSettings = null,
            bool applyVisibilityFilter = false)
        {
            var matchedSettings = (columnSettings ?? Array.Empty<SysGridColumn>())
                .Where(setting => !string.IsNullOrWhiteSpace(setting.FIELD_NAME))
                .Where(setting => !ReportSystemFields.IsSystemField(setting.FIELD_NAME))
                .Select((setting, index) => new
                {
                    Setting = setting,
                    ColumnName = ResolveColumn(table, setting.FIELD_NAME),
                    Index = index
                })
                .Where(item => item.ColumnName != null)
                .GroupBy(item => item.ColumnName!, Comparer)
                .Select(group => group
                    .OrderBy(item => item.Setting.VISIBLE_INDEX ?? int.MaxValue)
                    .ThenBy(item => item.Index)
                    .First())
                .ToList();

            if (applyVisibilityFilter && matchedSettings.Count > 0)
            {
                return matchedSettings
                    .Where(item => IsVisibleGridColumnSetting(item.Setting))
                    .OrderBy(item => item.Setting.VISIBLE_INDEX ?? int.MaxValue)
                    .ThenBy(item => item.Index)
                    .Select((item, index) =>
                    {
                        var column = CreatePreviewColumn(table.Columns[item.ColumnName!]!, item.Setting, index);
                        column.SORT_ORDER = index;
                        return column;
                    })
                    .ToList();
            }

            var settingsByColumn = matchedSettings.ToDictionary(
                item => item.ColumnName!,
                item => item.Setting,
                Comparer);

            return table.Columns
                .Cast<DataColumn>()
                .Where(column => !ReportSystemFields.IsSystemField(column.ColumnName))
                .Select((column, index) =>
                {
                    settingsByColumn.TryGetValue(column.ColumnName, out var setting);
                    return setting == null
                        ? CreatePreviewColumn(column, index)
                        : CreatePreviewColumn(column, setting, index);
                })
                .OrderBy(column => settingsByColumn.TryGetValue(column.FIELD_NAME, out var setting)
                    ? setting.VISIBLE_INDEX ?? int.MaxValue
                    : int.MaxValue)
                .ThenBy(column => column.SORT_ORDER)
                .Select((column, index) =>
                {
                    column.SORT_ORDER = index;
                    return column;
                })
                .ToList();
        }

        private static ConfiguredReportPreviewColumnDto CreatePreviewColumn(
            DataColumn column,
            SysGridColumn setting,
            int index)
        {
            var previewColumn = CreatePreviewColumn(column, index);
            var settingLabel = Common.NormalizeNullableText(setting.LABEL_TEXT);

            // Setting only overrides LABEL_TEXT; CAPTION stays from schema / sys_grid_column.
            if (settingLabel != null)
            {
                previewColumn.LABEL_TEXT = settingLabel;
            }

            if (setting.COLUMN_WIDTH.HasValue && setting.COLUMN_WIDTH.Value > 0)
            {
                previewColumn.WIDTH = setting.COLUMN_WIDTH.Value;
            }

            var align = Common.NormalizeNullableText(setting.ALIGN);
            if (align != null)
            {
                previewColumn.ALIGN = align.ToLowerInvariant();
            }

            var formatType = Common.NormalizeNullableText(setting.FORMAT_TYPE)
                ?? InferPreviewFormatTypeFromFieldName(column.ColumnName);
            if (formatType != null)
            {
                previewColumn.FORMAT = formatType;
                var dataTypeFromFormat = ResolvePreviewDataTypeFromFormatType(formatType);
                if (dataTypeFromFormat != null)
                {
                    previewColumn.DATA_TYPE = dataTypeFromFormat;
                }
            }

            previewColumn.SORT_ORDER = index;
            return previewColumn;
        }

        private static ConfiguredReportPreviewColumnDto CreatePreviewColumn(DataColumn column, int index)
        {
            var dataType = Nullable.GetUnderlyingType(column.DataType) ?? column.DataType;
            var inferredFormatType = InferPreviewFormatTypeFromFieldName(column.ColumnName);
            var dataTypeName = ResolvePreviewDataTypeFromFormatType(inferredFormatType)
                ?? ResolvePreviewDataType(dataType);
            return new ConfiguredReportPreviewColumnDto
            {
                COLUMN_KEY = column.ColumnName,
                FIELD_NAME = column.ColumnName,
                LABEL_TEXT = null,
                CAPTION = Common.NormalizeNullableText(column.Caption) ?? ResolveDefaultPreviewCaption(column.ColumnName),
                DATA_TYPE = dataTypeName,
                FORMAT = inferredFormatType ?? ResolvePreviewFormat(null, dataType),
                ALIGN = ResolvePreviewAlignment(null, dataType),
                WIDTH = CalculatePreviewWidth(column.ColumnName, dataType),
                SORT_ORDER = index
            };
        }

        private static string? ResolvePreviewDataTypeFromFormatType(string? formatType)
        {
            return Common.NormalizeToken(formatType) switch
            {
                "number0" or "number1" or "number2" or "number3" or "number4"
                    or "integer" or "amount" or "quantity" or "unitprice" => "number",
                "date" or "datetime" => "date",
                "boolean" or "bool" => "boolean",
                "text" or "string" => "string",
                _ => null
            };
        }

        private static object? NormalizePreviewValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return null;
            }

            return value is DateTime dateTime
                ? dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : value;
        }

        private static IEnumerable<string> EnumerateSystemFieldsFromTable(DataTable table)
        {
            foreach (DataColumn column in table.Columns)
            {
                if (ReportSystemFields.IsSystemField(column.ColumnName))
                {
                    yield return column.ColumnName;
                }
            }
        }

        private static string ResolvePreviewDataType(Type dataType)
        {
            if (IsNumericType(dataType))
            {
                return "number";
            }

            if (dataType == typeof(DateTime) || dataType == typeof(DateTimeOffset))
            {
                return "date";
            }

            return dataType == typeof(bool) ? "boolean" : "string";
        }

        private static string? ResolvePreviewFormat(string? formatType, Type dataType)
        {
            return Common.NormalizeToken(formatType) switch
            {
                "number0" or "integer" => "#,##0",
                "number1" => "#,##0.0",
                "number2" or "amount" or "quantity" or "unitprice" => "#,##0.00",
                "number3" => "#,##0.000",
                "number4" => "#,##0.0000",
                "date" => "dd/MM/yyyy",
                "datetime" => "dd/MM/yyyy HH:mm",
                _ => IsNumericType(dataType) ? "#,##0.00" : dataType == typeof(DateTime) ? "dd/MM/yyyy" : null
            };
        }

        private static string ResolvePreviewAlignment(string? alignment, Type dataType)
        {
            return Common.NormalizeToken(alignment) switch
            {
                "center" or "middlecenter" => "center",
                "right" or "middleright" => "right",
                "left" or "middleleft" => "left",
                _ => IsNumericType(dataType) ? "right" : dataType == typeof(DateTime) ? "center" : "left"
            };
        }

        private static double CalculatePreviewWidth(string columnName, Type dataType)
        {
            var upper = columnName.ToUpperInvariant();
            if (IsNumericType(dataType))
            {
                return 130d;
            }

            if (dataType == typeof(DateTime))
            {
                return 120d;
            }

            if (upper.Contains("DESCRIPTION") || upper.Contains("ADDRESS") || upper.Contains("NOTE") || upper.Contains("REMARK"))
            {
                return 280d;
            }

            return 140d;
        }

        private static string ResolveDefaultPreviewCaption(string columnName)
        {
            return columnName.Replace("_", " ").Trim();
        }

        private void EnrichReportDataTable(DataTable sourceTable, ExecutionContext context)
        {
            if (sourceTable == null || sourceTable.Columns.Count == 0)
            {
                return;
            }

            if (RequiresVoucherFormatting(sourceTable, context))
            {
                VoucherReportHelper.EnrichReportTable(sourceTable, context.ReportLanguage);
            }

            if (RequiresReportDateFormatting(sourceTable, context))
            {
                ReportDatePeriodHelper.EnrichReportTable(sourceTable, context.Query, context.ReportLanguage);
            }
        }

        private static bool RequiresVoucherFormatting(DataTable sourceTable, ExecutionContext context)
        {
            if (VoucherFormattedFields.Any(field => HasColumn(sourceTable, field)))
            {
                return true;
            }

            return context.Config.ELEMENTS.Any(element =>
            {
                var source = Common.NormalizeToken(element.VALUE_SOURCE).ToUpperInvariant();
                var field = Common.NormalizeNullableText(element.VALUE_FIELD);
                return source == "DATA" && field != null && VoucherFormattedFields.Contains(field);
            });
        }

        private static bool RequiresReportDateFormatting(DataTable sourceTable, ExecutionContext context)
        {
            if (ReportDateFormattedFields.Any(field => HasColumn(sourceTable, field)))
            {
                return true;
            }

            return context.Config.ELEMENTS.Any(element =>
            {
                var source = Common.NormalizeToken(element.VALUE_SOURCE).ToUpperInvariant();
                var field = Common.NormalizeNullableText(element.VALUE_FIELD);
                var itemKey = Common.NormalizeNullableText(element.ITEM_KEY);
                return (source == "DATA" && field != null && ReportDateFormattedFields.Contains(field)) ||
                       (itemKey != null && ReportDateItemKeys.Contains(itemKey));
            });
        }

        private async Task<ReportDataSourceResult> ExecuteCommandSourceAsync(
            string commandText,
            ExecutionContext context,
            string? dataSourceType,
            bool includeTableLayoutMetadata,
            CancellationToken cancellationToken)
        {
            var (db, sql) = Common.ParseCommandReference(commandText);
            sql = NormalizeSpCall(dataSourceType, sql);
            var parameters = BuildCommandParameters(sql, context);

            var tableName = Common.NormalizeNullableText(context.Config.DATA_SET_NAME)
                            ?? context.Config.REPORT_CODE;

            var sw = Stopwatch.StartNew();
            cancellationToken.ThrowIfCancellationRequested();
            var databaseName = db == Net_DB.Net_DB_Company
                ? await _companyDatabaseResolver.ResolveDatabaseNameAsync(context.CompanyCd)
                : null;

            using var connection = _db.GetOpenConnection(db, databaseName);

            var command = new CommandDefinition(
                sql,
                parameters,
                cancellationToken: cancellationToken);
            using var reader = await connection.ExecuteReaderAsync(command);

            cancellationToken.ThrowIfCancellationRequested();
            var dataTable = new DataTable(tableName);
            dataTable.Load(reader);
            cancellationToken.ThrowIfCancellationRequested();

            var metadataTable = includeTableLayoutMetadata
                ? await LoadReportColumnLayoutMetadataAsync(context, tableName ?? string.Empty)
                : null;

            if (includeTableLayoutMetadata && metadataTable != null && IsTableLayoutMetadata(metadataTable))
            {
                dataTable.ExtendedProperties[ReportDataTableProperties.HasDetailTable] =
                    HasRenderableTableField(metadataTable, dataTable);

                dataTable.ExtendedProperties[ReportDataTableProperties.TableLayoutMetadata] =
                    metadataTable;
            }

            sw.Stop();
            Common.LogQuery(sql, parameters, sw.ElapsedMilliseconds);

            return new ReportDataSourceResult
            {
                Data = dataTable
            };
        }

        private async Task<DataTable?> LoadReportColumnLayoutMetadataAsync(ExecutionContext context, string tableName)
        {
            var reportKey = Common.NormalizeNullableText(context.Config.REPORT_KEY) ??
                            context.MenuCode ??
                            context.ReportCode ??
                            context.Config.REPORT_CODE;
            var reportCode = Common.NormalizeNullableText(context.Config.REPORT_CODE) ?? context.ReportCode;
            if (string.IsNullOrWhiteSpace(reportKey) && string.IsNullOrWhiteSpace(reportCode))
            {
                return null;
            }

            var rows = (await _reportConfigurationRepository.GetReportColumnLayoutAsync(
                context.CompanyCd,
                reportKey ?? string.Empty,
                reportCode)).ToList();
            if (rows.Count == 0)
            {
                return null;
            }

            var table = Common.ConvertToDataTable(rows, $"{tableName}_TABLE_LAYOUT");
            return IsTableLayoutMetadata(table) ? table : null;
        }

        private static bool IsTableLayoutMetadata(DataTable table)
        {
            foreach (var columnName in new[]
            {
                "COLUMN_KEY",
                "PARENT_KEY",
                "FIELD_NAME",
                "CAPTION",
                "ROW_INDEX",
                "COL_INDEX",
                "COL_SPAN",
                "ROW_SPAN",
                "WIDTH",
                "ALIGN",
                "FORMAT_TYPE",
                "SORT_ORDER"
            })
            {
                if (!HasColumn(table, columnName))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasRenderableTableField(DataTable metadataTable, DataTable dataTable)
        {
            var fieldNameColumn = ResolveColumn(metadataTable, "FIELD_NAME");
            if (fieldNameColumn == null)
            {
                return false;
            }

            foreach (DataRow row in metadataTable.Rows)
            {
                var fieldName = Common.NormalizeNullableText(row[fieldNameColumn]?.ToString());
                if (fieldName != null && HasColumn(dataTable, fieldName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasColumn(DataTable table, string columnName)
        {
            return ResolveColumn(table, columnName) != null;
        }

        private static string? ResolveColumn(DataTable table, string columnName)
        {
            return table.Columns.Cast<DataColumn>()
                .FirstOrDefault(column => column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                ?.ColumnName;
        }

        private static DynamicParameters BuildCommandParameters(string commandText, ExecutionContext context)
        {
            var parameters = new DynamicParameters();
            var parameterNames = ExtractSqlParameterNames(commandText);

            foreach (var parameterName in parameterNames)
            {
                if (!TryResolveCommandParameterValue(context, parameterName, out var value))
                {
                    continue;
                }

                var resolvedValue = ResolveDbParameterValue(parameterName, value);
                var key = parameterName.TrimStart('@');
                if (!parameters.ParameterNames.Contains(key, Comparer))
                {
                    parameters.Add(key, resolvedValue);
                }
            }

            return parameters;
        }

        private static IReadOnlyList<string> ExtractSqlParameterNames(string commandText)
        {
            return Regex.Matches(commandText, @"(?<!@)@([A-Za-z_][A-Za-z0-9_]*)")
                .Select(match => match.Groups[1].Value)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(Comparer)
                .ToList();
        }

        private static bool TryResolveCommandParameterValue(ExecutionContext context, string parameterName, out object? value)
        {
            foreach (var key in EnumerateParameterLookupKeys(parameterName))
            {
                if (context.NamedValues.TryGetValue(key, out value))
                {
                    return true;
                }
            }

            foreach (var key in EnumerateKeys(Common.NormalizeToken(parameterName)))
            {
                if (context.NormalizedValues.TryGetValue(key, out value))
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static IEnumerable<string> EnumerateParameterLookupKeys(string parameterName)
        {
            var normalizedName = Common.NormalizeNullableText(parameterName)?.TrimStart('@');
            if (normalizedName == null)
            {
                yield break;
            }

            yield return normalizedName;

            var sqlParameterName = Common.NormalizeSqlParameterName(normalizedName);
            if (!sqlParameterName.Equals(normalizedName, StringComparison.OrdinalIgnoreCase))
            {
                yield return sqlParameterName;
            }

            if (normalizedName.StartsWith("p_", StringComparison.OrdinalIgnoreCase) && normalizedName.Length > 2)
            {
                yield return normalizedName[2..];
            }
        }

        private static object? ResolveDbParameterValue(string parameterName, object? value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is char character)
            {
                return character.ToString();
            }

            if (IsDbScalarValue(value))
            {
                return value;
            }

            if (value is IEnumerable enumerable && value is not string && value is not byte[])
            {
                var values = new List<object?>();
                foreach (var item in enumerable)
                {
                    if (item != null && !IsDbScalarValue(item))
                    {
                        throw new NotSupportedException($"Report parameter '{parameterName}' contains unsupported value type '{item.GetType().FullName}'.");
                    }

                    values.Add(item);
                }

                return values;
            }

            throw new NotSupportedException($"Report parameter '{parameterName}' has unsupported value type '{value.GetType().FullName}'.");
        }

        private static bool IsDbScalarValue(object value)
        {
            var type = value.GetType();
            return type.IsPrimitive ||
                   type.IsEnum ||
                   value is string ||
                   value is decimal ||
                   value is DateTime ||
                   value is DateTimeOffset ||
                   value is TimeSpan ||
                   value is Guid ||
                   value is byte[];
        }

        private async Task<object?> InvokeConfiguredSourceAsync(string dataSourceRef, ExecutionContext context, CancellationToken cancellationToken)
        {
            var separatorIndex = dataSourceRef.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex == dataSourceRef.Length - 1)
            {
                throw new InvalidOperationException($"Invalid DATA_SOURCE_REF '{dataSourceRef}'.");
            }

            var serviceType = Common.ResolveType(dataSourceRef[..separatorIndex].Trim())
                ?? throw new InvalidOperationException($"Service type '{dataSourceRef[..separatorIndex]}' was not found.");
            var methodName = dataSourceRef[(separatorIndex + 1)..].Trim();
            var service = ResolveService(serviceType)
                ?? throw new InvalidOperationException($"Service '{serviceType.FullName}' is not registered in DI.");

            foreach (var method in serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(item => item.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.GetParameters().Length))
            {
                if (!TryBuildArguments(method.GetParameters(), null, context, cancellationToken, out var args))
                {
                    continue;
                }

                return await Common.UnwrapAsyncResultAsync(method.Invoke(service, args), method.ReturnType);
            }

            throw new InvalidOperationException($"Unable to bind parameters for '{dataSourceRef}'.");
        }

        private static bool TryBuildArguments(
            ParameterInfo[] parameters,
            object? data,
            ExecutionContext context,
            CancellationToken cancellationToken,
            out object?[] args)
        {
            args = new object?[parameters.Length];

            for (var index = 0; index < parameters.Length; index++)
            {
                if (!TryResolveParameter(parameters[index], data, context, cancellationToken, out var value))
                {
                    args = Array.Empty<object?>();
                    return false;
                }

                args[index] = value;
            }

            return true;
        }

        private static bool TryResolveParameter(
            ParameterInfo parameter,
            object? data,
            ExecutionContext context,
            CancellationToken cancellationToken,
            out object? value)
        {
            if (data != null && parameter.ParameterType.IsInstanceOfType(data))
            {
                value = data;
                return true;
            }

            if (parameter.ParameterType == typeof(CancellationToken))
            {
                value = cancellationToken;
                return true;
            }

            if (parameter.ParameterType.IsAssignableFrom(typeof(Dictionary<string, string>)))
            {
                value = new Dictionary<string, string>(context.Query, Comparer);
                return true;
            }

            if (parameter.ParameterType.IsAssignableFrom(typeof(IReadOnlyDictionary<string, string>)))
            {
                value = context.Query;
                return true;
            }

            foreach (var key in EnumerateKeys(Common.NormalizeToken(parameter.Name)))
            {
                if (!context.NormalizedValues.TryGetValue(key, out var rawValue))
                {
                    continue;
                }

                try
                {
                    value = Common.ConvertRuntimeValue(rawValue, parameter.ParameterType);
                    return true;
                }
                catch
                {
                }
            }

            if (parameter.HasDefaultValue)
            {
                value = parameter.DefaultValue;
                return true;
            }

            if (!parameter.ParameterType.IsValueType || Nullable.GetUnderlyingType(parameter.ParameterType) != null)
            {
                value = null;
                return true;
            }

            value = null;
            return false;
        }

        private object? ResolveService(Type serviceType)
        {
            var service = _serviceProvider.GetService(serviceType);
            if (service != null)
            {
                return service;
            }

            if (!serviceType.IsClass)
            {
                return null;
            }

            return serviceType.GetInterfaces()
                .Select(interfaceType => _serviceProvider.GetService(interfaceType))
                .FirstOrDefault(candidate => candidate != null && serviceType.IsInstanceOfType(candidate));
        }

        private static IEnumerable<string> EnumerateKeys(string normalizedName)
        {
            if (normalizedName.Length == 0)
            {
                yield break;
            }

            yield return normalizedName;

            if (normalizedName.StartsWith("p", StringComparison.OrdinalIgnoreCase) && normalizedName.Length > 1)
            {
                yield return normalizedName[1..];
            }
        }

        private static void AddValue(
            IDictionary<string, object?> namedValues,
            IDictionary<string, object?> normalizedValues,
            string? key,
            object? value,
            bool overwrite = true)
        {
            var normalizedKey = Common.NormalizeNullableText(key);
            if (normalizedKey == null)
            {
                return;
            }

            if (overwrite || !namedValues.ContainsKey(normalizedKey))
            {
                namedValues[normalizedKey] = value;
            }

            var token = Common.NormalizeToken(normalizedKey);
            if (token.Length > 0 && (overwrite || !normalizedValues.ContainsKey(token)))
            {
                normalizedValues[token] = value;
            }
        }

        private static void AddCompanyInfoValues(
            IDictionary<string, object?> namedValues,
            IDictionary<string, object?> normalizedValues,
            CompanyInfo? companyInfo)
        {
            AddValue(namedValues, normalizedValues, "companyInfo", companyInfo);
            AddValue(namedValues, normalizedValues, "COMPANY_INFO", companyInfo);

            if (companyInfo == null)
            {
                return;
            }

            foreach (var property in typeof(CompanyInfo).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var propertyValue = property.GetValue(companyInfo);
                AddValue(namedValues, normalizedValues, property.Name, propertyValue, false);
                AddValue(namedValues, normalizedValues, Common.NormalizeSqlParameterName(property.Name), propertyValue, false);
            }
        }

        private static void AddReportLanguageValues(
            IDictionary<string, object?> namedValues,
            IDictionary<string, object?> normalizedValues,
            string reportLanguage)
        {
            AddValue(namedValues, normalizedValues, "language", reportLanguage);
            AddValue(namedValues, normalizedValues, "LANGUAGE", reportLanguage);
            AddValue(namedValues, normalizedValues, "lang", reportLanguage);
            AddValue(namedValues, normalizedValues, "LANG", reportLanguage);
            AddValue(namedValues, normalizedValues, "reportLanguage", reportLanguage);
            AddValue(namedValues, normalizedValues, "REPORT_LANGUAGE", reportLanguage);
            AddValue(namedValues, normalizedValues, "p_LANGUAGE", reportLanguage);
            AddValue(namedValues, normalizedValues, "p_LANG", reportLanguage);
            AddValue(namedValues, normalizedValues, "p_REPORT_LANGUAGE", reportLanguage);
        }

        private static bool IsFaStatusTextReport(ExecutionContext context)
        {
            foreach (var key in new[]
            {
                context.ReportCode,
                context.MenuCode,
                context.Config.REPORT_CODE,
                context.Config.REPORT_KEY
            })
            {
                var normalized = Common.NormalizeNullableText(key);
                if (normalized != null && FaStatusTextReports.Contains(normalized))
                {
                    return true;
                }
            }

            return false;
        }

        private static string? GetQueryValue(IReadOnlyDictionary<string, string> query, string? key)
        {
            var normalizedKey = Common.NormalizeNullableText(key);
            return normalizedKey != null && query.TryGetValue(normalizedKey, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : null;
        }

        private static bool IsNumericType(Type dataType)
        {
            return dataType == typeof(byte) ||
                   dataType == typeof(short) ||
                   dataType == typeof(int) ||
                   dataType == typeof(long) ||
                   dataType == typeof(float) ||
                   dataType == typeof(double) ||
                   dataType == typeof(decimal);
        }

        private static string NormalizeSpCall(string? dataSourceType, string commandText)
        {
            var type = Common.NormalizeRequiredText(dataSourceType);
            var isSp = type.Equals("SP", StringComparison.OrdinalIgnoreCase) ||
                       type.Equals("STOREDPROC", StringComparison.OrdinalIgnoreCase) ||
                       type.Equals("STORED_PROCEDURE", StringComparison.OrdinalIgnoreCase);
            if (!isSp || Common.LooksLikeSqlCommand(commandText))
            {
                return commandText;
            }

            return $"CALL {commandText}()";
        }


        private sealed record ExecutionContext(
            ReportConfigurationInfo Config,
            string CompanyCd,
            string? ReportCode,
            string? MenuCode,
            string ReportLanguage,
            IReadOnlyDictionary<string, string> Query,
            IReadOnlyDictionary<string, object?> NamedValues,
            IReadOnlyDictionary<string, object?> NormalizedValues,
            CompanyInfo? CompanyInfo);

        private sealed record PreviewSourceRow(DataRow Row, int OriginalIndex);

        private sealed record ResolvedReportData(
            ExecutionContext Context,
            DataTable Table);

        private enum PrintLayoutMode
        {
            Document,
            Grid
        }

        private sealed class ReportDataSourceResult
        {
            public static ReportDataSourceResult Empty { get; } = new();

            public object? Data { get; init; }
        }
    }
}

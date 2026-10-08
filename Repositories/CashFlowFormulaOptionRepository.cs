using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Dapper;

namespace API_AMNOTE_WEB.Repositories
{
    /// <summary>
    /// Stores the editable DETAIL rows for the B03 cash-flow templates in the
    /// company database. The report procedures resolve templates by company,
    /// so this repository deliberately uses the same source priority.
    /// </summary>
    public sealed class CashFlowFormulaOptionRepository : ICashFlowFormulaOptionRepository
    {
        private const string CashAccountPrefixesItemKey = "CASH_ACCOUNT_PREFIXES";
        private const string DefaultCashAccountPrefixes = "111,112,113";
        private const string BlankTemplateCompanyCd = "";

        // Dapper list expansion drops empty strings, so blank template rows must be matched explicitly.
        private const string TemplateCompanyMatchSql = "(template.COMPANY_CD IN @CandidateCompanyCds OR template.COMPANY_CD = '')";
        private const string FallbackTemplateCompanyMatchSql = "(template.COMPANY_CD IN ('0001', '0000') OR template.COMPANY_CD = '')";
        private const string TemplateCompanyPriorityOrderSql = """
            ORDER BY CASE
                WHEN template.COMPANY_CD = @CompanyCd THEN 0
                WHEN template.COMPANY_CD = '0001' THEN 1
                WHEN template.COMPANY_CD = '0000' THEN 2
                ELSE 3
            END,
            template.ID
            """;

        private static readonly IReadOnlyDictionary<string, ReportTemplateDefinition> TemplateDefinitions =
            new Dictionary<string, ReportTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["B03_DN_TT"] = new("B03_DN_TT", "report_b03dntt", "GL_FS_CASHFLOW", HasCashAccountConfig: true, HasDirectRules: true, HasDualFormulas: false),
                ["GL_CASHFLOW_B03DN_TT"] = new("B03_DN_TT", "report_b03dntt", "GL_FS_CASHFLOW", HasCashAccountConfig: true, HasDirectRules: true, HasDualFormulas: false),
                ["B03_DN_GT"] = new("B03_DN_GT", "report_b03dngt", "GL_FS_CASHFLOW", HasCashAccountConfig: true, HasDirectRules: false, HasDualFormulas: false),
                ["GL_CASHFLOW_B03DN_GT"] = new("B03_DN_GT", "report_b03dngt", "GL_FS_CASHFLOW", HasCashAccountConfig: true, HasDirectRules: false, HasDualFormulas: false),
                ["B01_DN"] = new("B01_DN", "report_b01dn", "GL_FS_BALANCE", HasCashAccountConfig: false, HasDirectRules: false, HasDualFormulas: false),
                ["GL_BALANCE_SHEET_B01DN"] = new("B01_DN", "report_b01dn", "GL_FS_BALANCE", HasCashAccountConfig: false, HasDirectRules: false, HasDualFormulas: false),
                ["B02_DN"] = new("B02_DN", "report_b02dn", "GL_FS_PL", HasCashAccountConfig: false, HasDirectRules: false, HasDualFormulas: false),
                ["GL_PROFIT_LOSS_B02DN"] = new("B02_DN", "report_b02dn", "GL_FS_PL", HasCashAccountConfig: false, HasDirectRules: false, HasDualFormulas: false),
                ["GL_PROFIT_LOSS_B02DNTT"] = new("B02_DN", "report_b02dn", "GL_FS_PL", HasCashAccountConfig: false, HasDirectRules: false, HasDualFormulas: false),
                ["GTGT_01"] = new("GTGT_01", "report_gtgt01", "VAT_DECLARATION", HasCashAccountConfig: false, HasDirectRules: false, HasDualFormulas: true),
                ["TAX_VAT_DECLARATION"] = new("GTGT_01", "report_gtgt01", "VAT_DECLARATION", HasCashAccountConfig: false, HasDirectRules: false, HasDualFormulas: true),
            };

        private static readonly Regex BasicFormulaCharacters = new(
            @"^[A-Za-z0-9_\.\s\+\-;=]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly DapperExecutor _db;
        private readonly ILogger<CashFlowFormulaOptionRepository> _logger;

        public CashFlowFormulaOptionRepository(
            DapperExecutor db,
            ILogger<CashFlowFormulaOptionRepository> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CashFlowFormulaOptionsDto> GetAsync(
            string companyCd,
            string? reportCode,
            string? reportVersion,
            CancellationToken cancellationToken = default)
        {
            var normalizedCompanyCd = NormalizeRequired(companyCd, "COMPANY_CD", 20);
            var definition = ResolveTemplateDefinition(reportCode);
            var normalizedVersion = NormalizeReportVersion(reportVersion);

            using var connection = _db.GetOpenConnection(Net_DB.Net_DB_Company);
            var result = await LoadAsync(
                connection,
                null,
                normalizedCompanyCd,
                definition,
                normalizedVersion,
                cancellationToken);

            _logger.LogInformation(
                "CashFlow formula options loaded: company={CompanyCd}, report={ReportCode}, version={ReportVersion}, source={SourceCompanyCd}, rows={RowCount}",
                normalizedCompanyCd,
                definition.ReportCode,
                normalizedVersion,
                result.SOURCE_COMPANY_CD,
                result.ROWS.Count);

            if (result.ROWS.Count == 0)
            {
                await LogEmptyTemplateDiagnosticsAsync(
                    connection,
                    normalizedCompanyCd,
                    definition,
                    normalizedVersion,
                    cancellationToken);
            }

            return result;
        }

        public async Task<CashFlowFormulaOptionsDto> SaveAsync(
            string companyCd,
            SaveCashFlowFormulaOptionsRequest request,
            string userId,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.ROWS == null)
            {
                throw new ArgumentException("ROWS is required.", nameof(request));
            }

            var normalizedCompanyCd = NormalizeRequired(companyCd, "COMPANY_CD", 20);
            var normalizedUserId = NormalizeRequired(userId, "USER_ID", 50);
            var definition = ResolveTemplateDefinition(request.REPORT_CODE);
            var normalizedVersion = NormalizeReportVersion(request.REPORT_VERSION);
            var normalizedRows = NormalizeRows(request.ROWS, definition);
            if (normalizedRows.Count == 0)
            {
                // An active CONFIG row by itself wins the report procedure's source-company
                // selection. Do not let an empty draft create that incomplete template state.
                throw new ArgumentException("ROWS must contain at least one DETAIL row.", nameof(request));
            }

            if (definition.HasDualFormulas)
            {
                ValidateDualFormulaReferences(normalizedRows);
            }
            else
            {
                ValidateFormulaReferences(normalizedRows);
            }

            var suppliedCashAccountPrefixes = NormalizeCashAccountPrefixes(request.CASH_ACCOUNT_PREFIXES);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await ApplyDraftInTransactionAsync(
                    session.Connection,
                    session.Transaction,
                    normalizedCompanyCd,
                    definition,
                    normalizedVersion,
                    normalizedRows,
                    suppliedCashAccountPrefixes,
                    normalizedUserId,
                    cancellationToken);

                session.Commit();
            }
            catch
            {
                session.Rollback();
                throw;
            }

            return await GetAsync(
                normalizedCompanyCd,
                definition.ReportCode,
                normalizedVersion,
                cancellationToken);
        }

        public async Task<FormulaOptionPreviewDto> PreviewDraftAsync(
            string companyCd,
            PreviewCashFlowFormulaOptionsRequest request,
            string userId,
            IConfiguredReportService configuredReportService,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.ROWS == null)
            {
                throw new ArgumentException("ROWS is required.", nameof(request));
            }

            if (configuredReportService == null)
            {
                throw new ArgumentNullException(nameof(configuredReportService));
            }

            var normalizedCompanyCd = NormalizeRequired(companyCd, "COMPANY_CD", 20);
            var normalizedUserId = NormalizeRequired(userId, "USER_ID", 50);
            var definition = ResolveTemplateDefinition(request.REPORT_CODE);
            var normalizedVersion = NormalizeReportVersion(request.REPORT_VERSION);
            var normalizedRows = NormalizeRows(request.ROWS, definition);
            if (normalizedRows.Count == 0)
            {
                throw new ArgumentException("ROWS must contain at least one DETAIL row.", nameof(request));
            }

            if (definition.HasDualFormulas)
            {
                ValidateDualFormulaReferences(normalizedRows);
            }
            else
            {
                ValidateFormulaReferences(normalizedRows);
            }

            var previewReportCode = Common.NormalizeNullableText(request.PreviewReportCode)
                ?? Common.NormalizeNullableText(request.REPORT_CODE);
            if (string.IsNullOrWhiteSpace(previewReportCode))
            {
                throw new ArgumentException("PreviewReportCode is required.", nameof(request));
            }

            var fromYmd = Common.NormalizeNullableYmdText(request.FromYmd, nameof(request.FromYmd));
            var toYmd = Common.NormalizeNullableYmdText(request.ToYmd, nameof(request.ToYmd));
            if (string.IsNullOrWhiteSpace(fromYmd) || string.IsNullOrWhiteSpace(toYmd))
            {
                throw new ArgumentException("fromYmd and toYmd are required for formula preview.");
            }
            Common.ValidateYmdRange(fromYmd, toYmd, nameof(request.FromYmd), nameof(request.ToYmd));

            var suppliedCashAccountPrefixes = NormalizeCashAccountPrefixes(request.CASH_ACCOUNT_PREFIXES);
            var query = BuildFormulaPreviewQuery(request, normalizedVersion, fromYmd, toYmd);
            var menuCode = Common.NormalizeNullableText(request.MenuCode);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                await ApplyDraftInTransactionAsync(
                    session.Connection,
                    session.Transaction,
                    normalizedCompanyCd,
                    definition,
                    normalizedVersion,
                    normalizedRows,
                    suppliedCashAccountPrefixes,
                    normalizedUserId,
                    cancellationToken);

                var dataTable = await configuredReportService.ExecuteReportDataTableInSessionAsync(
                    normalizedCompanyCd,
                    previewReportCode,
                    menuCode,
                    query,
                    session.Connection,
                    session.Transaction,
                    cancellationToken);

                return MapFormulaPreviewDto(dataTable);
            }
            finally
            {
                session.Rollback();
            }
        }

        public async Task<CashFlowFormulaOptionsDto> ResetToDefaultAsync(
            string companyCd,
            string? reportCode,
            string? reportVersion,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var normalizedCompanyCd = NormalizeRequired(companyCd, "COMPANY_CD", 20);
            var normalizedUserId = NormalizeRequired(userId, "USER_ID", 50);
            var definition = ResolveTemplateDefinition(reportCode);
            var normalizedVersion = NormalizeReportVersion(reportVersion);

            if (IsSharedDefaultTemplateCompany(normalizedCompanyCd))
            {
                throw new ArgumentException(
                    "Không thể reset mẫu mặc định dùng chung. Chỉ công ty đã tùy chỉnh mới được khôi phục về mẫu ban đầu.");
            }

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var affectedRows = await SoftDeleteCompanyTemplateRowsAsync(
                    session.Connection,
                    session.Transaction,
                    normalizedCompanyCd,
                    definition,
                    normalizedVersion,
                    normalizedUserId,
                    cancellationToken);

                session.Commit();

                _logger.LogInformation(
                    "Reset formula template {ReportCode}/{ReportVersion} for company {CompanyCd} to shared default. Soft-deleted {AffectedRows} row(s).",
                    definition.ReportCode,
                    normalizedVersion,
                    normalizedCompanyCd,
                    affectedRows);
            }
            catch
            {
                session.Rollback();
                throw;
            }

            return await GetAsync(
                normalizedCompanyCd,
                definition.ReportCode,
                normalizedVersion,
                cancellationToken);
        }

        private async Task LogEmptyTemplateDiagnosticsAsync(
            IDbConnection connection,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                SELECT
                    template.COMPANY_CD AS CompanyCd,
                    UPPER(IFNULL(template.SECTION_TYPE, '')) AS SectionType,
                    IFNULL(template.COLUMN_KEY, '') AS ColumnKey,
                    COUNT(*) AS RowCount
                FROM `{definition.TableName}` template
                WHERE template.REPORT_CODE = @ReportCode
                  AND template.REPORT_VERSION = @ReportVersion
                  AND {TemplateCompanyMatchSql}
                  AND IFNULL(template.ISDEL, '0') = '0'
                GROUP BY template.COMPANY_CD, UPPER(IFNULL(template.SECTION_TYPE, '')), IFNULL(template.COLUMN_KEY, '')
                ORDER BY template.COMPANY_CD, SectionType, ColumnKey;
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion,
                    CompanyCd = companyCd,
                    CandidateCompanyCds = BuildCandidateCompanyCds(companyCd)
                },
                cancellationToken: cancellationToken);

            var breakdown = (await connection.QueryAsync(command)).ToList();
            if (breakdown.Count == 0)
            {
                _logger.LogWarning(
                    "CashFlow formula template is empty: table={TableName}, report={ReportCode}, version={ReportVersion}, company={CompanyCd}. Run migration 20260820_seed_report_b03dn.sql against the company database.",
                    definition.TableName,
                    definition.ReportCode,
                    reportVersion,
                    companyCd);
                return;
            }

            _logger.LogWarning(
                "CashFlow formula template has no DETAIL rows: table={TableName}, report={ReportCode}, version={ReportVersion}, company={CompanyCd}, breakdown={Breakdown}",
                definition.TableName,
                definition.ReportCode,
                reportVersion,
                companyCd,
                breakdown);
        }

        private static async Task<CashFlowFormulaOptionsDto> LoadAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            var sourceCompanyCd = await ResolveEffectiveSourceCompanyCdAsync(
                connection,
                transaction,
                companyCd,
                definition,
                reportVersion,
                cancellationToken);

            List<CashFlowFormulaOptionRowDto> rows;
            if (sourceCompanyCd != null)
            {
                rows = await GetDetailRowsAsync(
                    connection,
                    transaction,
                    sourceCompanyCd,
                    definition,
                    reportVersion,
                    cancellationToken);
            }
            else
            {
                rows = new List<CashFlowFormulaOptionRowDto>();
            }

            // Company may have a CONFIG override but no DETAIL yet; still show the shared default rows.
            if (rows.Count == 0 && !IsSharedDefaultTemplateCompany(companyCd))
            {
                var sharedSourceCompanyCd = await ResolveSharedTemplateCompanyCdAsync(
                    connection,
                    transaction,
                    definition,
                    reportVersion,
                    cancellationToken);

                if (sharedSourceCompanyCd != null)
                {
                    var sharedRows = await GetDetailRowsAsync(
                        connection,
                        transaction,
                        sharedSourceCompanyCd,
                        definition,
                        reportVersion,
                        cancellationToken);

                    if (sharedRows.Count > 0)
                    {
                        sourceCompanyCd = sharedSourceCompanyCd;
                        rows = sharedRows;
                    }
                }
            }

            var cashAccountPrefixes = definition.HasCashAccountConfig
                ? await ResolveCashAccountPrefixesAsync(
                    connection,
                    transaction,
                    companyCd,
                    sourceCompanyCd,
                    definition,
                    reportVersion,
                    cancellationToken)
                : null;

            return new CashFlowFormulaOptionsDto
            {
                COMPANY_CD = companyCd,
                REPORT_CODE = definition.ReportCode,
                REPORT_VERSION = reportVersion,
                SOURCE_COMPANY_CD = sourceCompanyCd ?? companyCd,
                CASH_ACCOUNT_PREFIXES = cashAccountPrefixes ?? DefaultCashAccountPrefixes,
                ROWS = rows
            };
        }

        private static async Task<string?> ResolveSharedTemplateCompanyCdAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            return await ResolveFallbackDetailSourceCompanyCdAsync(
                connection,
                transaction,
                definition,
                reportVersion,
                cancellationToken);
        }

        private static async Task<string?> ResolveEffectiveSourceCompanyCdAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                SELECT template.COMPANY_CD
                FROM `{definition.TableName}` template
                WHERE template.REPORT_CODE = @ReportCode
                  AND template.REPORT_VERSION = @ReportVersion
                  AND {TemplateCompanyMatchSql}
                  AND UPPER(IFNULL(template.SECTION_TYPE, '')) = 'DETAIL'
                  AND IFNULL(template.ISDEL, '0') = '0'
                {TemplateCompanyPriorityOrderSql}
                LIMIT 1;
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    CompanyCd = companyCd,
                    CandidateCompanyCds = BuildCandidateCompanyCds(companyCd),
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

            var sourceCompanyCd = await connection.QueryFirstOrDefaultAsync<string?>(command);
            return ReadResolvedTemplateCompanyCd(sourceCompanyCd);
        }

        private static async Task<string?> ResolveFallbackDetailSourceCompanyCdAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            var forUpdateSql = transaction == null ? string.Empty : "\n                FOR UPDATE;";
            var sql = $"""
                SELECT template.COMPANY_CD
                FROM `{definition.TableName}` template
                WHERE template.REPORT_CODE = @ReportCode
                  AND template.REPORT_VERSION = @ReportVersion
                  AND {FallbackTemplateCompanyMatchSql}
                  AND UPPER(IFNULL(template.SECTION_TYPE, '')) = 'DETAIL'
                  AND IFNULL(template.ISDEL, '0') = '0'
                ORDER BY CASE
                    WHEN template.COMPANY_CD = '0001' THEN 1
                    WHEN template.COMPANY_CD = '0000' THEN 2
                    ELSE 3
                END,
                template.ID
                LIMIT 1{forUpdateSql}
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

            var sourceCompanyCd = await connection.QueryFirstOrDefaultAsync<string?>(command);
            return ReadResolvedTemplateCompanyCd(sourceCompanyCd);
        }

        private static async Task<bool> HasActiveDetailRowsAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                SELECT EXISTS(
                    SELECT 1
                    FROM `{definition.TableName}` template
                    WHERE template.COMPANY_CD = @CompanyCd
                      AND template.REPORT_CODE = @ReportCode
                      AND template.REPORT_VERSION = @ReportVersion
                      AND UPPER(IFNULL(template.SECTION_TYPE, '')) = 'DETAIL'
                      AND IFNULL(template.ISDEL, '0') = '0'
                );
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    CompanyCd = companyCd,
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

            return await connection.QuerySingleAsync<bool>(command);
        }

        private static async Task<List<CashFlowFormulaOptionRowDto>> GetDetailRowsAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            // Do not hard-filter COLUMN_KEY here. B01/B02 layout templates often store
            // one DETAIL line with COLUMN_KEY other than '' / ITEM_NAME (e.g. END_YEAR).
            // Prefer line/label columns when deduping below.
            var sql = definition.HasDirectRules
                ? $"""
                SELECT
                    ID,
                    ITEM_KEY,
                    ITEM_CODE,
                    CAPTION,
                    LABEL_TEXT,
                    COLUMN_KEY,
                    ELEMENT_TYPE,
                    LEVEL_NO,
                    DATA_SOURCE_TYPE,
                    FORMULA_EXPR,
                    DIRECT_RULE_FLOW_SIGN,
                    DIRECT_RULE_ACC_PREFIX,
                    DIRECT_RULE_PRIORITY,
                    DIRECT_RULE_FLOW_SIGN_2,
                    DIRECT_RULE_ACC_PREFIX_2,
                    DIRECT_RULE_PRIORITY_2,
                    ACCOUNT_RULE,
                    CALC_METHOD,
                    SORT_ORDER,
                    FONT_BOLD,
                    FONT_ITALIC,
                    IS_VISIBLE
                FROM `{definition.TableName}`
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'DETAIL'
                  AND IFNULL(ISDEL, '0') = '0'
                ORDER BY SORT_ORDER, ID;
                """
                : definition.HasDualFormulas
                ? $"""
                SELECT
                    ID,
                    ITEM_KEY,
                    ITEM_CODE,
                    CAPTION,
                    LABEL_TEXT,
                    COLUMN_KEY,
                    ELEMENT_TYPE,
                    LEVEL_NO,
                    DATA_SOURCE_TYPE,
                    FORMULA_EXPR,
                    CODE_NO1,
                    CODE_NO2,
                    FORMULA_NO1,
                    FORMULA_NO2,
                    CALC_METHOD_NO1,
                    CALC_METHOD_NO2,
                    ACCOUNT_RULE,
                    CALC_METHOD,
                    SORT_ORDER,
                    FONT_BOLD,
                    FONT_ITALIC,
                    IS_VISIBLE
                FROM `{definition.TableName}`
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'DETAIL'
                  AND IFNULL(ISDEL, '0') = '0'
                ORDER BY SORT_ORDER, ID;
                """
                : $"""
                SELECT
                    ID,
                    ITEM_KEY,
                    ITEM_CODE,
                    CAPTION,
                    LABEL_TEXT,
                    COLUMN_KEY,
                    ELEMENT_TYPE,
                    LEVEL_NO,
                    DATA_SOURCE_TYPE,
                    FORMULA_EXPR,
                    ACCOUNT_RULE,
                    CALC_METHOD,
                    SORT_ORDER,
                    FONT_BOLD,
                    FONT_ITALIC,
                    IS_VISIBLE
                FROM `{definition.TableName}`
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'DETAIL'
                  AND IFNULL(ISDEL, '0') = '0'
                ORDER BY SORT_ORDER, ID;
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    CompanyCd = companyCd,
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

            var rows = await connection.QueryAsync<CashFlowFormulaOptionRowDto>(command);
            var normalizedRows = rows
                .Select(row => NormalizeLoadedRow(row, definition.ReportCode))
                .ToList();
            return DedupeDetailRows(normalizedRows, definition.HasDualFormulas);
        }

        private static List<CashFlowFormulaOptionRowDto> DedupeDetailRows(
            IEnumerable<CashFlowFormulaOptionRowDto> rows,
            bool hasDualFormulas)
        {
            // Unique key includes COLUMN_KEY, so one ITEM_KEY can appear more than once.
            // The editor is line-based: keep a single row per ITEM_KEY.
            // GTGT reuses display ITEM_CODE (1, 2, ...) across sections — never collapse by ITEM_CODE.
            var byItemKey = rows
                .GroupBy(row => row.ITEM_KEY, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(PreferFormulaEditorColumnRank).ThenBy(row => row.ID).First());

            if (hasDualFormulas)
            {
                return byItemKey
                    .OrderBy(row => row.SORT_ORDER)
                    .ThenBy(row => row.ID)
                    .ToList();
            }

            return byItemKey
                .GroupBy(
                    row => string.IsNullOrWhiteSpace(row.ITEM_CODE)
                        ? $"KEY:{row.ITEM_KEY}"
                        : $"CODE:{row.ITEM_CODE}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(row => row.SORT_ORDER).ThenBy(row => row.ID).First())
                .OrderBy(row => row.SORT_ORDER)
                .ThenBy(row => row.ID)
                .ToList();
        }

        private static int PreferFormulaEditorColumnRank(CashFlowFormulaOptionRowDto row)
        {
            var columnKey = (row.COLUMN_KEY ?? string.Empty).Trim().ToUpperInvariant();
            var rank = columnKey switch
            {
                "" or "ITEM_NAME" => 0,
                "ITEM_CODE" => 1,
                "NOTE" => 2,
                _ => 3
            };

            // Prefer the cell that actually carries formula/account data when COLUMN_KEY varies.
            var hasRule = !string.IsNullOrWhiteSpace(row.ACCOUNT_RULE)
                || !string.IsNullOrWhiteSpace(row.FORMULA_EXPR)
                || !string.IsNullOrWhiteSpace(row.FORMULA_NO1)
                || !string.IsNullOrWhiteSpace(row.FORMULA_NO2)
                || !string.IsNullOrWhiteSpace(row.DIRECT_RULE_ACC_PREFIX);
            if (!hasRule)
            {
                rank += 10;
            }

            return rank;
        }

        private static async Task<string?> ResolveCashAccountPrefixesAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            string companyCd,
            string? detailSourceCompanyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            foreach (var candidateCompanyCd in BuildCashAccountPrefixCandidates(companyCd, detailSourceCompanyCd))
            {
                var prefixes = await GetCashAccountPrefixesAsync(
                    connection,
                    transaction,
                    candidateCompanyCd,
                    definition,
                    reportVersion,
                    cancellationToken);
                if (prefixes != null)
                {
                    return prefixes;
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildCashAccountPrefixCandidates(string companyCd, string? detailSourceCompanyCd)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in new[] { companyCd, detailSourceCompanyCd, "0001", "0000", BlankTemplateCompanyCd })
            {
                if (candidate == null || !seen.Add(candidate))
                {
                    continue;
                }

                yield return candidate;
            }
        }

        private static string[] BuildCandidateCompanyCds(string companyCd)
        {
            var candidates = new[] { companyCd, "0001", "0000" };
            return candidates
                .Select(candidate => Common.NormalizeNullableText(candidate))
                .Where(candidate => candidate != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(candidate => candidate!)
                .ToArray();
        }

        private static async Task<string?> GetCashAccountPrefixesAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            CancellationToken cancellationToken)
        {
            var sql = $"""
                SELECT ACCOUNT_RULE
                FROM `{definition.TableName}`
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND ITEM_KEY = @ItemKey
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'CONFIG'
                  AND IFNULL(ISDEL, '0') = '0'
                ORDER BY ID DESC
                LIMIT 1;
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    CompanyCd = companyCd,
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion,
                    ItemKey = CashAccountPrefixesItemKey
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

            var prefixes = await connection.QueryFirstOrDefaultAsync<string?>(command);
            // Existing company templates may predate the editor limits. Reading them must
            // remain permissive; validation is applied only to a new PUT value.
            return Common.NormalizeNullableText(prefixes);
        }

        private static CashFlowFormulaOptionRowDto NormalizeLoadedRow(CashFlowFormulaOptionRowDto row, string reportCode)
        {
            row.ITEM_KEY = Common.NormalizeNullableText(row.ITEM_KEY) ?? string.Empty;
            row.ITEM_CODE = Common.NormalizeNullableText(row.ITEM_CODE);
            row.CAPTION = Common.NormalizeNullableText(row.CAPTION);
            row.LABEL_TEXT = Common.NormalizeNullableText(row.LABEL_TEXT);
            row.COLUMN_KEY = Common.NormalizeNullableText(row.COLUMN_KEY) ?? string.Empty;
            row.ELEMENT_TYPE = Common.NormalizeNullableText(row.ELEMENT_TYPE) ?? "DETAIL";
            row.DATA_SOURCE_TYPE = Common.NormalizeNullableText(row.DATA_SOURCE_TYPE);
            if (IsAccountRuleReport(reportCode)
                && string.Equals(row.DATA_SOURCE_TYPE, "PROCEDURE", StringComparison.OrdinalIgnoreCase))
            {
                row.DATA_SOURCE_TYPE = "ACCOUNT_RULE";
            }
            row.FORMULA_EXPR = Common.NormalizeNullableText(row.FORMULA_EXPR);
            row.CODE_NO1 = Common.NormalizeNullableText(row.CODE_NO1);
            row.CODE_NO2 = Common.NormalizeNullableText(row.CODE_NO2);
            row.FORMULA_NO1 = Common.NormalizeNullableText(row.FORMULA_NO1);
            row.FORMULA_NO2 = Common.NormalizeNullableText(row.FORMULA_NO2);
            row.CALC_METHOD_NO1 = Common.NormalizeNullableText(row.CALC_METHOD_NO1);
            row.CALC_METHOD_NO2 = Common.NormalizeNullableText(row.CALC_METHOD_NO2);
            row.CALC_METHOD = Common.NormalizeNullableText(row.CALC_METHOD);
            row.DIRECT_RULE_ACC_PREFIX = Common.NormalizeNullableText(row.DIRECT_RULE_ACC_PREFIX);
            row.DIRECT_RULE_ACC_PREFIX_2 = Common.NormalizeNullableText(row.DIRECT_RULE_ACC_PREFIX_2);
            row.ACCOUNT_RULE = Common.NormalizeNullableText(row.ACCOUNT_RULE);
            row.FONT_BOLD = NormalizeLoadedFlag(row.FONT_BOLD, "0");
            row.FONT_ITALIC = NormalizeLoadedFlag(row.FONT_ITALIC, "0");
            row.IS_VISIBLE = NormalizeLoadedFlag(row.IS_VISIBLE, "1");
            row.FORMULA_DISPLAY = ReportFormulaDisplayBuilder.Build(row, reportCode);
            return row;
        }

        private static string NormalizeLoadedFlag(string? value, string defaultValue)
        {
            var normalized = Common.NormalizeNullableText(value);
            return normalized?.ToUpperInvariant() switch
            {
                "1" or "Y" or "YES" or "TRUE" => "1",
                "0" or "N" or "NO" or "FALSE" => "0",
                _ => defaultValue
            };
        }

        private static async Task CopyActiveRowsAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string targetCompanyCd,
            string sourceCompanyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            string userId,
            CancellationToken cancellationToken)
        {
            // B01/B02 templates do not have DIRECT_RULE* columns. Copy only columns that exist.
            // GTGT templates use CODE_NO* / FORMULA_NO* / CALC_METHOD_NO* instead of a single FORMULA_EXPR.
            var sql = definition.HasDirectRules
                ? $"""
                INSERT INTO `{definition.TableName}`
                (
                    COMPANY_CD, REPORT_CODE, REPORT_VERSION, SECTION_TYPE, ELEMENT_TYPE,
                    AREA_CODE, ROW_NO, COL_NO, ROW_SPAN, COL_SPAN, ITEM_KEY, PARENT_KEY,
                    LEVEL_NO, ITEM_CODE, CAPTION, COLUMN_KEY, LABEL_TEXT,
                    DATA_SOURCE_TYPE, CALC_METHOD, FORMULA_EXPR, ACCOUNT_RULE, DIRECT_RULE,
                    DIRECT_RULE_FLOW_SIGN, DIRECT_RULE_ACC_PREFIX, DIRECT_RULE_PRIORITY,
                    DIRECT_RULE_FLOW_SIGN_2, DIRECT_RULE_ACC_PREFIX_2, DIRECT_RULE_PRIORITY_2,
                    VALUE_PERIOD, UNIT_DIVISOR, DATA_TYPE, FORMAT_CODE, NEGATIVE_FORMAT,
                    WIDTH, ALIGN, FONT_BOLD, FONT_ITALIC, IS_VISIBLE, SORT_ORDER, ISDEL,
                    CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
                )
                SELECT
                    @TargetCompanyCd, @ReportCode, @ReportVersion, source.SECTION_TYPE, source.ELEMENT_TYPE,
                    source.AREA_CODE, source.ROW_NO, source.COL_NO, source.ROW_SPAN, source.COL_SPAN,
                    source.ITEM_KEY, source.PARENT_KEY, source.LEVEL_NO, source.ITEM_CODE, source.CAPTION,
                    source.COLUMN_KEY, source.LABEL_TEXT, source.DATA_SOURCE_TYPE,
                    source.CALC_METHOD, source.FORMULA_EXPR, source.ACCOUNT_RULE, source.DIRECT_RULE,
                    source.DIRECT_RULE_FLOW_SIGN, source.DIRECT_RULE_ACC_PREFIX, source.DIRECT_RULE_PRIORITY,
                    source.DIRECT_RULE_FLOW_SIGN_2, source.DIRECT_RULE_ACC_PREFIX_2, source.DIRECT_RULE_PRIORITY_2,
                    source.VALUE_PERIOD, source.UNIT_DIVISOR, source.DATA_TYPE, source.FORMAT_CODE,
                    source.NEGATIVE_FORMAT, source.WIDTH, source.ALIGN, source.FONT_BOLD, source.FONT_ITALIC,
                    source.IS_VISIBLE, source.SORT_ORDER, '0', NOW(), @UserId, NOW(), @UserId
                FROM `{definition.TableName}` source
                WHERE source.COMPANY_CD = @SourceCompanyCd
                  AND source.REPORT_CODE = @ReportCode
                  AND source.REPORT_VERSION = @ReportVersion
                  AND IFNULL(source.ISDEL, '0') = '0'
                ON DUPLICATE KEY UPDATE
                    SECTION_TYPE = VALUES(SECTION_TYPE),
                    ELEMENT_TYPE = VALUES(ELEMENT_TYPE),
                    AREA_CODE = VALUES(AREA_CODE),
                    ROW_NO = VALUES(ROW_NO),
                    COL_NO = VALUES(COL_NO),
                    ROW_SPAN = VALUES(ROW_SPAN),
                    COL_SPAN = VALUES(COL_SPAN),
                    PARENT_KEY = VALUES(PARENT_KEY),
                    LEVEL_NO = VALUES(LEVEL_NO),
                    ITEM_CODE = VALUES(ITEM_CODE),
                    CAPTION = VALUES(CAPTION),
                    LABEL_TEXT = VALUES(LABEL_TEXT),
                    DATA_SOURCE_TYPE = VALUES(DATA_SOURCE_TYPE),
                    CALC_METHOD = VALUES(CALC_METHOD),
                    FORMULA_EXPR = VALUES(FORMULA_EXPR),
                    ACCOUNT_RULE = VALUES(ACCOUNT_RULE),
                    DIRECT_RULE = VALUES(DIRECT_RULE),
                    DIRECT_RULE_FLOW_SIGN = VALUES(DIRECT_RULE_FLOW_SIGN),
                    DIRECT_RULE_ACC_PREFIX = VALUES(DIRECT_RULE_ACC_PREFIX),
                    DIRECT_RULE_PRIORITY = VALUES(DIRECT_RULE_PRIORITY),
                    DIRECT_RULE_FLOW_SIGN_2 = VALUES(DIRECT_RULE_FLOW_SIGN_2),
                    DIRECT_RULE_ACC_PREFIX_2 = VALUES(DIRECT_RULE_ACC_PREFIX_2),
                    DIRECT_RULE_PRIORITY_2 = VALUES(DIRECT_RULE_PRIORITY_2),
                    VALUE_PERIOD = VALUES(VALUE_PERIOD),
                    UNIT_DIVISOR = VALUES(UNIT_DIVISOR),
                    DATA_TYPE = VALUES(DATA_TYPE),
                    FORMAT_CODE = VALUES(FORMAT_CODE),
                    NEGATIVE_FORMAT = VALUES(NEGATIVE_FORMAT),
                    WIDTH = VALUES(WIDTH),
                    ALIGN = VALUES(ALIGN),
                    FONT_BOLD = VALUES(FONT_BOLD),
                    FONT_ITALIC = VALUES(FONT_ITALIC),
                    IS_VISIBLE = VALUES(IS_VISIBLE),
                    SORT_ORDER = VALUES(SORT_ORDER),
                    ISDEL = '0',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId;
                """
                : definition.HasDualFormulas
                ? $"""
                INSERT INTO `{definition.TableName}`
                (
                    COMPANY_CD, REPORT_CODE, REPORT_VERSION, SECTION_TYPE, ELEMENT_TYPE,
                    AREA_CODE, ROW_NO, COL_NO, ROW_SPAN, COL_SPAN, ITEM_KEY, PARENT_KEY,
                    LEVEL_NO, ITEM_CODE, CAPTION, COLUMN_KEY, LABEL_TEXT,
                    CODE_NO1, CODE_NO2, DATA_SOURCE_TYPE, CALC_METHOD, CALC_METHOD_NO1, CALC_METHOD_NO2,
                    FORMULA_EXPR, FORMULA_NO1, FORMULA_NO2, ACCOUNT_RULE,
                    VALUE_PERIOD, UNIT_DIVISOR, DATA_TYPE, FORMAT_CODE, NEGATIVE_FORMAT,
                    WIDTH, ALIGN, FONT_BOLD, FONT_ITALIC, IS_VISIBLE, SORT_ORDER, ISDEL,
                    CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
                )
                SELECT
                    @TargetCompanyCd, @ReportCode, @ReportVersion, source.SECTION_TYPE, source.ELEMENT_TYPE,
                    source.AREA_CODE, source.ROW_NO, source.COL_NO, source.ROW_SPAN, source.COL_SPAN,
                    source.ITEM_KEY, source.PARENT_KEY, source.LEVEL_NO, source.ITEM_CODE, source.CAPTION,
                    source.COLUMN_KEY, source.LABEL_TEXT,
                    source.CODE_NO1, source.CODE_NO2, source.DATA_SOURCE_TYPE, source.CALC_METHOD,
                    source.CALC_METHOD_NO1, source.CALC_METHOD_NO2, source.FORMULA_EXPR,
                    source.FORMULA_NO1, source.FORMULA_NO2, source.ACCOUNT_RULE,
                    source.VALUE_PERIOD, source.UNIT_DIVISOR, source.DATA_TYPE, source.FORMAT_CODE,
                    source.NEGATIVE_FORMAT, source.WIDTH, source.ALIGN, source.FONT_BOLD, source.FONT_ITALIC,
                    source.IS_VISIBLE, source.SORT_ORDER, '0', NOW(), @UserId, NOW(), @UserId
                FROM `{definition.TableName}` source
                WHERE source.COMPANY_CD = @SourceCompanyCd
                  AND source.REPORT_CODE = @ReportCode
                  AND source.REPORT_VERSION = @ReportVersion
                  AND IFNULL(source.ISDEL, '0') = '0'
                ON DUPLICATE KEY UPDATE
                    SECTION_TYPE = VALUES(SECTION_TYPE),
                    ELEMENT_TYPE = VALUES(ELEMENT_TYPE),
                    AREA_CODE = VALUES(AREA_CODE),
                    ROW_NO = VALUES(ROW_NO),
                    COL_NO = VALUES(COL_NO),
                    ROW_SPAN = VALUES(ROW_SPAN),
                    COL_SPAN = VALUES(COL_SPAN),
                    PARENT_KEY = VALUES(PARENT_KEY),
                    LEVEL_NO = VALUES(LEVEL_NO),
                    ITEM_CODE = VALUES(ITEM_CODE),
                    CAPTION = VALUES(CAPTION),
                    LABEL_TEXT = VALUES(LABEL_TEXT),
                    CODE_NO1 = VALUES(CODE_NO1),
                    CODE_NO2 = VALUES(CODE_NO2),
                    DATA_SOURCE_TYPE = VALUES(DATA_SOURCE_TYPE),
                    CALC_METHOD = VALUES(CALC_METHOD),
                    CALC_METHOD_NO1 = VALUES(CALC_METHOD_NO1),
                    CALC_METHOD_NO2 = VALUES(CALC_METHOD_NO2),
                    FORMULA_EXPR = VALUES(FORMULA_EXPR),
                    FORMULA_NO1 = VALUES(FORMULA_NO1),
                    FORMULA_NO2 = VALUES(FORMULA_NO2),
                    ACCOUNT_RULE = VALUES(ACCOUNT_RULE),
                    VALUE_PERIOD = VALUES(VALUE_PERIOD),
                    UNIT_DIVISOR = VALUES(UNIT_DIVISOR),
                    DATA_TYPE = VALUES(DATA_TYPE),
                    FORMAT_CODE = VALUES(FORMAT_CODE),
                    NEGATIVE_FORMAT = VALUES(NEGATIVE_FORMAT),
                    WIDTH = VALUES(WIDTH),
                    ALIGN = VALUES(ALIGN),
                    FONT_BOLD = VALUES(FONT_BOLD),
                    FONT_ITALIC = VALUES(FONT_ITALIC),
                    IS_VISIBLE = VALUES(IS_VISIBLE),
                    SORT_ORDER = VALUES(SORT_ORDER),
                    ISDEL = '0',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId;
                """
                : $"""
                INSERT INTO `{definition.TableName}`
                (
                    COMPANY_CD, REPORT_CODE, REPORT_VERSION, SECTION_TYPE, ELEMENT_TYPE,
                    AREA_CODE, ROW_NO, COL_NO, ROW_SPAN, COL_SPAN, ITEM_KEY, PARENT_KEY,
                    LEVEL_NO, ITEM_CODE, CAPTION, COLUMN_KEY, LABEL_TEXT,
                    DATA_SOURCE_TYPE, CALC_METHOD, FORMULA_EXPR, ACCOUNT_RULE,
                    VALUE_PERIOD, UNIT_DIVISOR, DATA_TYPE, FORMAT_CODE, NEGATIVE_FORMAT,
                    WIDTH, ALIGN, FONT_BOLD, FONT_ITALIC, IS_VISIBLE, SORT_ORDER, ISDEL,
                    CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
                )
                SELECT
                    @TargetCompanyCd, @ReportCode, @ReportVersion, source.SECTION_TYPE, source.ELEMENT_TYPE,
                    source.AREA_CODE, source.ROW_NO, source.COL_NO, source.ROW_SPAN, source.COL_SPAN,
                    source.ITEM_KEY, source.PARENT_KEY, source.LEVEL_NO, source.ITEM_CODE, source.CAPTION,
                    source.COLUMN_KEY, source.LABEL_TEXT, source.DATA_SOURCE_TYPE,
                    source.CALC_METHOD, source.FORMULA_EXPR, source.ACCOUNT_RULE,
                    source.VALUE_PERIOD, source.UNIT_DIVISOR, source.DATA_TYPE, source.FORMAT_CODE,
                    source.NEGATIVE_FORMAT, source.WIDTH, source.ALIGN, source.FONT_BOLD, source.FONT_ITALIC,
                    source.IS_VISIBLE, source.SORT_ORDER, '0', NOW(), @UserId, NOW(), @UserId
                FROM `{definition.TableName}` source
                WHERE source.COMPANY_CD = @SourceCompanyCd
                  AND source.REPORT_CODE = @ReportCode
                  AND source.REPORT_VERSION = @ReportVersion
                  AND IFNULL(source.ISDEL, '0') = '0'
                ON DUPLICATE KEY UPDATE
                    SECTION_TYPE = VALUES(SECTION_TYPE),
                    ELEMENT_TYPE = VALUES(ELEMENT_TYPE),
                    AREA_CODE = VALUES(AREA_CODE),
                    ROW_NO = VALUES(ROW_NO),
                    COL_NO = VALUES(COL_NO),
                    ROW_SPAN = VALUES(ROW_SPAN),
                    COL_SPAN = VALUES(COL_SPAN),
                    PARENT_KEY = VALUES(PARENT_KEY),
                    LEVEL_NO = VALUES(LEVEL_NO),
                    ITEM_CODE = VALUES(ITEM_CODE),
                    CAPTION = VALUES(CAPTION),
                    LABEL_TEXT = VALUES(LABEL_TEXT),
                    DATA_SOURCE_TYPE = VALUES(DATA_SOURCE_TYPE),
                    CALC_METHOD = VALUES(CALC_METHOD),
                    FORMULA_EXPR = VALUES(FORMULA_EXPR),
                    ACCOUNT_RULE = VALUES(ACCOUNT_RULE),
                    VALUE_PERIOD = VALUES(VALUE_PERIOD),
                    UNIT_DIVISOR = VALUES(UNIT_DIVISOR),
                    DATA_TYPE = VALUES(DATA_TYPE),
                    FORMAT_CODE = VALUES(FORMAT_CODE),
                    NEGATIVE_FORMAT = VALUES(NEGATIVE_FORMAT),
                    WIDTH = VALUES(WIDTH),
                    ALIGN = VALUES(ALIGN),
                    FONT_BOLD = VALUES(FONT_BOLD),
                    FONT_ITALIC = VALUES(FONT_ITALIC),
                    IS_VISIBLE = VALUES(IS_VISIBLE),
                    SORT_ORDER = VALUES(SORT_ORDER),
                    ISDEL = '0',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId;
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    TargetCompanyCd = targetCompanyCd,
                    SourceCompanyCd = sourceCompanyCd,
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion,
                    UserId = userId
                },
                transaction: transaction,
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }

        private static async Task UpsertCashAccountPrefixesAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            string cashAccountPrefixes,
            string userId,
            CancellationToken cancellationToken)
        {
            var updateSql = $"""
                UPDATE `{definition.TableName}`
                SET
                    ELEMENT_TYPE = 'CONFIG',
                    ITEM_CODE = NULL,
                    CAPTION = 'Cash account prefixes',
                    LABEL_TEXT = 'CASH_ACCOUNT_PREFIXES',
                    DATA_SOURCE_TYPE = 'CONFIG',
                    FORMULA_EXPR = NULL,
                    ACCOUNT_RULE = @CashAccountPrefixes,
                    DIRECT_RULE_FLOW_SIGN = NULL,
                    DIRECT_RULE_ACC_PREFIX = NULL,
                    DIRECT_RULE_PRIORITY = NULL,
                    DIRECT_RULE_FLOW_SIGN_2 = NULL,
                    DIRECT_RULE_ACC_PREFIX_2 = NULL,
                    DIRECT_RULE_PRIORITY_2 = NULL,
                    ISDEL = '0',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND ITEM_KEY = @ItemKey
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'CONFIG';
                """;

            var parameters = new
            {
                CompanyCd = companyCd,
                ReportCode = definition.ReportCode,
                ReportVersion = reportVersion,
                ItemKey = CashAccountPrefixesItemKey,
                CashAccountPrefixes = cashAccountPrefixes,
                UserId = userId
            };

            var update = new CommandDefinition(
                updateSql,
                parameters,
                transaction: transaction,
                cancellationToken: cancellationToken);
            var updatedRows = await connection.ExecuteAsync(update);
            if (updatedRows > 0)
            {
                return;
            }

            var insertSql = $"""
                INSERT INTO `{definition.TableName}`
                (
                    COMPANY_CD, REPORT_CODE, REPORT_VERSION, SECTION_TYPE, ELEMENT_TYPE,
                    AREA_CODE, ROW_NO, COL_NO, ROW_SPAN, COL_SPAN, ITEM_KEY, PARENT_KEY,
                    LEVEL_NO, ITEM_CODE, CAPTION, COLUMN_KEY, LABEL_TEXT,
                    DATA_SOURCE_TYPE, CALC_METHOD, FORMULA_EXPR, ACCOUNT_RULE, DIRECT_RULE,
                    VALUE_PERIOD, UNIT_DIVISOR, DATA_TYPE, FORMAT_CODE, NEGATIVE_FORMAT,
                    WIDTH, ALIGN, FONT_BOLD, FONT_ITALIC, IS_VISIBLE, SORT_ORDER, ISDEL,
                    CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
                )
                VALUES
                (
                    @CompanyCd, @ReportCode, @ReportVersion, 'CONFIG', 'CONFIG',
                    NULL, 0, 0, 1, 1, @ItemKey, NULL,
                    0, NULL, 'Cash account prefixes', NULL, 'CASH_ACCOUNT_PREFIXES', '',
                    'CONFIG', NULL, NULL, @CashAccountPrefixes, NULL,
                    NULL, 1, NULL, NULL, NULL,
                    NULL, NULL, '0', '0', '1', 0, '0',
                    NOW(), @UserId, NOW(), @UserId
                );
                """;

            var insert = new CommandDefinition(
                insertSql,
                parameters,
                transaction: transaction,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(insert);
        }

        private static async Task<int> UpdateDetailRowAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            NormalizedRow row,
            string userId,
            CancellationToken cancellationToken)
        {
            var sql = definition.HasDirectRules
                ? $"""
                UPDATE `{definition.TableName}`
                SET
                    ELEMENT_TYPE = @ElementType,
                    LEVEL_NO = @LevelNo,
                    ITEM_CODE = @ItemCode,
                    CAPTION = @Caption,
                    LABEL_TEXT = @LabelText,
                    DATA_SOURCE_TYPE = @DataSourceType,
                    FORMULA_EXPR = @FormulaExpr,
                    DIRECT_RULE_FLOW_SIGN = @DirectRuleFlowSign,
                    DIRECT_RULE_ACC_PREFIX = @DirectRuleAccPrefix,
                    DIRECT_RULE_PRIORITY = @DirectRulePriority,
                    DIRECT_RULE_FLOW_SIGN_2 = @DirectRuleFlowSign2,
                    DIRECT_RULE_ACC_PREFIX_2 = @DirectRuleAccPrefix2,
                    DIRECT_RULE_PRIORITY_2 = @DirectRulePriority2,
                    ACCOUNT_RULE = @AccountRule,
                    SORT_ORDER = @SortOrder,
                    FONT_BOLD = @FontBold,
                    FONT_ITALIC = @FontItalic,
                    IS_VISIBLE = @IsVisible,
                    ISDEL = '0',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'DETAIL'
                  AND UPPER(ITEM_KEY) = UPPER(@ItemKey);
                """
                : definition.HasDualFormulas
                ? $"""
                UPDATE `{definition.TableName}`
                SET
                    ELEMENT_TYPE = @ElementType,
                    LEVEL_NO = @LevelNo,
                    ITEM_CODE = @ItemCode,
                    CAPTION = @Caption,
                    LABEL_TEXT = @LabelText,
                    CODE_NO1 = @CodeNo1,
                    CODE_NO2 = @CodeNo2,
                    DATA_SOURCE_TYPE = @DataSourceType,
                    CALC_METHOD_NO1 = @CalcMethodNo1,
                    CALC_METHOD_NO2 = @CalcMethodNo2,
                    FORMULA_NO1 = @FormulaNo1,
                    FORMULA_NO2 = @FormulaNo2,
                    FORMULA_EXPR = @FormulaExpr,
                    ACCOUNT_RULE = @AccountRule,
                    SORT_ORDER = @SortOrder,
                    FONT_BOLD = @FontBold,
                    FONT_ITALIC = @FontItalic,
                    IS_VISIBLE = @IsVisible,
                    ISDEL = '0',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'DETAIL'
                  AND UPPER(ITEM_KEY) = UPPER(@ItemKey);
                """
                : $"""
                UPDATE `{definition.TableName}`
                SET
                    ELEMENT_TYPE = @ElementType,
                    LEVEL_NO = @LevelNo,
                    ITEM_CODE = @ItemCode,
                    CAPTION = @Caption,
                    LABEL_TEXT = @LabelText,
                    DATA_SOURCE_TYPE = @DataSourceType,
                    FORMULA_EXPR = @FormulaExpr,
                    ACCOUNT_RULE = @AccountRule,
                    CALC_METHOD = @CalcMethod,
                    SORT_ORDER = @SortOrder,
                    FONT_BOLD = @FontBold,
                    FONT_ITALIC = @FontItalic,
                    IS_VISIBLE = @IsVisible,
                    ISDEL = '0',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'DETAIL'
                  AND UPPER(ITEM_KEY) = UPPER(@ItemKey);
                """;

            var command = new CommandDefinition(
                sql,
                BuildRowParameters(companyCd, definition, reportVersion, row, userId),
                transaction: transaction,
                cancellationToken: cancellationToken);
            return await connection.ExecuteAsync(command);
        }

        private static async Task InsertDetailRowAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            NormalizedRow row,
            string userId,
            CancellationToken cancellationToken)
        {
            var sql = definition.HasDirectRules
                ? $"""
                INSERT INTO `{definition.TableName}`
                (
                    COMPANY_CD, REPORT_CODE, REPORT_VERSION, SECTION_TYPE, ELEMENT_TYPE,
                    AREA_CODE, ROW_NO, COL_NO, ROW_SPAN, COL_SPAN, ITEM_KEY, PARENT_KEY,
                    LEVEL_NO, ITEM_CODE, CAPTION, COLUMN_KEY, LABEL_TEXT,
                    DATA_SOURCE_TYPE, CALC_METHOD, FORMULA_EXPR, ACCOUNT_RULE, DIRECT_RULE,
                    DIRECT_RULE_FLOW_SIGN, DIRECT_RULE_ACC_PREFIX, DIRECT_RULE_PRIORITY,
                    DIRECT_RULE_FLOW_SIGN_2, DIRECT_RULE_ACC_PREFIX_2, DIRECT_RULE_PRIORITY_2,
                    VALUE_PERIOD, UNIT_DIVISOR, DATA_TYPE, FORMAT_CODE, NEGATIVE_FORMAT,
                    WIDTH, ALIGN, FONT_BOLD, FONT_ITALIC, IS_VISIBLE, SORT_ORDER, ISDEL,
                    CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
                )
                VALUES
                (
                    @CompanyCd, @ReportCode, @ReportVersion, 'DETAIL', @ElementType,
                    NULL, @SortOrder, 0, 1, 1, @ItemKey, NULL,
                    @LevelNo, @ItemCode, @Caption, '', @LabelText,
                    @DataSourceType, NULL, @FormulaExpr, @AccountRule, NULL,
                    @DirectRuleFlowSign, @DirectRuleAccPrefix, @DirectRulePriority,
                    @DirectRuleFlowSign2, @DirectRuleAccPrefix2, @DirectRulePriority2,
                    NULL, 1, NULL, NULL, NULL,
                    NULL, NULL, @FontBold, @FontItalic, @IsVisible, @SortOrder, '0',
                    NOW(), @UserId, NOW(), @UserId
                );
                """
                : definition.HasDualFormulas
                ? $"""
                INSERT INTO `{definition.TableName}`
                (
                    COMPANY_CD, REPORT_CODE, REPORT_VERSION, SECTION_TYPE, ELEMENT_TYPE,
                    AREA_CODE, ROW_NO, COL_NO, ROW_SPAN, COL_SPAN, ITEM_KEY, PARENT_KEY,
                    LEVEL_NO, ITEM_CODE, CAPTION, COLUMN_KEY, LABEL_TEXT,
                    CODE_NO1, CODE_NO2, DATA_SOURCE_TYPE, CALC_METHOD, CALC_METHOD_NO1, CALC_METHOD_NO2,
                    FORMULA_EXPR, FORMULA_NO1, FORMULA_NO2, ACCOUNT_RULE,
                    VALUE_PERIOD, UNIT_DIVISOR, DATA_TYPE, FORMAT_CODE, NEGATIVE_FORMAT,
                    WIDTH, ALIGN, FONT_BOLD, FONT_ITALIC, IS_VISIBLE, SORT_ORDER, ISDEL,
                    CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
                )
                VALUES
                (
                    @CompanyCd, @ReportCode, @ReportVersion, 'DETAIL', @ElementType,
                    NULL, @SortOrder, 0, 1, 1, @ItemKey, NULL,
                    @LevelNo, @ItemCode, @Caption, '', @LabelText,
                    @CodeNo1, @CodeNo2, @DataSourceType, NULL, @CalcMethodNo1, @CalcMethodNo2,
                    @FormulaExpr, @FormulaNo1, @FormulaNo2, @AccountRule,
                    NULL, 1, NULL, NULL, NULL,
                    NULL, NULL, @FontBold, @FontItalic, @IsVisible, @SortOrder, '0',
                    NOW(), @UserId, NOW(), @UserId
                );
                """
                : $"""
                INSERT INTO `{definition.TableName}`
                (
                    COMPANY_CD, REPORT_CODE, REPORT_VERSION, SECTION_TYPE, ELEMENT_TYPE,
                    AREA_CODE, ROW_NO, COL_NO, ROW_SPAN, COL_SPAN, ITEM_KEY, PARENT_KEY,
                    LEVEL_NO, ITEM_CODE, CAPTION, COLUMN_KEY, LABEL_TEXT,
                    DATA_SOURCE_TYPE, CALC_METHOD, FORMULA_EXPR, ACCOUNT_RULE,
                    VALUE_PERIOD, UNIT_DIVISOR, DATA_TYPE, FORMAT_CODE, NEGATIVE_FORMAT,
                    WIDTH, ALIGN, FONT_BOLD, FONT_ITALIC, IS_VISIBLE, SORT_ORDER, ISDEL,
                    CREATE_AT, CREATE_BY, UPDATE_AT, UPDATE_BY
                )
                VALUES
                (
                    @CompanyCd, @ReportCode, @ReportVersion, 'DETAIL', @ElementType,
                    NULL, @SortOrder, 0, 1, 1, @ItemKey, NULL,
                    @LevelNo, @ItemCode, @Caption, '', @LabelText,
                    @DataSourceType, @CalcMethod, @FormulaExpr, @AccountRule,
                    NULL, 1, NULL, NULL, NULL,
                    NULL, NULL, @FontBold, @FontItalic, @IsVisible, @SortOrder, '0',
                    NOW(), @UserId, NOW(), @UserId
                );
                """;

            var command = new CommandDefinition(
                sql,
                BuildRowParameters(companyCd, definition, reportVersion, row, userId),
                transaction: transaction,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(command);
        }

        private static async Task SoftDeleteMissingDetailRowsAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            IReadOnlyCollection<string> itemKeys,
            string userId,
            CancellationToken cancellationToken)
        {
            var whereNotIn = itemKeys.Count == 0
                ? string.Empty
                : "\n  AND ITEM_KEY NOT IN @ItemKeys";
            var sql = $"""
                UPDATE `{definition.TableName}`
                SET
                    ISDEL = '1',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND UPPER(IFNULL(SECTION_TYPE, '')) = 'DETAIL'
                  AND IFNULL(ISDEL, '0') = '0'{whereNotIn};
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    CompanyCd = companyCd,
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion,
                    ItemKeys = itemKeys,
                    UserId = userId
                },
                transaction: transaction,
                cancellationToken: cancellationToken);
            await connection.ExecuteAsync(command);
        }

        private static async Task<int> SoftDeleteCompanyTemplateRowsAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            string userId,
            CancellationToken cancellationToken)
        {
            // Soft-delete every company override (DETAIL + CONFIG). Shared default COMPANY_CD='' is untouched.
            var sql = $"""
                UPDATE `{definition.TableName}`
                SET
                    ISDEL = '1',
                    UPDATE_AT = NOW(),
                    UPDATE_BY = @UserId
                WHERE COMPANY_CD = @CompanyCd
                  AND REPORT_CODE = @ReportCode
                  AND REPORT_VERSION = @ReportVersion
                  AND IFNULL(ISDEL, '0') = '0';
                """;

            var command = new CommandDefinition(
                sql,
                new
                {
                    CompanyCd = companyCd,
                    ReportCode = definition.ReportCode,
                    ReportVersion = reportVersion,
                    UserId = userId
                },
                transaction: transaction,
                cancellationToken: cancellationToken);
            return await connection.ExecuteAsync(command);
        }

        private static object BuildRowParameters(
            string companyCd,
            ReportTemplateDefinition definition,
            string reportVersion,
            NormalizedRow row,
            string userId)
        {
            return new
            {
                CompanyCd = companyCd,
                ReportCode = definition.ReportCode,
                ReportVersion = reportVersion,
                ItemKey = row.ITEM_KEY,
                ItemCode = row.ITEM_CODE,
                Caption = row.CAPTION,
                LabelText = row.LABEL_TEXT,
                ElementType = row.ELEMENT_TYPE,
                LevelNo = row.LEVEL_NO,
                DataSourceType = row.DATA_SOURCE_TYPE,
                FormulaExpr = row.FORMULA_EXPR,
                CodeNo1 = row.CODE_NO1,
                CodeNo2 = row.CODE_NO2,
                FormulaNo1 = row.FORMULA_NO1,
                FormulaNo2 = row.FORMULA_NO2,
                CalcMethodNo1 = row.CALC_METHOD_NO1,
                CalcMethodNo2 = row.CALC_METHOD_NO2,
                DirectRuleFlowSign = row.DIRECT_RULE_FLOW_SIGN,
                DirectRuleAccPrefix = row.DIRECT_RULE_ACC_PREFIX,
                DirectRulePriority = row.DIRECT_RULE_PRIORITY,
                DirectRuleFlowSign2 = row.DIRECT_RULE_FLOW_SIGN_2,
                DirectRuleAccPrefix2 = row.DIRECT_RULE_ACC_PREFIX_2,
                DirectRulePriority2 = row.DIRECT_RULE_PRIORITY_2,
                AccountRule = row.ACCOUNT_RULE,
                CalcMethod = row.CALC_METHOD,
                SortOrder = row.SORT_ORDER,
                FontBold = row.FONT_BOLD,
                FontItalic = row.FONT_ITALIC,
                IsVisible = row.IS_VISIBLE,
                UserId = userId
            };
        }

        private static List<NormalizedRow> NormalizeRows(
            IEnumerable<SaveCashFlowFormulaOptionRowRequest> rows,
            ReportTemplateDefinition definition)
        {
            var normalizedRows = new List<NormalizedRow>();
            var itemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var itemCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var codeNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0;

            foreach (var source in rows)
            {
                index++;
                if (source == null)
                {
                    throw new ArgumentException($"ROWS[{index - 1}] is required.");
                }

                var itemKey = NormalizeRequired(source.ITEM_KEY, $"ROWS[{index - 1}].ITEM_KEY", 80);
                if (string.Equals(itemKey, CashAccountPrefixesItemKey, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"ROWS[{index - 1}].ITEM_KEY is reserved.");
                }

                var elementType = NormalizeRequired(source.ELEMENT_TYPE, $"ROWS[{index - 1}].ELEMENT_TYPE", 50);

                // GTGT reuses display ITEM_CODE across sections; ITEM_KEY is the stable identity.
                // B03 section headers may omit ITEM_CODE in the shared template.
                var itemCode = definition.HasDualFormulas
                    ? NormalizeOptional(source.ITEM_CODE, $"ROWS[{index - 1}].ITEM_CODE", 50) ?? string.Empty
                    : IsStructuralElementType(elementType)
                        ? NormalizeOptional(source.ITEM_CODE, $"ROWS[{index - 1}].ITEM_CODE", 50) ?? string.Empty
                        : NormalizeRequired(source.ITEM_CODE, $"ROWS[{index - 1}].ITEM_CODE", 50);
                if (!itemKeys.Add(itemKey))
                {
                    throw new ArgumentException($"Duplicate ITEM_KEY: {itemKey}.");
                }

                if (!definition.HasDualFormulas
                    && !string.IsNullOrWhiteSpace(itemCode)
                    && !itemCodes.Add(itemCode))
                {
                    throw new ArgumentException($"Duplicate ITEM_CODE: {itemCode}.");
                }

                int? directRuleFlowSign = null;
                int? directRuleFlowSign2 = null;
                string? directRuleAccPrefix = null;
                int? directRulePriority = null;
                string? directRuleAccPrefix2 = null;
                int? directRulePriority2 = null;

                if (definition.HasDirectRules)
                {
                    directRuleFlowSign = ValidateFlowSign(
                        source.DIRECT_RULE_FLOW_SIGN,
                        $"ROWS[{index - 1}].DIRECT_RULE_FLOW_SIGN");
                    directRuleFlowSign2 = ValidateFlowSign(
                        source.DIRECT_RULE_FLOW_SIGN_2,
                        $"ROWS[{index - 1}].DIRECT_RULE_FLOW_SIGN_2");
                    directRuleAccPrefix = NormalizeDirectRuleAccountPrefixes(
                        source.DIRECT_RULE_ACC_PREFIX,
                        $"ROWS[{index - 1}].DIRECT_RULE_ACC_PREFIX");
                    directRulePriority = ValidatePriority(
                        source.DIRECT_RULE_PRIORITY,
                        $"ROWS[{index - 1}].DIRECT_RULE_PRIORITY");
                    directRuleAccPrefix2 = NormalizeDirectRuleAccountPrefixes(
                        source.DIRECT_RULE_ACC_PREFIX_2,
                        $"ROWS[{index - 1}].DIRECT_RULE_ACC_PREFIX_2");
                    directRulePriority2 = ValidatePriority(
                        source.DIRECT_RULE_PRIORITY_2,
                        $"ROWS[{index - 1}].DIRECT_RULE_PRIORITY_2");

                    ValidateDirectRule(
                        directRuleAccPrefix,
                        directRuleFlowSign,
                        directRulePriority,
                        $"ROWS[{index - 1}] rule 1");
                    ValidateDirectRule(
                        directRuleAccPrefix2,
                        directRuleFlowSign2,
                        directRulePriority2,
                        $"ROWS[{index - 1}] rule 2");
                }

                var dataSourceType = NormalizeOptional(source.DATA_SOURCE_TYPE, $"ROWS[{index - 1}].DATA_SOURCE_TYPE", 50);
                if (IsAccountRuleReport(definition.ReportCode)
                    && string.Equals(dataSourceType, "PROCEDURE", StringComparison.OrdinalIgnoreCase))
                {
                    dataSourceType = "ACCOUNT_RULE";
                }

                string? codeNo1 = null;
                string? codeNo2 = null;
                string? formulaNo1 = null;
                string? formulaNo2 = null;
                string? calcMethodNo1 = null;
                string? calcMethodNo2 = null;

                if (definition.HasDualFormulas)
                {
                    codeNo1 = NormalizeOptional(source.CODE_NO1, $"ROWS[{index - 1}].CODE_NO1", 20);
                    codeNo2 = NormalizeOptional(source.CODE_NO2, $"ROWS[{index - 1}].CODE_NO2", 20);
                    formulaNo1 = NormalizeOptional(source.FORMULA_NO1, $"ROWS[{index - 1}].FORMULA_NO1", 1000);
                    formulaNo2 = NormalizeOptional(source.FORMULA_NO2, $"ROWS[{index - 1}].FORMULA_NO2", 1000);
                    calcMethodNo1 = NormalizeGtgtCalcMethod(source.CALC_METHOD_NO1, $"ROWS[{index - 1}].CALC_METHOD_NO1");
                    calcMethodNo2 = NormalizeGtgtCalcMethod(source.CALC_METHOD_NO2, $"ROWS[{index - 1}].CALC_METHOD_NO2");

                    if (!string.IsNullOrWhiteSpace(codeNo1) && !codeNos.Add(codeNo1))
                    {
                        throw new ArgumentException($"Duplicate CODE_NO1: {codeNo1}.");
                    }

                    if (!string.IsNullOrWhiteSpace(codeNo2) && !codeNos.Add(codeNo2))
                    {
                        throw new ArgumentException($"Duplicate CODE_NO2: {codeNo2}.");
                    }
                }

                var caption = NormalizeOptional(source.CAPTION, $"ROWS[{index - 1}].CAPTION", 1000)
                    ?? NormalizeOptional(source.ITEM_NAME, $"ROWS[{index - 1}].ITEM_NAME", 1000);
                var labelText = NormalizeOptional(source.LABEL_TEXT, $"ROWS[{index - 1}].LABEL_TEXT", 200)
                    ?? BuildDefaultLabelText(definition.ReportCode, itemCode, itemKey);

                normalizedRows.Add(new NormalizedRow(
                    itemKey,
                    itemCode,
                    caption,
                    labelText,
                    elementType,
                    ValidateRange(source.LEVEL_NO ?? 0, -100, 100, $"ROWS[{index - 1}].LEVEL_NO"),
                    dataSourceType,
                    NormalizeOptional(source.FORMULA_EXPR, $"ROWS[{index - 1}].FORMULA_EXPR", 1000),
                    codeNo1,
                    codeNo2,
                    formulaNo1,
                    formulaNo2,
                    calcMethodNo1,
                    calcMethodNo2,
                    directRuleFlowSign,
                    directRuleAccPrefix,
                    directRulePriority,
                    directRuleFlowSign2,
                    directRuleAccPrefix2,
                    directRulePriority2,
                    NormalizeOptional(source.ACCOUNT_RULE, $"ROWS[{index - 1}].ACCOUNT_RULE", 10000),
                    NormalizeOptional(source.CALC_METHOD, $"ROWS[{index - 1}].CALC_METHOD", 50),
                    source.SORT_ORDER ?? index,
                    NormalizeFlag(source.FONT_BOLD, "0", $"ROWS[{index - 1}].FONT_BOLD"),
                    NormalizeFlag(source.FONT_ITALIC, "0", $"ROWS[{index - 1}].FONT_ITALIC"),
                    NormalizeFlag(source.IS_VISIBLE, "1", $"ROWS[{index - 1}].IS_VISIBLE")));
            }

            return normalizedRows;
        }

        private static string? NormalizeGtgtCalcMethod(string? value, string fieldName)
        {
            var normalized = NormalizeOptional(value, fieldName, 50);
            if (normalized == null)
            {
                return null;
            }

            var upper = normalized.ToUpperInvariant();
            return upper switch
            {
                "MANUAL" or "FORMULA" or "FORMULA_POSITIVE" or "FORMULA_NEGATIVE" or "HEADER" => upper,
                _ => throw new ArgumentException($"{fieldName} must be MANUAL, FORMULA, FORMULA_POSITIVE, FORMULA_NEGATIVE, or HEADER.")
            };
        }

        private static void ValidateDualFormulaReferences(IReadOnlyCollection<NormalizedRow> rows)
        {
            var cellCodes = rows
                .SelectMany(row => new[] { row.CODE_NO1, row.CODE_NO2 })
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dependencies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                ValidateDualFormulaSide(
                    row.CALC_METHOD_NO1,
                    row.FORMULA_NO1,
                    row.CODE_NO1,
                    "FORMULA_NO1",
                    cellCodes,
                    dependencies);
                ValidateDualFormulaSide(
                    row.CALC_METHOD_NO2,
                    row.FORMULA_NO2,
                    row.CODE_NO2,
                    "FORMULA_NO2",
                    cellCodes,
                    dependencies);
            }

            foreach (var cellCode in dependencies.Keys)
            {
                if (HasFormulaCycle(cellCode, dependencies, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("FORMULA_NO1/FORMULA_NO2 values cannot contain a circular reference.");
                }
            }
        }

        private static void ValidateDualFormulaSide(
            string? calcMethod,
            string? formulaExpr,
            string? cellCode,
            string fieldName,
            ISet<string> cellCodes,
            IDictionary<string, IReadOnlyList<string>> dependencies)
        {
            var method = (calcMethod ?? string.Empty).Trim().ToUpperInvariant();
            if (method is not ("FORMULA" or "FORMULA_POSITIVE" or "FORMULA_NEGATIVE"))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(cellCode))
            {
                throw new ArgumentException($"{fieldName} requires a cell code when CALC_METHOD is {method}.");
            }

            if (string.IsNullOrWhiteSpace(formulaExpr))
            {
                throw new ArgumentException($"{fieldName} for CODE {cellCode} is required when CALC_METHOD is {method}.");
            }

            var expression = formulaExpr.Trim();
            var equalsIndex = expression.IndexOf('=');
            if (equalsIndex >= 0)
            {
                expression = expression[(equalsIndex + 1)..].Trim();
            }

            if (expression.Length == 0 || expression.Contains('=') || !BasicFormulaCharacters.IsMatch(expression))
            {
                return;
            }

            var references = expression
                .Replace(';', '+')
                .Split(['+', '-'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (references.Length > 40)
            {
                throw new ArgumentException($"{fieldName} for CODE {cellCode} must contain at most 40 references.");
            }

            foreach (var term in references)
            {
                if (term == "0" || cellCodes.Contains(term))
                {
                    if (string.Equals(term, cellCode, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException($"{fieldName} for CODE {cellCode} cannot reference itself.");
                    }

                    continue;
                }

                throw new ArgumentException($"{fieldName} for CODE {cellCode} references unknown CODE {term}.");
            }

            dependencies[cellCode] = references
                .Where(term => term != "0")
                .ToList();
        }

        private static void ValidateFormulaReferences(IReadOnlyCollection<NormalizedRow> rows)
        {
            var itemCodes = rows
                .Select(row => row.ITEM_CODE)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dependencies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (!string.Equals(row.DATA_SOURCE_TYPE, "FORMULA", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(row.FORMULA_EXPR))
                {
                    continue;
                }

                var expression = row.FORMULA_EXPR.Trim();
                var equalsIndex = expression.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    expression = expression[(equalsIndex + 1)..].Trim();
                }

                // The report procedure supports +/-/; item-code expressions. Only validate
                // this unambiguous subset, so existing expressions using legacy syntax are
                // preserved instead of being rejected by the editor API.
                if (expression.Length == 0 || expression.Contains('=') || !BasicFormulaCharacters.IsMatch(expression))
                {
                    continue;
                }

                var references = expression
                    .Replace(';', '+')
                    .Split(['+', '-'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (references.Length > 40)
                {
                    throw new ArgumentException($"FORMULA_EXPR for ITEM_CODE {row.ITEM_CODE} must contain at most 40 references.");
                }

                foreach (var term in references)
                {
                    if (term == "0" || itemCodes.Contains(term))
                    {
                        if (string.Equals(term, row.ITEM_CODE, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ArgumentException($"FORMULA_EXPR for ITEM_CODE {row.ITEM_CODE} cannot reference itself.");
                        }

                        continue;
                    }

                    throw new ArgumentException($"FORMULA_EXPR for ITEM_CODE {row.ITEM_CODE} references unknown ITEM_CODE {term}.");
                }

                dependencies[row.ITEM_CODE] = references
                    .Where(term => term != "0")
                    .ToList();
            }

            foreach (var itemCode in dependencies.Keys)
            {
                if (HasFormulaCycle(itemCode, dependencies, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException("FORMULA_EXPR values cannot contain a circular reference.");
                }
            }
        }

        private static bool HasFormulaCycle(
            string itemCode,
            IReadOnlyDictionary<string, IReadOnlyList<string>> dependencies,
            ISet<string> visiting,
            ISet<string> visited)
        {
            if (visiting.Contains(itemCode))
            {
                return true;
            }

            if (visited.Contains(itemCode) || !dependencies.TryGetValue(itemCode, out var references))
            {
                return false;
            }

            visiting.Add(itemCode);
            foreach (var reference in references)
            {
                if (HasFormulaCycle(reference, dependencies, visiting, visited))
                {
                    return true;
                }
            }

            visiting.Remove(itemCode);
            visited.Add(itemCode);
            return false;
        }

        public static string ResolvePermissionMenuCode(string? reportCode)
        {
            return ResolveTemplateDefinition(reportCode).PermissionMenuCode;
        }

        private static ReportTemplateDefinition ResolveTemplateDefinition(string? reportCode)
        {
            var normalizedReportCode = Common.NormalizeNullableText(reportCode)?.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedReportCode) ||
                !TemplateDefinitions.TryGetValue(normalizedReportCode, out var definition))
            {
                throw new ArgumentException(
                    "REPORT_CODE must be a supported formula template: B03_DN_TT, B03_DN_GT, B01_DN, B02_DN, GTGT_01, TAX_VAT_DECLARATION, GL_CASHFLOW_B03DN_TT, GL_CASHFLOW_B03DN_GT, GL_BALANCE_SHEET_B01DN, GL_PROFIT_LOSS_B02DN, or GL_PROFIT_LOSS_B02DNTT.");
            }

            return definition;
        }

        private static bool IsAccountRuleReport(string? reportCode)
        {
            return string.Equals(reportCode, "B01_DN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(reportCode, "B02_DN", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStructuralElementType(string? elementType)
        {
            return string.Equals(elementType, "SECTION", StringComparison.OrdinalIgnoreCase)
                || string.Equals(elementType, "HEADER", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeReportVersion(string? reportVersion)
        {
            return NormalizeOptional(reportVersion, "REPORT_VERSION", 20) ?? "2025";
        }

        private static string? NormalizeCashAccountPrefixes(string? value)
        {
            return NormalizeOptional(value, "CASH_ACCOUNT_PREFIXES", 500);
        }

        private static string NormalizeRequired(string? value, string fieldName, int maxLength)
        {
            var normalized = NormalizeOptional(value, fieldName, maxLength);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException($"{fieldName} is required.");
            }

            return normalized;
        }

        private static string? NormalizeOptional(string? value, string fieldName, int maxLength)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (normalized == null)
            {
                return null;
            }

            if (normalized.Length > maxLength)
            {
                throw new ArgumentException($"{fieldName} must not exceed {maxLength} characters.");
            }

            return normalized;
        }

        private static int? ValidateFlowSign(int? value, string fieldName)
        {
            if (value is null)
            {
                return null;
            }

            if (value is not (-1 or 0 or 1))
            {
                throw new ArgumentException($"{fieldName} must be -1, 0, or 1.");
            }

            return value;
        }

        private static int? ValidatePriority(int? value, string fieldName)
        {
            if (value is null)
            {
                return null;
            }

            return ValidateRange(value.Value, -100000, 100000, fieldName);
        }

        private static string? NormalizeDirectRuleAccountPrefixes(string? value, string fieldName)
        {
            var normalized = NormalizeOptional(value, fieldName, 500);
            if (normalized == null)
            {
                return null;
            }

            var prefixes = normalized
                .Replace(';', ',')
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (prefixes.Count == 0)
            {
                return null;
            }

            if (prefixes.Count > 40)
            {
                throw new ArgumentException($"{fieldName} must contain at most 40 account prefixes.");
            }

            if (prefixes.Any(prefix => prefix.Length > 20))
            {
                throw new ArgumentException($"{fieldName} account prefixes must not exceed 20 characters.");
            }

            return string.Join(',', prefixes);
        }

        private static void ValidateDirectRule(
            string? accountPrefixes,
            int? flowSign,
            int? priority,
            string fieldPrefix)
        {
            if (string.IsNullOrWhiteSpace(accountPrefixes))
            {
                return;
            }

            if (flowSign is not (-1 or 1))
            {
                throw new ArgumentException($"{fieldPrefix} requires DIRECT_RULE_FLOW_SIGN of -1 or 1 when an account prefix is provided.");
            }

            if (!priority.HasValue)
            {
                throw new ArgumentException($"{fieldPrefix} requires DIRECT_RULE_PRIORITY when an account prefix is provided.");
            }
        }

        private static int ValidateRange(int value, int minimum, int maximum, string fieldName)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentException($"{fieldName} must be between {minimum} and {maximum}.");
            }

            return value;
        }

        private static string NormalizeFlag(string? value, string defaultValue, string fieldName)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (normalized == null)
            {
                return defaultValue;
            }

            return normalized.ToUpperInvariant() switch
            {
                "1" or "Y" or "YES" or "TRUE" => "1",
                "0" or "N" or "NO" or "FALSE" => "0",
                _ => throw new ArgumentException($"{fieldName} must be a boolean flag.")
            };
        }

        private static string? ReadResolvedTemplateCompanyCd(string? value)
        {
            // Blank COMPANY_CD is the shared default template key. Do not use NormalizeNullableText here.
            return value == null ? null : value.Trim();
        }

        private static bool IsSharedDefaultTemplateCompany(string companyCd)
        {
            return companyCd.Length == 0;
        }

        private static bool IsTemplateOwnerCompany(string companyCd)
        {
            return IsSharedDefaultTemplateCompany(companyCd);
        }

        private async Task ApplyDraftInTransactionAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            string normalizedCompanyCd,
            ReportTemplateDefinition definition,
            string normalizedVersion,
            IReadOnlyList<NormalizedRow> normalizedRows,
            string? suppliedCashAccountPrefixes,
            string normalizedUserId,
            CancellationToken cancellationToken)
        {
            var hasCurrentDetailRows = await HasActiveDetailRowsAsync(
                connection,
                transaction,
                normalizedCompanyCd,
                definition,
                normalizedVersion,
                cancellationToken);

            if (!IsSharedDefaultTemplateCompany(normalizedCompanyCd) && !hasCurrentDetailRows)
            {
                var fallbackCompanyCd = await ResolveFallbackDetailSourceCompanyCdAsync(
                    connection,
                    transaction,
                    definition,
                    normalizedVersion,
                    cancellationToken);

                if (fallbackCompanyCd != null)
                {
                    await CopyActiveRowsAsync(
                        connection,
                        transaction,
                        normalizedCompanyCd,
                        fallbackCompanyCd,
                        definition,
                        normalizedVersion,
                        normalizedUserId,
                        cancellationToken);

                    _logger.LogInformation(
                        "Initialized cash-flow formula template {ReportCode}/{ReportVersion} for company {CompanyCd} from {SourceCompanyCd}.",
                        definition.ReportCode,
                        normalizedVersion,
                        normalizedCompanyCd,
                        fallbackCompanyCd);
                }
            }

            var cashAccountPrefixes = definition.HasCashAccountConfig
                ? suppliedCashAccountPrefixes
                    ?? await GetCashAccountPrefixesAsync(
                        connection,
                        transaction,
                        normalizedCompanyCd,
                        definition,
                        normalizedVersion,
                        cancellationToken)
                    ?? DefaultCashAccountPrefixes
                : null;

            foreach (var row in normalizedRows)
            {
                var affectedRows = await UpdateDetailRowAsync(
                    connection,
                    transaction,
                    normalizedCompanyCd,
                    definition,
                    normalizedVersion,
                    row,
                    normalizedUserId,
                    cancellationToken);

                if (affectedRows == 0)
                {
                    await InsertDetailRowAsync(
                        connection,
                        transaction,
                        normalizedCompanyCd,
                        definition,
                        normalizedVersion,
                        row,
                        normalizedUserId,
                        cancellationToken);
                }
            }

            await SoftDeleteMissingDetailRowsAsync(
                connection,
                transaction,
                normalizedCompanyCd,
                definition,
                normalizedVersion,
                normalizedRows.Select(row => row.ITEM_KEY).ToList(),
                normalizedUserId,
                cancellationToken);

            if (!await HasActiveDetailRowsAsync(
                    connection,
                    transaction,
                    normalizedCompanyCd,
                    definition,
                    normalizedVersion,
                    cancellationToken))
            {
                throw new InvalidOperationException("A cash-flow template must contain at least one active DETAIL row.");
            }

            if (definition.HasCashAccountConfig && cashAccountPrefixes != null)
            {
                await UpsertCashAccountPrefixesAsync(
                    connection,
                    transaction,
                    normalizedCompanyCd,
                    definition,
                    normalizedVersion,
                    cashAccountPrefixes,
                    normalizedUserId,
                    cancellationToken);
            }
        }

        private static Dictionary<string, string> BuildFormulaPreviewQuery(
            PreviewCashFlowFormulaOptionsRequest request,
            string reportVersion,
            string fromYmd,
            string toYmd)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fromYmd"] = fromYmd,
                ["toYmd"] = toYmd,
                ["reportVersion"] = reportVersion,
                ["unitDivisor"] = Common.NormalizeNullableText(request.UnitDivisor) ?? "1",
                ["language"] = "VIET",
            };
        }

        private static FormulaOptionPreviewDto MapFormulaPreviewDto(DataTable dataTable)
        {
            var itemCodeColumn = ResolveDataTableColumn(dataTable, "ITEM_CODE");
            if (itemCodeColumn == null)
            {
                throw new InvalidOperationException("Report preview result is missing ITEM_CODE.");
            }

            var valueColumns = dataTable.Columns
                .Cast<DataColumn>()
                .Where(column => IsFormulaPreviewValueColumn(column))
                .OrderBy(column => ResolveFormulaPreviewColumnSortOrder(column.ColumnName))
                .ThenBy(column => column.ColumnName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var preview = new FormulaOptionPreviewDto
            {
                COLUMNS = valueColumns
                    .Select(column => new FormulaOptionPreviewColumnDto
                    {
                        FIELD_NAME = column.ColumnName,
                        CAPTION = ResolveFormulaPreviewColumnCaption(column.ColumnName),
                    })
                    .ToList(),
            };

            foreach (DataRow row in dataTable.Rows)
            {
                var itemCode = Common.NormalizeNullableText(row[itemCodeColumn]?.ToString());
                if (string.IsNullOrWhiteSpace(itemCode))
                {
                    continue;
                }

                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in valueColumns)
                {
                    values[column.ColumnName] = NormalizePreviewCellValue(row[column]);
                }

                preview.ROWS.Add(new FormulaOptionPreviewRowDto
                {
                    ITEM_CODE = itemCode,
                    VALUES = values,
                });
            }

            return preview;
        }

        private static readonly HashSet<string> FormulaPreviewMetadataColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            "ROW_KEY",
            "COMPANY_CD",
            "ROW_TYPE",
            "SORT_ORDER",
            "ITEM_CODE",
            "ITEM_NAME",
            "CAPTION",
            "LABEL_TEXT",
            "NOTE",
            "LEVEL_NO",
            "FONT_BOLD",
            "FONT_ITALIC",
            "DATA_SOURCE_TYPE",
            "FORMULA_EXPR",
            "REPORT_YEAR_TEXT",
            "REPORT_PERIOD_TEXT",
            "REPORT_DATE_TEXT",
            "AMOUNT_UNIT_TEXT",
            "STT",
            "KY_TINH_THUE_TEXT",
            "CODE_NO1",
            "CODE_NO2",
        };

        private static readonly Dictionary<string, string> FormulaPreviewColumnCaptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CURRENT_YEAR"] = "Năm nay",
            ["PREVIOUS_YEAR"] = "Năm trước",
            ["END_YEAR"] = "Cuối kỳ",
            ["BEGIN_YEAR"] = "Đầu kỳ",
            ["VALUE_HHDV"] = "HHDV",
            ["VAT_AMOUNT"] = "Thuế GTGT",
        };

        private static bool IsFormulaPreviewValueColumn(DataColumn column)
        {
            if (FormulaPreviewMetadataColumns.Contains(column.ColumnName))
            {
                return false;
            }

            return column.DataType == typeof(decimal)
                || column.DataType == typeof(double)
                || column.DataType == typeof(float)
                || column.DataType == typeof(int)
                || column.DataType == typeof(long)
                || column.DataType == typeof(short)
                || column.DataType == typeof(byte);
        }

        private static string ResolveFormulaPreviewColumnCaption(string fieldName)
        {
            if (FormulaPreviewColumnCaptions.TryGetValue(fieldName, out var caption))
            {
                return caption;
            }

            return fieldName.Replace('_', ' ').Trim();
        }

        private static int ResolveFormulaPreviewColumnSortOrder(string fieldName)
        {
            return fieldName.ToUpperInvariant() switch
            {
                "CURRENT_YEAR" or "END_YEAR" or "VALUE_HHDV" => 0,
                "PREVIOUS_YEAR" or "BEGIN_YEAR" or "VAT_AMOUNT" => 1,
                _ => 10,
            };
        }

        private static string? ResolveDataTableColumn(DataTable table, string columnName)
        {
            return table.Columns.Cast<DataColumn>()
                .FirstOrDefault(column => column.ColumnName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                ?.ColumnName;
        }

        private static object? NormalizePreviewCellValue(object value)
        {
            if (value == null || value is DBNull)
            {
                return null;
            }

            return value;
        }

        private sealed record ReportTemplateDefinition(
            string ReportCode,
            string TableName,
            string PermissionMenuCode,
            bool HasCashAccountConfig,
            bool HasDirectRules,
            bool HasDualFormulas);

        private static string BuildDefaultLabelText(string reportCode, string itemCode, string itemKey)
        {
            return !string.IsNullOrWhiteSpace(itemCode)
                ? $"{reportCode}.{itemCode}"
                : itemKey;
        }

        private sealed record NormalizedRow(
            string ITEM_KEY,
            string ITEM_CODE,
            string? CAPTION,
            string? LABEL_TEXT,
            string ELEMENT_TYPE,
            int LEVEL_NO,
            string? DATA_SOURCE_TYPE,
            string? FORMULA_EXPR,
            string? CODE_NO1,
            string? CODE_NO2,
            string? FORMULA_NO1,
            string? FORMULA_NO2,
            string? CALC_METHOD_NO1,
            string? CALC_METHOD_NO2,
            int? DIRECT_RULE_FLOW_SIGN,
            string? DIRECT_RULE_ACC_PREFIX,
            int? DIRECT_RULE_PRIORITY,
            int? DIRECT_RULE_FLOW_SIGN_2,
            string? DIRECT_RULE_ACC_PREFIX_2,
            int? DIRECT_RULE_PRIORITY_2,
            string? ACCOUNT_RULE,
            string? CALC_METHOD,
            int SORT_ORDER,
            string FONT_BOLD,
            string FONT_ITALIC,
            string IS_VISIBLE);
    }
}

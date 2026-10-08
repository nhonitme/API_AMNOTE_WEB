using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reports;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

public sealed class DefaultExcelImportJobProcessor : IExcelImportJobProcessor
{
    public async Task<ExcelImportResultDto> ProcessAsync(
        ExcelImportJobRequest request,
        IExcelImportJobProgressWriter progressWriter,
        IExcelImportModuleHandler handler,
        CancellationToken cancellationToken = default)
    {
        const int TOTAL_PROGRESS = 100;
        var lang = request.Lang ?? Common.GetCurrentLanguage();
        if (handler is IExcelImportCustomProcessorHandler customProcessor)
        {
            return await customProcessor.ProcessImportAsync(request, progressWriter, cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(request.ModuleCd))
        {
            return BuildError(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_MODULE_REQUIRED", lang));
        }
        if (string.IsNullOrWhiteSpace(request.TempFilePath) || !File.Exists(request.TempFilePath))
        {
            return BuildError(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_NOT_FOUND", lang));
        }
        await progressWriter.ReportPercentAsync(10, ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_READ_PROGRESS", lang), cancellationToken);
        await using var stream = File.OpenRead(request.TempFilePath);
        request.Params.TryGetValue("sheetName", out var sheetName);
        var importedData = await ExcelHelper.ReadExcelDataAsync(
            stream,
            request.ModuleCd,
            request.CompanyCd,
            sheetName,
            async (processedRows, totalRows, ct) =>
            {
                await progressWriter.ReportAsync(
                    processedRows,
                    totalRows,
                    ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_READ_PROGRESS", lang) + $" {processedRows}/{totalRows}.",
                    ct);
            },
            cancellationToken);
        var totalExcelRows = importedData.Count;
        await progressWriter.ReportAsync(
            totalExcelRows,
            totalExcelRows,
            20,
            string.Format(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_READ_COMPLETE", lang), totalExcelRows),
            cancellationToken);
        var templateColumns = await Common.GetExcelTemplateColumnInfosAsync(request.ModuleCd, request.CompanyCd);
        if (templateColumns == null || !templateColumns.Any())
        {
            // Empty excel_template_column (no defaults) — reuse MODULE_REQUIRED i18n key.
            return BuildError(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_MODULE_REQUIRED", lang));
        }
        var totalRowsOriginal = importedData.Count;
        var validateResults = new List<ExcelImportResultRowDto>();
        // Cache EXPLAIN / existence lookups once per job — avoids N×rows DB hits.
        var explainCdCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        const int maxStoredRowResults = 500;
        // Early-stop only for vouchers (all-or-nothing + huge files). Masters validate fully so every duplicate is listed.
        var earlyStopOnErrors = !AllowsPartialImport(request.ModuleCd);
        for (var i = 0; i < importedData.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var excelRowNo = ExcelImportHandlerHelper.GetExcelRowNo(importedData[i], i);
            handler.ValidateRow(importedData[i], excelRowNo, validateResults);
            await ValidateRow(
                templateColumns,
                importedData[i],
                excelRowNo,
                i,
                lang,
                request,
                validateResults,
                importedData,
                explainCdCache);
            // Vouchers: stop after enough errors to report (avoids OOM on 10k+ rows).
            if (earlyStopOnErrors
                && validateResults.Count >= maxStoredRowResults
                && validateResults.Any(x => string.Equals(x.Status, "ERROR", StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }
            // Throttle progress I/O — every-row persist was amplifying host instability.
            if (i == importedData.Count - 1 || (i + 1) % 25 == 0)
            {
                var percent = 20 + (int)Math.Round((i + 1) * 20m / Math.Max(importedData.Count, 1));
                await progressWriter.ReportAsync(
                    i + 1,
                    totalExcelRows,
                    percent,
                    string.Format(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_VALIDATING_DATA", lang), i + 1, importedData.Count),
                    cancellationToken);
            }
        }
        if (importedData.Count > 0 && (!earlyStopOnErrors || !validateResults.Any(x => string.Equals(x.Status, "ERROR", StringComparison.OrdinalIgnoreCase))))
        {
            await handler.ValidateRowsAsync(importedData, request, validateResults, cancellationToken);
        }
        var errorItems = validateResults.Where(x => string.Equals(x.Status, "ERROR", StringComparison.OrdinalIgnoreCase)).ToList();
        var skipItems = validateResults.Where(x => string.Equals(x.Status, "SKIP", StringComparison.OrdinalIgnoreCase)).ToList();
        var warningItems = validateResults.Where(x => string.Equals(x.Status, "WARNING", StringComparison.OrdinalIgnoreCase)).ToList();
        var errorRowNos = errorItems.Select(x => x.RowNo).ToHashSet();
        var skipRowNos = skipItems.Select(x => x.RowNo).ToHashSet();
        // Cap payload size after row-number sets are built (partial import still has full sets).
        TrimValidateResults(validateResults, maxStoredRowResults);
        var rowsToSave = new List<Dictionary<string, object>>();
        for (var i = 0; i < importedData.Count; i++)
        {
            var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(importedData[i], i);
            if (skipRowNos.Contains(rowNo) || errorRowNos.Contains(rowNo))
            {
                continue;
            }
            rowsToSave.Add(importedData[i]);
        }
        // Never silently skip failed rows — any ERROR fails the job (WARNING/SKIP only for soft refs).
        if (errorItems.Count > 0)
        {
            await progressWriter.ReportAsync(
                totalExcelRows,
                totalExcelRows,
                TOTAL_PROGRESS,
                string.Format(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_HAS_ERRORS", lang), errorItems.Count),
                cancellationToken);
            return new ExcelImportResultDto
            {
                Success = false,
                TotalRows = totalRowsOriginal,
                SuccessRows = 0,
                WarningRows = warningItems.Count + skipItems.Count,
                ErrorRows = errorItems.Count,
                Message = string.Format(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_HAS_ERRORS", lang), errorItems.Count),
                Rows = validateResults.Take(maxStoredRowResults).ToList(),
                SheetName = sheetName
            };
        }
        if (rowsToSave.Count == 0)
        {
            await progressWriter.ReportAsync(
                0,
                0,
                TOTAL_PROGRESS,
                ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_JOB_COMPLETE", lang),
                cancellationToken);
            return new ExcelImportResultDto
            {
                Success = true,
                TotalRows = totalRowsOriginal,
                SuccessRows = 0,
                WarningRows = skipItems.Count + warningItems.Count,
                ErrorRows = 0,
                Message = ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_NO_DATA_IN_FILE", lang),
                Rows = validateResults.Take(maxStoredRowResults).ToList(),
                SheetName = sheetName
            };
        }
        importedData = rowsToSave;
        await handler.SaveAsync(
            importedData,
            request,
            progressWriter,
            cancellationToken);
        await progressWriter.ReportAsync(
            totalExcelRows,
            totalExcelRows,
            TOTAL_PROGRESS,
            ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_JOB_COMPLETE", lang),
            cancellationToken);
        var okMessage = warningItems.Count > 0
            ? $"Import Excel hoàn tất. Ghi {importedData.Count}/{totalRowsOriginal} dòng ({warningItems.Count} cảnh báo)."
            : ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_JOB_COMPLETE", lang);
        return new ExcelImportResultDto
        {
            Success = true,
            TotalRows = totalRowsOriginal,
            SuccessRows = importedData.Count,
            WarningRows = warningItems.Count,
            ErrorRows = 0,
            Message = okMessage,
            Rows = warningItems.Count > 0
                ? validateResults.Take(maxStoredRowResults).ToList()
                : new List<ExcelImportResultRowDto>(),
            SheetName = sheetName
        };
    }
    private async Task ValidateRow(
        List<ExcelTemplateColumnInfo> templateColumns,
        Dictionary<string, object> row,
        int rowNo,
        int rowIndex,
        string lang,
        ExcelImportJobRequest request,
        List<ExcelImportResultRowDto> validateResults,
        List<Dictionary<string, object>> _importedData,
        Dictionary<string, HashSet<string>> explainCdCache)
    {
        for (int i = 0; i < templateColumns.Count; i++)
        {
            var col = templateColumns[i];
            if (string.IsNullOrWhiteSpace(col.FIELD_NAME))
            {
                validateResults.Add(ExcelImportHandlerHelper.BuildError(
                    rowNo,
                    $"Row {rowNo}: FIELD_NAME {ExcelImportHandlerHelper.GetLocalizedMessage("REQUIRED", lang)}"));
            }
            var displayLabel = ResolveColumnDisplayLabel(col, lang);
            var val = (Common.GetStringValue(row, col.FIELD_NAME) + "").Trim();
            if (col.IS_REQUIRED + "" == "1" || col.IS_PRIMARY_KEY + "" == "1")
            {
                if (string.IsNullOrWhiteSpace(val)
                    && !IsSoftMissingReference(request.ModuleCd, col.FIELD_NAME))
                {
                    validateResults.Add(ExcelImportHandlerHelper.BuildRequiredFieldError(rowNo, col.FIELD_NAME, lang));
                }
            }
            if (col.IS_PRIMARY_KEY + "" == "1")
            {
                for (int j = 0; j < _importedData.Count; j++)
                {
                    if (j != rowIndex && (Common.GetStringValue(_importedData[j], col.FIELD_NAME) + "").ToUpper() == val.ToUpper())
                    {
                        validateResults.Add(new ExcelImportResultRowDto
                        {
                            RowNo = rowNo,
                            Status = "ERROR",
                            Message = ExcelImportHandlerHelper.BuildDuplicateFieldExceptionMessage(col.FIELD_NAME, new[] { val }, lang),
                            KeyValue = val
                        });
                    }
                }
                if (col.TABLE_NM + "" != "" && col.FIELD_NAME != "" && val != "")
                {
                    var existsKey = $"TABLE|{col.TABLE_NM}|{col.FIELD_NAME}|{request.CompanyCd}";
                    var existingCodes = await GetOrLoadExplainCdsAsync(
                        explainCdCache,
                        existsKey,
                        () => ImportColumnExplanationProviders.GetImportColumnExplanationAsync(
                            lang, col.TABLE_NM + "", col.FIELD_NAME + "", request.CompanyCd));
                    if (existingCodes.Contains(val))
                    {
                        validateResults.Add(ExcelImportHandlerHelper.BuildError(
                            rowNo,
                            ExcelImportHandlerHelper.BuildRowAlreadyExistsMessage(rowNo, displayLabel, val + "", lang)));
                    }
                }
            }
            if (val != "" && col.EXPLAIN_TABLE_QUERY + "" != "")
            {
                var EXPLAIN_TABLE_QUERY = (col.EXPLAIN_TABLE_QUERY + "")
                    .Replace("@COMPANY_CD", request.CompanyCd)
                    .Replace("@COMPANYCD", request.CompanyCd);
                var explainKey = $"Q|{EXPLAIN_TABLE_QUERY}";
                var explainCds = await GetOrLoadExplainCdsAsync(
                    explainCdCache,
                    explainKey,
                    () => ImportColumnExplanationProviders.GetImportColumnExplanationAsync(
                        lang, EXPLAIN_TABLE_QUERY, request.CompanyCd));
                if (!explainCds.Contains(val))
                {
                    if (IsSoftMissingReference(request.ModuleCd, col.FIELD_NAME))
                    {
                        row[col.FIELD_NAME] = null!;
                        validateResults.Add(new ExcelImportResultRowDto
                        {
                            RowNo = rowNo,
                            Status = "WARNING",
                            Message = ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, displayLabel, val, lang).Message
                                + " (đã bỏ qua)",
                            KeyValue = val
                        });
                    }
                    else
                    {
                        validateResults.Add(ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, displayLabel, val, lang));
                    }
                }
            }
        }
    }

    private static string ResolveColumnDisplayLabel(ExcelTemplateColumnInfo col, string lang)
        => ReportLanguageHelper.ResolveDisplayLabel(col.LABEL_TEXT, lang);

    private static async Task<HashSet<string>> GetOrLoadExplainCdsAsync(
        Dictionary<string, HashSet<string>> cache,
        string cacheKey,
        Func<Task<IEnumerable<EtcInfo>>> loader)
    {
        if (cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var lines = await loader();
            if (lines != null)
            {
                foreach (var item in lines)
                {
                    var cd = (item.CD + "").Trim();
                    if (cd.Length > 0)
                    {
                        set.Add(cd);
                    }
                }
            }
        }
        catch
        {
            // Keep empty set — treat as "not found" rather than crashing the whole job.
        }
        cache[cacheKey] = set;
        return set;
    }

    private static ExcelImportResultDto BuildError(string message)
    {
        return new ExcelImportResultDto
        {
            Success = false,
            TotalRows = 0,
            SuccessRows = 0,
            WarningRows = 0,
            ErrorRows = 1,
            Message = message,
            Rows = new List<ExcelImportResultRowDto>
            {
                new()
                {
                    RowNo = 0,
                    Status = "ERROR",
                    Message = message
                }
            },
            SheetName = string.Empty
        };
    }
    /// <summary>
    /// Catalog modules: validate every row so duplicate/already-exists errors are all reported.
    /// Used only to decide whether validate can early-stop (vouchers = false).
    /// </summary>
    private static bool AllowsPartialImport(string? moduleCd)
    {
        if (string.IsNullOrWhiteSpace(moduleCd))
        {
            return false;
        }
        return moduleCd is
            "ProductUnit" or
            "ProductKind" or
            "StoreKindInfo" or
            "StoreInfo" or
            "BankInfo" or
            "DepartmentInfo" or
            "ManagementInfo" or
            "ProductInfo" or
            "CustomerInfoCustomerExt" or
            "CustomerInfo" or
            "FixedAssetInfo";
    }

    private static bool IsSoftMissingReference(string? moduleCd, string? columnKey)
    {
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            return false;
        }
        // Customer bank is optional for convert — missing BANK_CD should not reject the whole file.
        if (string.Equals(columnKey, "BANK_CD", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(moduleCd, "CustomerInfoCustomerExt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(moduleCd, "CustomerInfo", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return IsOptionalProductInfoReference(moduleCd, columnKey);
    }

    private static bool IsOptionalProductInfoReference(string? moduleCd, string? columnKey)
    {
        if (!string.Equals(moduleCd, "ProductInfo", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(columnKey))
        {
            return false;
        }
        // Template keys are *_CD only (NormalizeImportTemplateKey maps leftover *_ID → *_CD).
        return string.Equals(columnKey, "PRODUCT_KIND_CD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnKey, "STORE_CD", StringComparison.OrdinalIgnoreCase);
    }

    private static void TrimValidateResults(List<ExcelImportResultRowDto> validateResults, int maxStored)
    {
        if (validateResults.Count <= maxStored)
        {
            return;
        }
        // Prefer keeping ERRORs, then SKIP/WARNING; drop excess to avoid OOM on 10k+ voucher rows.
        var errors = validateResults.Where(x => string.Equals(x.Status, "ERROR", StringComparison.OrdinalIgnoreCase)).Take(maxStored).ToList();
        var remaining = maxStored - errors.Count;
        var others = remaining <= 0
            ? new List<ExcelImportResultRowDto>()
            : validateResults
                .Where(x => !string.Equals(x.Status, "ERROR", StringComparison.OrdinalIgnoreCase))
                .Take(remaining)
                .ToList();
        validateResults.Clear();
        validateResults.AddRange(errors);
        validateResults.AddRange(others);
    }
}

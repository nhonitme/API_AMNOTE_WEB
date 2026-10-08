using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reports;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;
using DevExpress.DataProcessing;
using DevExpress.Office.Utils;
using DevExpress.SpreadsheetSource;
using DevExpress.SpreadsheetSource.Implementation;
using DevExpress.XtraGauges.Core.Primitive;
using DevExpress.XtraReports.Web.WebDocumentViewer.Native.Services;
using DevExpress.XtraRichEdit.Model;
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Helpers
{
    public class ExcelHelper
    {
        public static Task<(bool IsValid, string? ErrorKey)> ValidateExcelFileAsync(IFormFile file, IEnumerable<string>? allowedExtensions = null, long maxSizeBytes = 10L * 1024 * 1024)
        {
            if (file == null || file.Length == 0)
                return Task.FromResult((false, (string?)"VAL_SELECT_EXCEL_FILE"));

            var extensions = (allowedExtensions ?? new[] { ".xlsx", ".xls" })
                .Select(x => x?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.StartsWith('.') ? x : $".{x}")
                .ToArray();

            if (!extensions.Any(ext => file.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                return Task.FromResult((false, (string?)"VAL_EXCEL_FORMAT"));

            if (file.Length > maxSizeBytes)
                return Task.FromResult((false, (string?)"VAL_EXCEL_SIZE"));

            return Task.FromResult((true, (string?)null));
        }

        public static async Task<List<Dictionary<string, object>>> ReadExcelDataAsync(IFormFile file, string moduleCd)
        {
            if (file == null)
                return new List<Dictionary<string, object>>();

            var keys = (await Common.GetExcelTemplateKeysAsync(moduleCd)).Keys.ToList();

            using var stream = file.OpenReadStream();
            return await ImportFromExcelAsync(stream, keys);
        }

        public static async Task<List<Dictionary<string, object>>> ReadExcelDataAsync(
            Stream fileStream,
            string moduleCd,
            string companyCd,
            string? sheetName = null,
            Func<int, int, CancellationToken, Task>? onProgress = null,
            CancellationToken cancellationToken = default)
        {
            if (fileStream == null)
                return new List<Dictionary<string, object>>();

            var keys = (await Common.GetExcelTemplateKeysAsync(moduleCd, companyCd)).Keys.ToList();
            return await ImportFromExcelAsync(fileStream, keys, onProgress, cancellationToken, sheetName);
        }


        /// <summary>
        /// Export menu B
        /// from GridData to Excel
        /// - Header Left / Right
        /// - TITLE
        /// - SIGNATURE Left, Center, Right
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="columnMapping"></param>
        /// <returns></returns>
        public static Task<MemoryStream> ExportToExcelAsync<T>(
            IEnumerable<T> data,
            Dictionary<string, string> columnMapping)
        {
            return ExportToExcelAsync(data, columnMapping, formatTypes: null);
        }

        public static async Task<MemoryStream> ExportToExcelAsync<T>(
            IEnumerable<T> data,
            Dictionary<string, string> columnMapping,
            IReadOnlyDictionary<string, string?>? formatTypes,
            IReadOnlyDictionary<string, Dictionary<string, string>>? sysCodeDisplayMap = null)
        {
            ExcelPackage.License.SetNonCommercialPersonal("AMNote System");

            var stream = new MemoryStream();
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.Add("Data");

            int rowIndex = 1;

            int colIndex = 1;
            foreach (var column in columnMapping)
            {
                var headerCell = worksheet.Cells[rowIndex, colIndex];
                headerCell.Value = column.Value;
                headerCell.Style.Font.Bold = true;
                ApplyThinBorder(headerCell);
                colIndex++;
            }

            rowIndex++;
            foreach (var item in data)
            {
                colIndex = 1;
                foreach (var column in columnMapping)
                {
                    var cell = worksheet.Cells[rowIndex, colIndex];
                    // Always materialize the cell - empty trailing columns otherwise lose Right border in Excel.
                    cell.Value = string.Empty;

                    var property = typeof(T).GetProperty(
                        column.Key,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (property != null)
                    {
                        var value = property.GetValue(item);
                        if (value is not null && !string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture)))
                        {
                            var cellValue = ResolveSysCodeDisplayValue(column.Key, value, sysCodeDisplayMap) ?? value;
                            cell.Value = cellValue;
                            ApplyExcelFormat(cell, cellValue, formatTypes?.GetValueOrDefault(column.Key));
                        }
                    }

                    ApplyThinBorder(cell);
                    colIndex++;
                }
                rowIndex++;
            }

            if (worksheet.Dimension != null)
            {
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }

            await package.SaveAsync();
            stream.Position = 0;
            return stream;
        }

        private static object? ResolveSysCodeDisplayValue(
            string fieldName,
            object value,
            IReadOnlyDictionary<string, Dictionary<string, string>>? sysCodeDisplayMap)
        {
            if (sysCodeDisplayMap == null || !sysCodeDisplayMap.TryGetValue(fieldName, out var codeMap) || codeMap == null)
            {
                return null;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return codeMap.TryGetValue(text, out var display) && !string.IsNullOrWhiteSpace(display)
                ? display
                : null;
        }

        private static void ApplyThinBorder(ExcelRange cell)
        {
            cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
            cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            cell.Style.Border.Left.Style = ExcelBorderStyle.Thin;
            cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        }

        private static void ApplyExcelFormat(ExcelRange cell, object? value, string? formatType)
        {
            var numberFormat = ResolveExcelNumberFormat(formatType);
            if (numberFormat == null)
            {
                return;
            }

            if (value is DateTime or DateTimeOffset or decimal or double or float or int or long or short or byte)
            {
                cell.Style.Numberformat.Format = numberFormat;
                return;
            }

            // YMD / YM strings: keep text but still prefer readable Excel date format when parseable.
            if (value is string text)
            {
                var trimmed = text.Trim();
                if (trimmed.Length == 8 && trimmed.All(char.IsDigit) &&
                    DateTime.TryParseExact(trimmed, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ymd))
                {
                    cell.Value = ymd;
                    cell.Style.Numberformat.Format = numberFormat;
                    return;
                }

                if (trimmed.Length == 6 && trimmed.All(char.IsDigit) &&
                    DateTime.TryParseExact(trimmed + "01", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var ym))
                {
                    cell.Value = ym;
                    cell.Style.Numberformat.Format = "yyyy-MM";
                }
            }
        }

        private static string? ResolveExcelNumberFormat(string? formatType)
        {
            return Common.NormalizeToken(formatType) switch
            {
                "number0" or "integer" => "#,##0",
                "number1" => "#,##0.0",
                "number2" or "amount" or "quantity" or "unitprice" => "#,##0.00",
                "number3" => "#,##0.000",
                "number4" => "#,##0.0000",
                "date" => "dd/mm/yyyy",
                "datetime" => "dd/mm/yyyy hh:mm",
                _ => null
            };
        }

        public static async Task<MemoryStream> ExportToExcelAsync(
            IEnumerable<IDictionary<string, object?>> data,
            Dictionary<string, string> columnMapping,
            IReadOnlyDictionary<string, Dictionary<string, string>>? sysCodeDisplayMap = null)
        {
            ExcelPackage.License.SetNonCommercialPersonal("AMNote System");

            var stream = new MemoryStream();
            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.Add("Data");

            int colIndex = 1;
            foreach (var column in columnMapping)
            {
                worksheet.Cells[1, colIndex].Value = column.Value;
                worksheet.Cells[1, colIndex].Style.Font.Bold = true;
                colIndex++;
            }

            int rowIndex = 2;
            foreach (var item in data)
            {
                colIndex = 1;
                foreach (var column in columnMapping)
                {
                    if (item.TryGetValue(column.Key, out var value) && value != null)
                    {
                        var cellValue = ResolveSysCodeDisplayValue(column.Key, value, sysCodeDisplayMap) ?? value;
                        worksheet.Cells[rowIndex, colIndex].Value = cellValue;
                    }

                    colIndex++;
                }

                rowIndex++;
            }

            if (worksheet.Dimension != null)
            {
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
            }

            await package.SaveAsync();
            stream.Position = 0;
            return stream;
        }

        public static async Task<MemoryStream> BuildCustomerExcelExportAsync<T>(IEnumerable<T> data, string moduleCd, string lang)
        {
            var templateColumns = await Common.GetExcelTemplateColumnInfosAsync(moduleCd);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(templateColumns, lang);
            return await ExportToExcelAsync(data, columnMapping);
        }

        /// <summary>
        /// Export the import template (headers + column explanations) for a module.
        /// </summary>
        /// <param name="templateColumns"></param>
        /// <param name="lang"></param>
        /// <param name="moduleCd"></param>
        /// <returns></returns>
        public static async Task<MemoryStream> ExportTemplateAsync2(List<ExcelTemplateColumnInfo> templateColumns, string lang, string moduleCd)
        {
            ExcelPackage.License.SetNonCommercialPersonal("AMNote System");

            var stream = new MemoryStream();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Template");

                for (int i = 0; i < templateColumns.Count; i++)
                {
                    var iStart_row = 1;
                    var col = templateColumns[i];

                    var col_caption = ResolveTemplateColumnCaption(col, lang);

                    if (string.IsNullOrWhiteSpace(col_caption)
                        || string.Equals(col_caption, col.FIELD_NAME, StringComparison.OrdinalIgnoreCase))
                    {
                        col_caption = await ResolveTemplateColumnCaptionAsync(col.FIELD_NAME, lang, moduleCd);
                    }

                    if (col.IS_REQUIRED == "1" || col.IS_PRIMARY_KEY == "1")
                        col_caption += " (*)";
                    worksheet.Cells[iStart_row, i + 1].Value = col_caption;
                    worksheet.Cells[iStart_row, i + 1].Style.Font.Bold = true;


                    // Row 1 = header, row 2 = data entry, row 3 = blank separator, row 4+ = explanation.
                    // Blank row 3 stops import before the lookup/explanation block.
                    iStart_row = 4;
                    worksheet.Cells[iStart_row++, i + 1].Value = await Common.getLanguage("FILE_MANAGER_30", lang);

                    if (col.EXPLAIN_TABLE_QUERY + "" != "")  // if (col.EXPLAIN_TABLE + "" != "" && col.EXPLAIN_COLUMN_CD + "" != "" && col.EXPLAIN_COLUMN_NM + "" != "")
                    {
                        var EXPLAIN_TABLE_QUERY = (col.EXPLAIN_TABLE_QUERY + "").Replace("@COMPANY_CD", Common.GetCompanyCode()).Replace("@COMPANYCD", Common.GetCompanyCode());
                        
                        var explanationLines = (await ImportColumnExplanationProviders.GetImportColumnExplanationAsync(lang, EXPLAIN_TABLE_QUERY, Common.GetCompanyCode())).ToList();
                        if (explanationLines.Count > 0)
                        {
                            foreach (var explanationLine in explanationLines)
                            {
                                var NM_lang = Common.getLanguageV2(explanationLine.NM_VIET, lang);
                                if (NM_lang == "")
                                {
                                    if (lang == "ENG")
                                        NM_lang = explanationLine.NM_ENG;
                                    else if (lang == "KOR")
                                    {
                                        NM_lang = explanationLine.NM_KOR;
                                        if (NM_lang == "")
                                            NM_lang = explanationLine.NM_ENG;
                                    }
                                    else if (lang == "CHINA")
                                    {
                                        NM_lang = explanationLine.NM_CHINA;
                                        if (NM_lang == "")
                                            NM_lang = explanationLine.NM_ENG;
                                    }
                                    //
                                    if (NM_lang == "")
                                        NM_lang = explanationLine.NM_VIET;
                                }

                                worksheet.Cells[iStart_row++, i + 1].Value = explanationLine.CD + " - " + NM_lang;
                            }
                        }
                    }

                }

                if (worksheet.Dimension != null)
                {
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                }

                await package.SaveAsAsync(stream);
            }

            stream.Position = 0;

            return stream;
        }

        private static string ResolveTemplateColumnCaption(ExcelTemplateColumnInfo col, string lang)
        {
            var labelText = Common.NormalizeNullableText(col.LABEL_TEXT);
            if (!string.IsNullOrWhiteSpace(labelText))
            {
                var parts = labelText.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    return ReportLanguageHelper.LocalizeLabel(parts[0], lang)
                        + " - "
                        + ReportLanguageHelper.LocalizeLabel(parts[1], lang);
                }
            }

            return ReportLanguageHelper.ResolveDisplayLabel(col.LABEL_TEXT, lang);
        }

        private static async Task<string> ResolveTemplateColumnCaptionAsync(string columnKey, string lang, string moduleCd)
        {
            var normalizedModule = moduleCd?.Trim().ToUpperInvariant();
            var normalizedColumnKey = Common.NormalizeNullableText(columnKey) ?? string.Empty;
            var normalizedColumn = normalizedColumnKey.ToUpperInvariant();

            if (normalizedModule == "DEPARTMENTINFO" && normalizedColumn == "PRODUCT_CD")
            {
                return await Common.getLanguage("Product_department", lang);
            }

            if (normalizedModule == "CUSTOMERINFOCUSTOMEREXT")
            {
                return normalizedColumn switch
                {
                    "CUSTOMER_TYPE" => await Common.getLanguage("lbloutside", lang),
                    "CATEGORY_CD" => await Common.getLanguage("lbl_CustomerCategory", lang),
                    "CUSTOMER_NM" => await Common.getLanguage("CUSTOMER_NM", lang),
                    "CUSTOMER_NM_VIET" => $"{await Common.getLanguage("CUSTOMER_NM", lang)} (VIET)",
                    "CUSTOMER_NM_ENG" => $"{await Common.getLanguage("CUSTOMER_NM", lang)} (ENG)",
                    "CUSTOMER_NM_KOR" => $"{await Common.getLanguage("CUSTOMER_NM", lang)} (KOR)",
                    "CUSTOMER_NM_CHINA" => $"{await Common.getLanguage("CUSTOMER_NM", lang)} (CHINA)",
                    _ => await Common.getLanguage(normalizedColumnKey, lang)
                };
            }

            return await Common.getLanguage(normalizedColumnKey, lang);
        }

        public static async Task<MemoryStream> ExportTemplateAsync(List<string> headers, string lang)
        {
            ExcelPackage.License.SetNonCommercialPersonal("AMNote System");

            var stream = new MemoryStream();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Template");

                for (int i = 0; i < headers.Count; i++)
                {
                    worksheet.Cells[1, i + 1].Value = await Common.getLanguage(headers[i], lang);
                    worksheet.Cells[1, i + 1].Style.Font.Bold = true;
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                await package.SaveAsAsync(stream);
            }

            stream.Position = 0;
            return stream;
        }

        public static async Task<MemoryStream> ExportTemplateAsync(Dictionary<string, string> columnMapping)
        {
            ExcelPackage.License.SetNonCommercialPersonal("AMNote System");

            var stream = new MemoryStream();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Template");

                int columnIndex = 1;
                foreach (var column in columnMapping)
                {
                    worksheet.Cells[1, columnIndex].Value = column.Value;
                    worksheet.Cells[1, columnIndex].Style.Font.Bold = true;
                    columnIndex++;
                }

                if (worksheet.Dimension != null)
                {
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                }

                await package.SaveAsAsync(stream);
            }

            stream.Position = 0;
            return stream;
        }

        public static async Task<List<Dictionary<string, object>>> ImportFromExcelAsync(Stream fileStream, List<string> expectedHeaders)
        {
            return await ImportFromExcelAsync(fileStream, expectedHeaders, null, CancellationToken.None);
        }

        /// <summary>
        /// Excel row number key (1-based) stamped onto every imported row.
        /// </summary>
        public const string ExcelImportRowNoKey = "__EXCEL_ROW_NO";

        public static async Task<List<Dictionary<string, object>>> ImportFromExcelAsync(
            Stream fileStream,
            List<string> expectedHeaders,
            Func<int, int, CancellationToken, Task>? onProgress,
            CancellationToken cancellationToken = default,
            string? sheetName = null)
        {
            ExcelPackage.License.SetNonCommercialPersonal("AMNote System");

            var result = new List<Dictionary<string, object>>();

            using var package = new ExcelPackage(fileStream);

            var worksheet = package.Workbook.Worksheets[0];
            if (!string.IsNullOrEmpty(sheetName))
                worksheet = package.Workbook.Worksheets[sheetName];

            if (worksheet?.Dimension == null)
                return result;

            var lastColumn = worksheet.Dimension.End.Column;
            var lastRow = worksheet.Dimension.End.Row;

            var importColumnCount = Math.Min(lastColumn, expectedHeaders.Count);

            if (importColumnCount == 0)
                return result;

            var totalRows = Math.Max(0, lastRow - 1);
            var processedRows = 0;
            var explanationLabels = await BuildExcelImportExplanationLabelsAsync();

            if (onProgress != null)
            {
                await onProgress(0, totalRows, cancellationToken);
            }

            for (int row = 2; row <= lastRow; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                bool hasData = false;

                for (int col = 1; col <= importColumnCount; col++)
                {
                    var key = expectedHeaders[col - 1];
                    var cellValue = worksheet.Cells[row, col].Value;

                    if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                    {
                        rowData[key] = cellValue;
                        hasData = true;
                    }
                    else
                    {
                        rowData[key] = null!;
                    }
                }

                if (!hasData)
                {
                    // Blank row ends the data block (explanations follow).
                    break;
                }

                if (IsExcelImportExplanationLabelRow(rowData, explanationLabels))
                {
                    // Explanation header (FILE_MANAGER_30) - the reference block below is not imported.
                    break;
                }

                rowData[ExcelImportRowNoKey] = row;
                result.Add(rowData);
                processedRows++;

                if (onProgress != null && (processedRows == 1 || processedRows % 50 == 0 || row == lastRow))
                {
                    await onProgress(processedRows, totalRows, cancellationToken);
                }
            }

            if (onProgress != null)
            {
                await onProgress(processedRows, processedRows, cancellationToken);
            }

            return result;
        }

        private static async Task<HashSet<string>> BuildExcelImportExplanationLabelsAsync()
        {
            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "FILE_MANAGER_30"
            };

            foreach (var lang in new[] { "VIET", "ENG", "KOR", "CHINA" })
            {
                try
                {
                    var text = (await Common.getLanguage("FILE_MANAGER_30", lang) ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        labels.Add(text);
                    }
                }
                catch
                {
                    // ignore language lookup failures
                }
            }

            return labels;
        }

        private static bool IsExcelImportExplanationLabelRow(
            Dictionary<string, object> rowData,
            HashSet<string> explanationLabels)
        {
            foreach (var value in rowData.Values)
            {
                var text = (value + "").Trim();
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (explanationLabels.Contains(text))
                {
                    return true;
                }

                // Khớp thêm nhãn giải thích kiểu e-invoice detail template.
                if (text.StartsWith("giai thich du lieu", StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith("giải thích dữ liệu", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

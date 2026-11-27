using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reports;
using System.Data;
using System.Globalization;

namespace API_AMNOTE_WEB.Reporting
{
    /// <summary>
    /// PDF-only section grouping for TAX_VAT_INVOICE_LIST (/tax/vat/invoice-list).
    /// Injects SECTION rows + footer total texts without changing the grid SP result shape for UI.
    /// Fixed section codes prefer sys_code (VAT_*_CATEGORY), same as the web grid.
    /// </summary>
    internal static class VatInvoiceListPrintLayout
    {
        private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
        private const string ExcludedInfoTypeLabel = "không lên bảng kê";

        /// <summary>
        /// Vietnamese number grouping without CultureInfo("vi-VN"), which is unavailable
        /// under DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 on Linux.
        /// </summary>
        private static readonly NumberFormatInfo VietnamNumberFormat = new()
        {
            NumberGroupSeparator = ".",
            NumberDecimalSeparator = ",",
            NumberGroupSizes = new[] { 3 }
        };

        private static readonly string[] SellSectionCodes = { "1", "2", "3", "4", "5" };
        private static readonly string[] BuySectionCodes = { "1a", "1b", "2", "3", "5", "6" };

        private static readonly Dictionary<string, string> SellFallbackLabels = new(Comparer)
        {
            ["1"] = "1. Hàng hóa, dịch vụ không chịu thuế GTGT",
            ["2"] = "2. Hàng hóa, dịch vụ chịu thuế suất GTGT 0%",
            ["3"] = "3. Hàng hóa, dịch vụ chịu thuế suất GTGT 5%",
            ["4"] = "4. Hàng hóa, dịch vụ chịu thuế suất GTGT 10%",
            ["5"] = "5. Hàng hóa, dịch vụ không tính thuế GTGT",
            ["6"] = "Không lên bảng kê",
        };

        private static readonly Dictionary<string, string> BuyFallbackLabels = new(Comparer)
        {
            ["1a"] = "1a. Hàng hóa, dịch vụ dùng riêng cho SXKD chịu thuế GTGT (nhóm 1a)",
            ["1b"] = "1b. Hàng hóa, dịch vụ dùng riêng cho SXKD chịu thuế GTGT (nhóm 1b)",
            ["2"] = "2. Hàng hóa, dịch vụ dùng chung cho SXKD chịu thuế và không chịu thuế",
            ["3"] = "3. Hàng hóa, dịch vụ dùng cho dự án đầu tư",
            ["4"] = "Không lên bảng kê",
            ["5"] = "4. Hàng hóa, dịch vụ không đủ điều kiện khấu trừ",
            ["6"] = "5. Hàng hóa, dịch vụ không phải tổng hợp trên tờ khai 01/GTGT",
        };

        public static bool IsVatInvoiceListReport(string? reportCode)
        {
            return string.Equals(
                Common.NormalizeNullableText(reportCode),
                "TAX_VAT_INVOICE_LIST",
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>sys_code CODE_TYPE for invoice-list groups — mirrors getVatCategoryCodeType on web.</summary>
        public static string ResolveCategoryCodeType(string? vatDirection)
        {
            var direction = NormalizeDirection(vatDirection) ?? "SELL";
            return direction == "BUY" ? "VAT_BUY_CATEGORY" : "VAT_SELL_CATEGORY";
        }

        public static void Apply(
            DataTable table,
            string? reportLanguage,
            string? vatDirection,
            IEnumerable<SysCodeInfo>? catalogCodes = null)
        {
            if (table == null)
            {
                return;
            }

            var direction = NormalizeDirection(vatDirection);
            if (direction == null)
            {
                direction = "SELL";
            }

            var language = ReportLanguageHelper.NormalizeLanguage(reportLanguage);
            EnsureColumn(table, "__ROW_TYPE", typeof(string));
            EnsureColumn(table, "SUMMARY_LABEL", typeof(string));
            EnsureColumn(table, "TOTAL_TAXABLE_LINE_TEXT", typeof(string));
            EnsureColumn(table, "TOTAL_VAT_LINE_TEXT", typeof(string));
            EnsureColumn(table, "AMOUNT_UNIT_TEXT", typeof(string));
            EnsureColumn(table, "REPORT_TITLE_TEXT", typeof(string));
            EnsureColumn(table, "KY_TINH_THUE_TEXT", typeof(string));

            var detailRows = table.Rows.Cast<DataRow>()
                .Where(row => !IsSectionOrTotalRow(row))
                .ToList();

            var (taxableTotal, vatTotal) = SumTotals(detailRows, direction);
            var taxableLine = BuildTotalLine(direction, isVat: false, taxableTotal, language);
            var vatLine = BuildTotalLine(direction, isVat: true, vatTotal, language);
            var amountUnit = ResolveAmountUnitText(detailRows, language);
            var reportTitle = ResolveReportTitle(detailRows, direction, language);
            var periodText = ResolvePeriodText(detailRows);

            var catalogList = (catalogCodes ?? Enumerable.Empty<SysCodeInfo>()).ToList();
            var sectionCodes = ResolveFixedSectionCodes(direction, catalogList);
            var grouped = detailRows
                .GroupBy(row => NormalizeInfoType(ReadText(row, "VAT_INFO_TYPE")), Comparer)
                .ToDictionary(group => group.Key, group => group.ToList(), Comparer);

            var extraCodes = grouped.Keys
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Where(code => !sectionCodes.Any(fixedCode => Comparer.Equals(fixedCode, code)))
                .Where(code => !IsExcludedInfoType(direction, code))
                .OrderBy(code => ResolveSortKey(direction, code), StringComparer.Ordinal)
                .ToList();

            var orderedCodes = sectionCodes
                .Concat(extraCodes)
                .Distinct(Comparer)
                .ToList();

            var output = table.Clone();
            var templateCompanyCd = detailRows
                .Select(row => ReadText(row, "COMPANY_CD"))
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                ?? string.Empty;
            var sort = 0;
            foreach (var code in orderedCodes)
            {
                var sectionRow = CreateSectionRow(
                    output,
                    direction,
                    code,
                    ResolveSectionLabel(direction, code, language),
                    $"SECTION:{direction}:{code}:{sort++}",
                    reportTitle,
                    periodText,
                    amountUnit,
                    taxableLine,
                    vatLine,
                    templateCompanyCd);
                output.Rows.Add(sectionRow);

                if (!grouped.TryGetValue(code, out var rowsForCode))
                {
                    continue;
                }

                foreach (var source in rowsForCode.OrderBy(row => ResolveDetailSort(row)))
                {
                    var detail = output.NewRow();
                    detail.ItemArray = (object[])source.ItemArray.Clone();
                    detail["__ROW_TYPE"] = "DETAIL";
                    detail["SUMMARY_LABEL"] = string.Empty;
                    detail["TOTAL_TAXABLE_LINE_TEXT"] = taxableLine;
                    detail["TOTAL_VAT_LINE_TEXT"] = vatLine;
                    if (string.IsNullOrWhiteSpace(ReadText(detail, "AMOUNT_UNIT_TEXT")))
                    {
                        detail["AMOUNT_UNIT_TEXT"] = amountUnit;
                    }
                    if (string.IsNullOrWhiteSpace(ReadText(detail, "REPORT_TITLE_TEXT")))
                    {
                        detail["REPORT_TITLE_TEXT"] = reportTitle;
                    }
                    if (string.IsNullOrWhiteSpace(ReadText(detail, "KY_TINH_THUE_TEXT")))
                    {
                        detail["KY_TINH_THUE_TEXT"] = periodText;
                    }
                    output.Rows.Add(detail);
                }
            }

            // Orphan rows without VAT_INFO_TYPE still print after fixed sections.
            if (grouped.TryGetValue(string.Empty, out var untypedRows) && untypedRows.Count > 0)
            {
                var sectionRow = CreateSectionRow(
                    output,
                    direction,
                    string.Empty,
                    ReportLanguageHelper.LocalizeLabelOrFallback(
                        "VAT_INFO_TYPE_EMPTY",
                        "Chưa phân loại",
                        language),
                    $"SECTION:{direction}:EMPTY:{sort++}",
                    reportTitle,
                    periodText,
                    amountUnit,
                    taxableLine,
                    vatLine,
                    templateCompanyCd);
                output.Rows.Add(sectionRow);

                foreach (var source in untypedRows)
                {
                    var detail = output.NewRow();
                    detail.ItemArray = (object[])source.ItemArray.Clone();
                    detail["__ROW_TYPE"] = "DETAIL";
                    detail["SUMMARY_LABEL"] = string.Empty;
                    detail["TOTAL_TAXABLE_LINE_TEXT"] = taxableLine;
                    detail["TOTAL_VAT_LINE_TEXT"] = vatLine;
                    output.Rows.Add(detail);
                }
            }

            // Empty dataset: still show section headers + footer totals.
            if (output.Rows.Count == 0)
            {
                foreach (var code in sectionCodes)
                {
                    var sectionRow = CreateSectionRow(
                        output,
                        direction,
                        code,
                        ResolveSectionLabel(direction, code, language),
                        $"SECTION:{direction}:{code}:{sort++}",
                        reportTitle,
                        periodText,
                        amountUnit,
                        taxableLine,
                        vatLine,
                        templateCompanyCd);
                    output.Rows.Add(sectionRow);
                }
            }

            table.Rows.Clear();
            foreach (DataRow row in output.Rows)
            {
                table.ImportRow(row);
            }
        }

        private static DataRow CreateSectionRow(
            DataTable output,
            string direction,
            string infoType,
            string summaryLabel,
            string rowKey,
            string reportTitle,
            string periodText,
            string amountUnit,
            string taxableLine,
            string vatLine,
            string companyCd)
        {
            var sectionRow = output.NewRow();
            FillNonNullableDefaults(sectionRow, output);
            sectionRow["__ROW_TYPE"] = "SECTION";
            sectionRow["SUMMARY_LABEL"] = summaryLabel;
            sectionRow["VAT_INFO_TYPE"] = infoType;
            sectionRow["VAT_DIRECTION"] = direction;
            sectionRow["ROW_KEY"] = rowKey;
            if (HasColumn(output, "COMPANY_CD"))
            {
                sectionRow["COMPANY_CD"] = companyCd;
            }
            sectionRow["REPORT_TITLE_TEXT"] = reportTitle;
            sectionRow["KY_TINH_THUE_TEXT"] = periodText;
            sectionRow["AMOUNT_UNIT_TEXT"] = amountUnit;
            sectionRow["TOTAL_TAXABLE_LINE_TEXT"] = taxableLine;
            sectionRow["TOTAL_VAT_LINE_TEXT"] = vatLine;
            return sectionRow;
        }

        /// <summary>
        /// DataTable from ConvertToDataTable often marks string columns AllowDBNull=false.
        /// Section rows must seed those columns before Rows.Add.
        /// </summary>
        private static void FillNonNullableDefaults(DataRow row, DataTable table)
        {
            foreach (DataColumn column in table.Columns)
            {
                if (column.AllowDBNull)
                {
                    continue;
                }

                if (column.DataType == typeof(string))
                {
                    row[column] = string.Empty;
                    continue;
                }

                if (column.DataType == typeof(decimal)
                    || column.DataType == typeof(double)
                    || column.DataType == typeof(float)
                    || column.DataType == typeof(int)
                    || column.DataType == typeof(long)
                    || column.DataType == typeof(short)
                    || column.DataType == typeof(byte))
                {
                    row[column] = Convert.ChangeType(0, column.DataType);
                    continue;
                }

                if (column.DataType == typeof(bool))
                {
                    row[column] = false;
                    continue;
                }

                if (column.DataType == typeof(DateTime))
                {
                    row[column] = DateTime.MinValue;
                }
            }
        }

        private static bool HasColumn(DataTable table, string field)
            => ResolveColumn(table, field) != null;

        private static (decimal Taxable, decimal Vat) SumTotals(IReadOnlyList<DataRow> rows, string direction)
        {
            decimal taxable = 0m;
            decimal vat = 0m;
            foreach (var row in rows)
            {
                var infoType = NormalizeInfoType(ReadText(row, "VAT_INFO_TYPE"));
                if (IsExcludedInfoType(direction, infoType))
                {
                    continue;
                }

                var amount = ReadDecimal(row, "VAT_TAXABLE_AMOUNT");
                var vatAmount = ReadDecimal(row, "VAT_AMOUNT");
                vat += vatAmount;

                if (direction == "SELL")
                {
                    // Form 01/GTGT (*): doanh thu chịu thuế — exclude non-taxable / non-declared buckets.
                    if (Comparer.Equals(infoType, "2")
                        || Comparer.Equals(infoType, "3")
                        || Comparer.Equals(infoType, "4"))
                    {
                        taxable += amount;
                    }
                }
                else
                {
                    taxable += amount;
                }
            }

            return (taxable, vat);
        }

        private static string BuildTotalLine(string direction, bool isVat, decimal amount, string language)
        {
            string key;
            string fallback;
            if (direction == "BUY")
            {
                key = isVat ? "VAT_TOTAL_ACCOUNT_BUY_VAT" : "VAT_TOTAL_ACCOUNT_BUY";
                fallback = isVat
                    ? "Tổng thuế GTGT của hàng hóa, dịch vụ mua vào"
                    : "Tổng giá trị hàng hoá, dịch vụ mua vào";
            }
            else
            {
                key = isVat ? "VAT_TOTAL_VATACCOUNT" : "VAT_TOTAL_ACCOUNT_NO119";
                fallback = isVat
                    ? "Tổng thuế GTGT của hàng hóa, dịch vụ bán ra"
                    : "Tổng doanh thu hàng hoá, dịch vụ bán ra chịu thuế GTGT (*)";
            }

            var label = ReportLanguageHelper.LocalizeLabelOrFallback(key, fallback, language);
            var amountText = amount.ToString("#,##0.###", VietnamNumberFormat);
            return $"{label} : {amountText}";
        }

        private static string ResolveSectionLabel(string direction, string code, string language)
        {
            var fallbackMap = direction == "BUY" ? BuyFallbackLabels : SellFallbackLabels;
            fallbackMap.TryGetValue(code, out var fallback);
            fallback ??= code;

            var key = direction == "BUY"
                ? $"VAT_BUY_CATEGORY_{code.ToUpperInvariant()}"
                : $"VAT_SELL_CATEGORY_{code.ToUpperInvariant()}";
            var translated = ReportLanguageHelper.LocalizeLabelOrFallback(key, fallback, language).Trim();
            if (string.IsNullOrWhiteSpace(translated))
            {
                return fallback;
            }

            // Strip [[RATE:...]] markers from sys_code NOTE-style translations.
            translated = System.Text.RegularExpressions.Regex.Replace(
                translated,
                @"\[\[RATE:[^\]]*\]\]",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            translated = System.Text.RegularExpressions.Regex.Replace(translated, @"\s{2,}", " ").Trim();

            if (translated.Length == 0)
            {
                return fallback;
            }

            if (char.IsDigit(translated[0]) || Comparer.Equals(translated, code) || Comparer.Equals(translated, fallback))
            {
                return translated;
            }

            return $"{code}. {translated}";
        }

        private static string ResolveAmountUnitText(IReadOnlyList<DataRow> rows, string language)
        {
            var existing = rows
                .Select(row => ReadText(row, "AMOUNT_UNIT_TEXT"))
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            if (!string.IsNullOrWhiteSpace(existing))
            {
                if (existing!.Contains(':'))
                {
                    return existing;
                }

                var label = ReportLanguageHelper.LocalizeLabelOrFallback("lblmonetary", "Đơn vị tiền tệ", language);
                return $"{label}: {existing}";
            }

            var unitLabel = ReportLanguageHelper.LocalizeLabelOrFallback("lblmonetary", "Đơn vị tiền tệ", language);
            return $"{unitLabel}: VND";
        }

        private static string ResolveReportTitle(IReadOnlyList<DataRow> rows, string direction, string language)
        {
            var existing = rows
                .Select(row => ReadText(row, "REPORT_TITLE_TEXT"))
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing!;
            }

            var key = direction == "BUY" ? "TSvat_list_BUY" : "TSvat_list_SELL";
            var fallback = direction == "BUY"
                ? "BẢNG KÊ HOÁ ĐƠN, CHỨNG TỪ HÀNG HOÁ, DỊCH VỤ MUA VÀO"
                : "BẢNG KÊ HÓA ĐƠN, CHỨNG TỪ HÀNG HÓA, DỊCH VỤ BÁN RA";
            return ReportLanguageHelper.LocalizeLabelOrFallback(key, fallback, language);
        }

        private static string ResolvePeriodText(IReadOnlyList<DataRow> rows)
        {
            return rows
                .Select(row => ReadText(row, "KY_TINH_THUE_TEXT"))
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                ?? string.Empty;
        }

        private static bool IsSectionOrTotalRow(DataRow row)
        {
            var rowType = Common.NormalizeToken(ReadText(row, "__ROW_TYPE")).ToUpperInvariant();
            return rowType is "SECTION" or "SUBTOTAL" or "GRANDTOTAL" or "GRAND_TOTAL";
        }

        /// <summary>
        /// Same rule as web listFixedVatInvoiceListInfoTypes: prefer active sys_code catalog,
        /// else known fallback order. Skip codes marked "Không lên bảng kê".
        /// </summary>
        private static IReadOnlyList<string> ResolveFixedSectionCodes(
            string direction,
            IReadOnlyList<SysCodeInfo> catalogCodes)
        {
            var fromCatalog = catalogCodes
                .Where(IsUsableCatalogCode)
                .OrderBy(item => item.SORT_ORDER ?? 0)
                .ThenBy(item => NormalizeInfoType(item.CODE_CD), StringComparer.OrdinalIgnoreCase)
                .Select(item => NormalizeInfoType(item.CODE_CD))
                .Where(code => code.Length > 0)
                .Distinct(Comparer)
                .Where(code =>
                {
                    var item = catalogCodes.FirstOrDefault(entry =>
                        Comparer.Equals(NormalizeInfoType(entry.CODE_CD), code));
                    return !IsExcludedInfoType(direction, code, item?.NOTE ?? item?.CODE_NAME);
                })
                .ToList();

            if (fromCatalog.Count > 0)
            {
                return fromCatalog;
            }

            var fallback = direction == "BUY" ? BuySectionCodes : SellSectionCodes;
            return fallback
                .Where(code => !IsExcludedInfoType(direction, code))
                .ToList();
        }

        private static bool IsUsableCatalogCode(SysCodeInfo item)
        {
            if (item == null)
            {
                return false;
            }

            // Match web groupSysCodesByType: skip ISDEL=1 and require IS_ACTIVE=1.
            if (string.Equals(Common.NormalizeNullableText(item.ISDEL), "1", StringComparison.Ordinal))
            {
                return false;
            }

            return item.IS_ACTIVE == 1;
        }

        private static bool IsExcludedInfoType(string direction, string code, string? noteOrName = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            var fallbackMap = direction == "BUY" ? BuyFallbackLabels : SellFallbackLabels;
            fallbackMap.TryGetValue(code, out var fallback);

            var strippedNote = StripRateMarkers(noteOrName);
            foreach (var candidate in new[] { fallback, noteOrName, strippedNote })
            {
                if (string.Equals(
                        Common.NormalizeNullableText(candidate),
                        ExcludedInfoTypeLabel,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (direction == "BUY" && Comparer.Equals(code, "4"))
            {
                return true;
            }

            if (direction == "SELL" && Comparer.Equals(code, "6"))
            {
                return true;
            }

            return false;
        }

        private static string? StripRateMarkers(string? text)
        {
            var value = Common.NormalizeNullableText(text);
            if (value == null)
            {
                return null;
            }

            value = System.Text.RegularExpressions.Regex.Replace(
                value,
                @"\[\[RATE:[^\]]*\]\]",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            value = System.Text.RegularExpressions.Regex.Replace(value, @"\s{2,}", " ").Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string ResolveSortKey(string direction, string code)
        {
            if (direction == "BUY")
            {
                return code.ToLowerInvariant() switch
                {
                    "1a" => "01",
                    "1b" => "02",
                    "2" => "03",
                    "3" => "04",
                    "5" => "05",
                    "6" => "06",
                    "4" => "07",
                    _ => $"100|{code}"
                };
            }

            if (int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return number.ToString("00", CultureInfo.InvariantCulture);
            }

            return $"100|{code}";
        }

        private static string ResolveDetailSort(DataRow row)
        {
            return string.Join(
                "|",
                ReadText(row, "VAT_YMD"),
                ReadText(row, "VAT_SERIAL_NO"),
                ReadText(row, "VAT_CHIT_NO"),
                ReadText(row, "CHIT_ID"),
                ReadText(row, "CHITDETAIL_ID"));
        }

        private static string? NormalizeDirection(string? value)
        {
            var token = Common.NormalizeToken(value);
            return token switch
            {
                "buy" or "in" or "1" => "BUY",
                "sell" or "out" or "2" or "0" => "SELL",
                _ => null
            };
        }

        private static string NormalizeInfoType(string? value)
            => Common.NormalizeNullableText(value) ?? string.Empty;

        private static string ReadText(DataRow row, string field)
        {
            var column = ResolveColumn(row.Table, field);
            if (column == null)
            {
                return string.Empty;
            }

            return Common.NormalizeNullableText(row[column]?.ToString()) ?? string.Empty;
        }

        private static decimal ReadDecimal(DataRow row, string field)
        {
            var column = ResolveColumn(row.Table, field);
            if (column == null || row[column] == DBNull.Value || row[column] == null)
            {
                return 0m;
            }

            return decimal.TryParse(
                Convert.ToString(row[column], CultureInfo.InvariantCulture),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : 0m;
        }

        private static string? ResolveColumn(DataTable table, string field)
        {
            foreach (DataColumn column in table.Columns)
            {
                if (Comparer.Equals(column.ColumnName, field))
                {
                    return column.ColumnName;
                }
            }

            return null;
        }

        private static void EnsureColumn(DataTable table, string columnName, Type type)
        {
            if (ResolveColumn(table, columnName) != null)
            {
                return;
            }

            table.Columns.Add(columnName, type);
        }
    }
}

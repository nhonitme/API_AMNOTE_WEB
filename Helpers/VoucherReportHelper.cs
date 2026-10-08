using API_AMNOTE_WEB.Reports;
using System.Data;
using System.Globalization;

namespace API_AMNOTE_WEB.Helpers
{
    public static class VoucherReportHelper
    {
        public static string FormatVoucherDate(string? ymd, string? reportLanguage = null)
        {
            var language = ReportLanguageHelper.NormalizeLanguage(reportLanguage);
            if (!TryParseVoucherDate(ymd, out var date))
            {
                return ReportLanguageHelper.LocalizeLabel("VOUCHER_DATE_EMPTY", language);
            }

            return FormatLocalizedLabel("VOUCHER_DATE_FORMAT", language, date.Day, date.Month, date.Year);
        }

        public static void EnrichReportTable(DataTable table, string? reportLanguage)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            ReportDataTableHelper.EnsureColumn(table, "HEADER_DATE_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "SIGNATURE_DATE_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "AMOUNT_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "AMOUNT_IN_WORDS", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "EXCHANGE_RATE_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "CONVERTED_AMOUNT_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "ATTACHMENT_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REFERENCE_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "NOTE_TEXT", typeof(string));

            var language = ReportLanguageHelper.NormalizeLanguage(reportLanguage);

            foreach (DataRow row in table.Rows)
            {
                var chitYmd = Common.NormalizeNullableText(row.Table.Columns.Contains("CHIT_YMD") ? row["CHIT_YMD"]?.ToString() : null);
                row["HEADER_DATE_TEXT"] = FormatVoucherDate(chitYmd, language);
                row["SIGNATURE_DATE_TEXT"] = FormatVoucherDate(chitYmd, language);

                var localAmount = GetDecimalValue(row.Table.Columns.Contains("AMOUNT") ? row["AMOUNT"] : null);
                var foreignCurrencyType = Common.NormalizeToken(row.Table.Columns.Contains("FC_TYPE") ? row["FC_TYPE"]?.ToString() : null).ToUpperInvariant();
                var foreignCurrencyAmount = GetDecimalValue(row.Table.Columns.Contains("FC_AMOUNT") ? row["FC_AMOUNT"] : null);
                var foreignCurrencyRate = GetNullableDecimalValue(row.Table.Columns.Contains("FC_RATE") ? row["FC_RATE"] : null);
                var reportAmount = foreignCurrencyType.Length > 0 && foreignCurrencyAmount > 0m ? foreignCurrencyAmount : localAmount;

                row["AMOUNT_TEXT"] = ReportCurrencyHelper.FormatAmount(reportAmount, foreignCurrencyType.Length > 0 ? foreignCurrencyType : null);
                row["AMOUNT_IN_WORDS"] = ReportCurrencyHelper.ConvertAmountToWords(reportAmount, foreignCurrencyType.Length > 0 ? foreignCurrencyType : null, language);
                row["EXCHANGE_RATE_TEXT"] = ReportCurrencyHelper.FormatExchangeRate(foreignCurrencyType.Length > 0 ? foreignCurrencyType : null, foreignCurrencyRate);
                row["CONVERTED_AMOUNT_TEXT"] = ReportCurrencyHelper.FormatAmount(localAmount);

                var detailCount = GetIntValue(row.Table.Columns.Contains("DETAIL_COUNT") ? row["DETAIL_COUNT"] : null);
                row["ATTACHMENT_TEXT"] = detailCount > 0 ? FormatLocalizedLabel("DOCUMENT_COUNT_FORMAT", language, detailCount) : string.Empty;
                row["REFERENCE_TEXT"] = detailCount > 0 ? FormatLocalizedLabel("ORIGINAL_DOCUMENT_COUNT_FORMAT", language, detailCount) : string.Empty;

                var note = Common.NormalizeNullableText(row.Table.Columns.Contains("NOTE") ? row["NOTE"]?.ToString() : null);
                row["NOTE_TEXT"] = note == null
                    ? string.Empty
                    : $"{ReportLanguageHelper.LocalizeLabel("NOTE_LABEL", language)}: {note}";
            }
        }

        private static string FormatLocalizedLabel(string key, string language, params object[] values)
        {
            var format = ReportLanguageHelper.LocalizeLabel(key, language);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, values);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        private static decimal GetDecimalValue(object? value)
        {
            return GetNullableDecimalValue(value) ?? 0m;
        }

        private static int GetIntValue(object? value)
        {
            if (value == null || value is DBNull)
            {
                return 0;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue > int.MaxValue ? int.MaxValue : Convert.ToInt32(longValue);
            }

            var text = value.ToString();
            return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }

        private static decimal? GetNullableDecimalValue(object? value)
        {
            if (value == null || value is DBNull)
            {
                return null;
            }

            if (value is decimal decimalValue)
            {
                return decimalValue;
            }

            if (value is double doubleValue)
            {
                return Convert.ToDecimal(doubleValue);
            }

            if (value is float floatValue)
            {
                return Convert.ToDecimal(floatValue);
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            var text = value.ToString();
            if (text != null && decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            if (text != null && decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static bool TryParseVoucherDate(string? value, out DateTime date)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (normalized != null)
            {
                if (DateTime.TryParseExact(normalized, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return true;
                }

                if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    return true;
                }
            }

            date = default;
            return false;
        }
    }
}

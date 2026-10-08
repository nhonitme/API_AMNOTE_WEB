using API_AMNOTE_WEB.Reports;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace API_AMNOTE_WEB.Helpers
{
    public static class ReportDatePeriodHelper
    {
        public static void EnrichReportTable(DataTable table, IReadOnlyDictionary<string, string> query, string? reportLanguage)
        {
            var period = ResolvePeriod(query, reportLanguage);
            if (period == null)
            {
                EnrichLegacyReportDateFields(table);
                return;
            }

            EnsureReportDateColumns(table);

            foreach (DataRow row in table.Rows)
            {
                row["REPORT_PERIOD_KEY"] = period.Key;
                row["REPORT_PERIOD_TEXT"] = period.Text;
                row["REPORT_YEAR_TEXT"] = period.YearText;
                row["REPORT_MONTH_TEXT"] = period.MonthText;
                row["REPORT_QUARTER_TEXT"] = period.QuarterText;
                row["REPORT_DATE_RANGE_TEXT"] = period.DateRangeText;
                row["REPORT_AS_OF_DATE_TEXT"] = period.AsOfDateText;
                row["REPORT_DATE_TYPE"] = period.Key;
                row["REPORT_DATE_TEXT"] = period.AsOfDateText;
            }
        }

        private static ReportPeriodText? ResolvePeriod(IReadOnlyDictionary<string, string> query, string? reportLanguage)
        {
            var range = ResolveDateRange(query);
            if (range == null)
            {
                return null;
            }

            var fromDate = range.FromDate.Date;
            var toDate = range.ToDate.Date;
            if (fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            return BuildPeriodText(fromDate, toDate, ReportLanguageHelper.NormalizeLanguage(reportLanguage));
        }

        private static DateRange? ResolveDateRange(IReadOnlyDictionary<string, string> query)
        {
            DateTime? fromDate = null;
            DateTime? toDate = null;
            DateTime? singleDate = null;

            foreach (var item in query)
            {
                if (!TryParseDate(item.Value, out var parsedDate))
                {
                    continue;
                }

                switch (ResolveDateRole(item.Key))
                {
                    case QueryDateRole.From:
                        fromDate = parsedDate;
                        break;
                    case QueryDateRole.To:
                        toDate = parsedDate;
                        break;
                    case QueryDateRole.Single:
                        singleDate = parsedDate;
                        break;
                }
            }

            if (!fromDate.HasValue && !toDate.HasValue && singleDate.HasValue)
            {
                fromDate = singleDate;
                toDate = singleDate;
            }

            if (!fromDate.HasValue && !toDate.HasValue)
            {
                return null;
            }

            var resolvedFromDate = fromDate ?? toDate!.Value;
            var resolvedToDate = toDate ?? fromDate!.Value;
            return new DateRange(resolvedFromDate, resolvedToDate);
        }

        private static QueryDateRole ResolveDateRole(string key)
        {
            var token = Common.NormalizeToken(key);
            if (token.Length == 0 || (!token.Contains("date") && !token.Contains("ymd")))
            {
                return QueryDateRole.None;
            }

            if (IsFromDateToken(token))
            {
                return QueryDateRole.From;
            }

            if (IsToDateToken(token))
            {
                return QueryDateRole.To;
            }

            return QueryDateRole.Single;
        }

        private static bool IsFromDateToken(string token)
        {
            return token.StartsWith("from")
                || token.StartsWith("start")
                || token.StartsWith("begin")
                || ContainsAny(token, "fromdate", "fromymd", "datefrom", "ymdfrom", "startdate", "startymd", "begindate", "beginymd");
        }

        private static bool IsToDateToken(string token)
        {
            return token.StartsWith("to")
                || token.StartsWith("end")
                || token.StartsWith("until")
                || token.StartsWith("asof")
                || ContainsAny(token, "todate", "toymd", "dateto", "ymdto", "enddate", "endymd", "dateend", "ymdend", "untildate", "untilymd", "asofdate", "asofymd");
        }

        private static bool ContainsAny(string token, params string[] values)
        {
            foreach (var value in values)
            {
                if (token.Contains(value))
                {
                    return true;
                }
            }

            return false;
        }

        private static ReportPeriodText BuildPeriodText(DateTime fromDate, DateTime toDate, string language)
        {
            var yearText = FormatLabel(language, "REPORT_YEAR_FORMAT", "Năm {0}", toDate.Year);
            var monthText = FormatLabel(language, "REPORT_MONTH_FORMAT", "Tháng {0:00} năm {1}", toDate.Month, toDate.Year);
            var quarter = ((toDate.Month - 1) / 3) + 1;
            var quarterText = FormatLabel(language, "REPORT_QUARTER_FORMAT", "Quý {0} năm {1}", quarter, toDate.Year);
            var dateRangeText = FormatLabel(
                language,
                "REPORT_DATE_RANGE_FORMAT",
                "Từ ngày {0:00}/{1:00}/{2} đến ngày {3:00}/{4:00}/{5}",
                fromDate.Day,
                fromDate.Month,
                fromDate.Year,
                toDate.Day,
                toDate.Month,
                toDate.Year);
            var asOfDateText = FormatLabel(
                language,
                "REPORT_AS_OF_DATE_FORMAT",
                "Tại ngày {0:00}/{1:00}/{2}",
                toDate.Day,
                toDate.Month,
                toDate.Year);

            if (fromDate == toDate)
            {
                return new ReportPeriodText("REPORT_AS_OF_DATE", asOfDateText, yearText, monthText, quarterText, dateRangeText, asOfDateText);
            }

            if (IsFullYear(fromDate, toDate))
            {
                return new ReportPeriodText("REPORT_YEAR", yearText, yearText, monthText, quarterText, dateRangeText, asOfDateText);
            }

            if (IsFullQuarter(fromDate, toDate))
            {
                return new ReportPeriodText("REPORT_QUARTER", quarterText, yearText, monthText, quarterText, dateRangeText, asOfDateText);
            }

            if (IsFullMonth(fromDate, toDate))
            {
                return new ReportPeriodText("REPORT_MONTH", monthText, yearText, monthText, quarterText, dateRangeText, asOfDateText);
            }

            return new ReportPeriodText("REPORT_DATE_RANGE", dateRangeText, yearText, monthText, quarterText, dateRangeText, asOfDateText);
        }

        private static bool IsFullYear(DateTime fromDate, DateTime toDate)
        {
            return fromDate.Year == toDate.Year
                && fromDate.Month == 1
                && fromDate.Day == 1
                && toDate.Month == 12
                && toDate.Day == 31;
        }

        private static bool IsFullQuarter(DateTime fromDate, DateTime toDate)
        {
            if (fromDate.Year != toDate.Year)
            {
                return false;
            }

            var quarterStartMonth = ((fromDate.Month - 1) / 3) * 3 + 1;
            var quarterStart = new DateTime(fromDate.Year, quarterStartMonth, 1);
            var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
            return fromDate == quarterStart && toDate == quarterEnd;
        }

        private static bool IsFullMonth(DateTime fromDate, DateTime toDate)
        {
            var monthStart = new DateTime(fromDate.Year, fromDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            return fromDate == monthStart && toDate == monthEnd;
        }

        private static string FormatLabel(string language, string key, string fallback, params object[] args)
        {
            var template = ReportLanguageHelper.LocalizeLabel(key, language);
            if (string.Equals(template, key, StringComparison.OrdinalIgnoreCase))
            {
                template = fallback;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, args);
            }
            catch (FormatException)
            {
                return string.Format(CultureInfo.InvariantCulture, fallback, args);
            }
        }

        private static bool TryParseDate(string? value, out DateTime date)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                date = default;
                return false;
            }

            var formats = new[]
            {
                "yyyy-MM-dd",
                "yyyyMMdd",
                "dd/MM/yyyy",
                "MM/dd/yyyy",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:ssZ"
            };

            return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date)
                || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date);
        }

        private static void EnrichLegacyReportDateFields(DataTable table)
        {
            EnsureReportDateColumns(table);

            foreach (DataRow row in table.Rows)
            {
                var periodText = FirstNonEmpty(
                    ReadText(row, "REPORT_PERIOD_TEXT"),
                    ReadText(row, "REPORT_DATE_RANGE_TEXT"),
                    ReadText(row, "REPORT_AS_OF_DATE_TEXT"),
                    ReadText(row, "REPORT_DATE_TEXT"));

                var periodKey = FirstNonEmpty(
                    ReadText(row, "REPORT_PERIOD_KEY"),
                    ReadText(row, "REPORT_DATE_TYPE"),
                    InferLegacyPeriodKey(row));

                if (string.IsNullOrWhiteSpace(ReadText(row, "REPORT_PERIOD_TEXT")))
                {
                    row["REPORT_PERIOD_TEXT"] = periodText ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(ReadText(row, "REPORT_PERIOD_KEY")))
                {
                    row["REPORT_PERIOD_KEY"] = periodKey ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(ReadText(row, "REPORT_DATE_TYPE")))
                {
                    row["REPORT_DATE_TYPE"] = periodKey ?? string.Empty;
                }
            }
        }

        private static string? InferLegacyPeriodKey(DataRow row)
        {
            if (!string.IsNullOrWhiteSpace(ReadText(row, "REPORT_AS_OF_DATE_TEXT")) || !string.IsNullOrWhiteSpace(ReadText(row, "REPORT_DATE_TEXT")))
            {
                return "REPORT_AS_OF_DATE";
            }

            if (!string.IsNullOrWhiteSpace(ReadText(row, "REPORT_YEAR_TEXT")))
            {
                return "REPORT_YEAR";
            }

            if (!string.IsNullOrWhiteSpace(ReadText(row, "REPORT_MONTH_TEXT")))
            {
                return "REPORT_MONTH";
            }

            if (!string.IsNullOrWhiteSpace(ReadText(row, "REPORT_QUARTER_TEXT")))
            {
                return "REPORT_QUARTER";
            }

            if (!string.IsNullOrWhiteSpace(ReadText(row, "REPORT_DATE_RANGE_TEXT")))
            {
                return "REPORT_DATE_RANGE";
            }

            return null;
        }

        private static string? ReadText(DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value ? Convert.ToString(row[columnName]) : null;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static void EnsureReportDateColumns(DataTable table)
        {
            ReportDataTableHelper.EnsureColumn(table, "REPORT_PERIOD_KEY", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_PERIOD_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_YEAR_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_MONTH_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_QUARTER_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_DATE_RANGE_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_AS_OF_DATE_TEXT", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_DATE_TYPE", typeof(string));
            ReportDataTableHelper.EnsureColumn(table, "REPORT_DATE_TEXT", typeof(string));
        }

        private sealed record DateRange(DateTime FromDate, DateTime ToDate);

        private sealed record ReportPeriodText(
            string Key,
            string Text,
            string YearText,
            string MonthText,
            string QuarterText,
            string DateRangeText,
            string AsOfDateText);

        private enum QueryDateRole
        {
            None,
            From,
            To,
            Single
        }
    }
}

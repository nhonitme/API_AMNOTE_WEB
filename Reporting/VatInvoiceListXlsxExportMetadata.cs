using System.Data;

namespace API_AMNOTE_WEB.Reporting
{
    /// <summary>
    /// Snapshot of TAX_VAT_INVOICE_LIST print rows taken after VatInvoiceListPrintLayout.Apply(),
    /// keyed later by reportId/documentId for XLSX post-processing.
    /// </summary>
    internal sealed class VatInvoiceListXlsxExportMetadata
    {
        public VatInvoiceListXlsxExportMetadata(
            string reportCode,
            string reportTitle,
            IReadOnlyList<string> visibleColumnFields,
            IReadOnlyList<VatInvoiceListXlsxBodyRow> bodyRows,
            string? titleFontFamily = null,
            float? titleFontSize = null,
            bool? titleFontBold = null,
            bool? titleFontItalic = null,
            string? sectionFontFamily = null,
            float? sectionFontSize = null,
            bool? sectionFontBold = null,
            bool? sectionFontItalic = null)
        {
            ReportCode = reportCode;
            ReportTitle = reportTitle;
            VisibleColumnFields = visibleColumnFields;
            BodyRows = bodyRows;
            TitleFontFamily = NormalizeFontFamily(titleFontFamily);
            TitleFontSize = NormalizeFontSize(titleFontSize);
            TitleFontBold = titleFontBold;
            TitleFontItalic = titleFontItalic;
            SectionFontFamily = NormalizeFontFamily(sectionFontFamily);
            SectionFontSize = NormalizeFontSize(sectionFontSize);
            SectionFontBold = sectionFontBold;
            SectionFontItalic = sectionFontItalic;
        }

        public string ReportCode { get; }

        public string ReportTitle { get; }

        public IReadOnlyList<string> VisibleColumnFields { get; }

        public IReadOnlyList<VatInvoiceListXlsxBodyRow> BodyRows { get; }

        /// <summary>Resolved FONT_FAMILY used by PDF TITLE_MAIN (layout font family).</summary>
        public string? TitleFontFamily { get; }

        /// <summary>Resolved FONT_SIZE used by PDF TITLE_MAIN via CreateElementFont().</summary>
        public float? TitleFontSize { get; }

        public bool? TitleFontBold { get; }

        public bool? TitleFontItalic { get; }

        /// <summary>Resolved FONT_FAMILY used by PDF SECTION rows (layout detail font).</summary>
        public string? SectionFontFamily { get; }

        public float? SectionFontSize { get; }

        public bool? SectionFontBold { get; }

        public bool? SectionFontItalic { get; }

        public IReadOnlyList<VatInvoiceListXlsxBodyRow> SectionRows
            => BodyRows.Where(row => row.IsSection).ToList();

        public static VatInvoiceListXlsxExportMetadata? TryCreate(
            DataTable? table,
            string? reportCode,
            IReadOnlyList<string>? visibleColumnFields = null,
            string? titleFontFamily = null,
            float? titleFontSize = null,
            bool? titleFontBold = null,
            bool? titleFontItalic = null,
            string? sectionFontFamily = null,
            float? sectionFontSize = null,
            bool? sectionFontBold = null,
            bool? sectionFontItalic = null)
        {
            if (table == null || !IsVatInvoiceListReport(reportCode))
            {
                return null;
            }

            var normalizedReportCode = NormalizeText(reportCode);
            if (normalizedReportCode.Length == 0)
            {
                normalizedReportCode = "TAX_VAT_INVOICE_LIST";
            }

            var columns = visibleColumnFields?
                .Where(field => !string.IsNullOrWhiteSpace(field))
                .Select(field => field.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? ResolveDefaultVisibleColumns(table);

            var bodyRows = new List<VatInvoiceListXlsxBodyRow>(table.Rows.Count);
            var reportTitle = string.Empty;
            for (var index = 0; index < table.Rows.Count; index++)
            {
                var row = table.Rows[index];
                var rowType = NormalizeToken(ReadText(row, "__ROW_TYPE"));
                if (rowType is not ("SECTION" or "DETAIL"))
                {
                    continue;
                }

                if (reportTitle.Length == 0)
                {
                    reportTitle = ReadText(row, "REPORT_TITLE_TEXT");
                }

                var summaryLabel = ReadText(row, "SUMMARY_LABEL");
                var infoType = ReadText(row, "VAT_INFO_TYPE");
                var values = CaptureDetailValues(row, columns);
                bodyRows.Add(new VatInvoiceListXlsxBodyRow(
                    index,
                    rowType,
                    summaryLabel,
                    infoType,
                    values));
            }

            if (reportTitle.Length == 0)
            {
                reportTitle = table.Rows.Cast<DataRow>()
                    .Select(row => ReadText(row, "REPORT_TITLE_TEXT"))
                    .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                    ?? string.Empty;
            }

            return new VatInvoiceListXlsxExportMetadata(
                normalizedReportCode,
                reportTitle,
                columns,
                bodyRows,
                titleFontFamily,
                titleFontSize,
                titleFontBold,
                titleFontItalic,
                sectionFontFamily,
                sectionFontSize,
                sectionFontBold,
                sectionFontItalic);
        }

        private static bool IsVatInvoiceListReport(string? reportCode)
            => string.Equals(NormalizeText(reportCode), "TAX_VAT_INVOICE_LIST", StringComparison.OrdinalIgnoreCase);

        private static IReadOnlyList<string> ResolveDefaultVisibleColumns(DataTable table)
        {
            string[] preferred =
            {
                "STT",
                "VAT_SERIAL_NO",
                "VAT_CHIT_NO",
                "VAT_YMD",
                "VAT_COMPANY_ISSUE",
                "VAT_COMPANY_TAXCD",
                "VAT_PRODUCT_NM",
                "VAT_TAXABLE_AMOUNT",
                "VAT_AMOUNT",
                "NOTE",
            };

            var resolved = new List<string>();
            foreach (var field in preferred)
            {
                if (ResolveColumn(table, field) != null)
                {
                    resolved.Add(field);
                }
            }

            return resolved;
        }

        private static IReadOnlyDictionary<string, string> CaptureDetailValues(
            DataRow row,
            IReadOnlyList<string> columns)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                var text = ReadText(row, column);
                if (text.Length > 0)
                {
                    values[column] = text;
                }
            }

            return values;
        }

        private static string ReadText(DataRow row, string field)
        {
            var column = ResolveColumn(row.Table, field);
            if (column == null)
            {
                return string.Empty;
            }

            return NormalizeText(row[column]?.ToString());
        }

        private static string? ResolveColumn(DataTable table, string field)
        {
            foreach (DataColumn column in table.Columns)
            {
                if (string.Equals(column.ColumnName, field, StringComparison.OrdinalIgnoreCase))
                {
                    return column.ColumnName;
                }
            }

            return null;
        }

        private static string? NormalizeFontFamily(string? value)
        {
            var normalized = NormalizeText(value);
            return normalized.Length == 0 ? null : normalized;
        }

        private static float? NormalizeFontSize(float? value)
            => value is > 0f ? value : null;

        private static string NormalizeText(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string NormalizeToken(string? value)
            => NormalizeText(value).ToUpperInvariant();
    }

    internal sealed class VatInvoiceListXlsxBodyRow
    {
        public VatInvoiceListXlsxBodyRow(
            int sourceIndex,
            string rowType,
            string summaryLabel,
            string vatInfoType,
            IReadOnlyDictionary<string, string> cellValues)
        {
            SourceIndex = sourceIndex;
            RowType = rowType;
            SummaryLabel = summaryLabel ?? string.Empty;
            VatInfoType = vatInfoType ?? string.Empty;
            CellValues = cellValues ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public int SourceIndex { get; }

        public string RowType { get; }

        public string SummaryLabel { get; }

        public string VatInfoType { get; }

        public IReadOnlyDictionary<string, string> CellValues { get; }

        public bool IsSection
            => string.Equals(RowType, "SECTION", StringComparison.OrdinalIgnoreCase);

        public bool IsDetail
            => string.Equals(RowType, "DETAIL", StringComparison.OrdinalIgnoreCase);
    }
}

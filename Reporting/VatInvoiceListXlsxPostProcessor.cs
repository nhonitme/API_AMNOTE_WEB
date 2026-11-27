using System.Text;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace API_AMNOTE_WEB.Reporting
{
    /// <summary>
    /// Web Document Viewer exports the same cached bricks for PDF and XLSX, so SECTION merge /
    /// TITLE_MAIN cannot be fixed in BeforePrint alone. Post-process the XLSX bytes instead,
    /// using DataTable metadata captured before render (not Excel cell heuristics as source of truth).
    /// </summary>
    internal static class VatInvoiceListXlsxPostProcessor
    {
        private static readonly Regex SectionLabelRegex = new(
            @"^\s*\d+[a-zA-Z]?\.\s+\S+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly string[] FormNoMarkers =
        {
            "Mẫu số",
            "Mau so",
            "Form No",
            "Form No:",
            "様式",
            "표번호",
        };

        private static readonly string[] FooterMarkers =
        {
            "Tổng doanh thu",
            "Tong doanh thu",
            "Tổng thuế GTGT",
            "Tong thue GTGT",
            "Tổng giá trị",
            "Tong gia tri",
            "Total revenue",
            "Total VAT",
            "Total value",
        };

        public static byte[] Apply(byte[] xlsxBytes, VatInvoiceListXlsxExportMetadata? metadata)
        {
            if (xlsxBytes == null || xlsxBytes.Length == 0)
            {
                return xlsxBytes ?? Array.Empty<byte>();
            }

            if (metadata == null
                || !string.Equals(metadata.ReportCode, "TAX_VAT_INVOICE_LIST", StringComparison.OrdinalIgnoreCase))
            {
                return xlsxBytes;
            }

            ExcelPackage.License.SetNonCommercialPersonal("AMNote System");
            using var stream = new MemoryStream();
            stream.Write(xlsxBytes, 0, xlsxBytes.Length);
            stream.Position = 0;

            using var package = new ExcelPackage(stream);
            foreach (var sheet in package.Workbook.Worksheets)
            {
                if (sheet.Dimension == null)
                {
                    continue;
                }

                // Fix SECTION rows before EnsureTitle — title insert would shift row indexes.
                if (!TryMergeSectionRowsFromMetadata(sheet, metadata))
                {
                    RebuildBodyFromMetadata(sheet, metadata);
                }

                EnsureTitle(sheet, metadata.ReportTitle);
            }

            return package.GetAsByteArray();
        }

        /// <summary>Legacy overload kept for call-site compatibility during migration.</summary>
        public static byte[] Apply(byte[] xlsxBytes, string? reportTitle)
        {
            var metadata = new VatInvoiceListXlsxExportMetadata(
                "TAX_VAT_INVOICE_LIST",
                reportTitle ?? string.Empty,
                Array.Empty<string>(),
                Array.Empty<VatInvoiceListXlsxBodyRow>());
            return Apply(xlsxBytes, metadata);
        }

        private static void EnsureTitle(ExcelWorksheet sheet, string? reportTitle)
        {
            var title = Normalize(reportTitle);
            if (title.Length == 0)
            {
                return;
            }

            var dim = sheet.Dimension;
            var startCol = dim.Start.Column;
            var endCol = dim.End.Column;
            var startRow = dim.Start.Row;
            var endRow = Math.Min(dim.End.Row, startRow + 24);

            var existingTitleRow = FindExistingTitleRow(sheet, startRow, endRow, startCol, endCol, title);
            if (existingTitleRow > 0)
            {
                var mergeEnd = ResolveTitleMergeEndCol(sheet, existingTitleRow, startCol, endCol);
                WriteTitleCell(sheet, existingTitleRow, startCol, mergeEnd, title);
                return;
            }

            var formRow = FindMarkerRow(sheet, startRow, endRow, startCol, endCol, FormNoMarkers);
            if (formRow <= 0)
            {
                sheet.InsertRow(startRow, 1);
                WriteTitleCell(
                    sheet,
                    startRow,
                    startCol,
                    ResolveTitleMergeEndCol(sheet, startRow + 1, startCol, endCol),
                    title);
                return;
            }

            var titleRow = formRow + 1;
            for (var row = formRow + 1; row <= Math.Min(formRow + 4, endRow + 1); row++)
            {
                if (IsRowEffectivelyEmpty(sheet, row, startCol, endCol)
                    || RowLooksLikePartialTitle(sheet, row, startCol, endCol, title))
                {
                    titleRow = row;
                    break;
                }
            }

            if (!IsRowEffectivelyEmpty(sheet, titleRow, startCol, endCol)
                && !RowLooksLikePartialTitle(sheet, titleRow, startCol, endCol, title)
                && !RowContainsNormalized(sheet, titleRow, startCol, endCol, title))
            {
                // Avoid overwriting period / company / amount-unit lines.
                sheet.InsertRow(formRow + 1, 1);
                titleRow = formRow + 1;
            }

            var resolvedMergeEnd = ResolveTitleMergeEndCol(sheet, titleRow, startCol, endCol);
            WriteTitleCell(sheet, titleRow, startCol, resolvedMergeEnd, title);
        }

        private static void WriteTitleCell(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int endCol,
            string title)
        {
            UnmergeRows(sheet, row, row);

            var mergeEnd = Math.Max(startCol, endCol);
            if (mergeEnd > startCol)
            {
                sheet.Cells[row, startCol, row, mergeEnd].Merge = true;
            }

            var cell = sheet.Cells[row, startCol];
            cell.Value = title;
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = Math.Max(cell.Style.Font.Size, 12);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.WrapText = true;
            sheet.Row(row).Height = Math.Max(sheet.Row(row).Height, EstimateRowHeight(title, mergeEnd - startCol + 1));
        }

        private static bool TryMergeSectionRowsFromMetadata(
            ExcelWorksheet sheet,
            VatInvoiceListXlsxExportMetadata metadata)
        {
            if (metadata.BodyRows.Count == 0)
            {
                return true;
            }

            var dim = sheet.Dimension;
            var startCol = dim.Start.Column;
            var endCol = dim.End.Column;
            var headerRow = FindHeaderRow(sheet, dim.Start.Row, dim.End.Row, startCol, endCol);
            if (headerRow <= 0)
            {
                return false;
            }

            var tableEndCol = ResolveTableEndColumn(sheet, headerRow, startCol, endCol);
            if (tableEndCol <= startCol)
            {
                return false;
            }

            var headerSignature = CaptureRowSignature(sheet, headerRow, startCol, tableEndCol);
            var mappedRows = TryMapBodyRows(
                sheet,
                headerRow,
                startCol,
                tableEndCol,
                headerSignature,
                metadata.BodyRows);
            if (mappedRows == null)
            {
                return false;
            }

            foreach (var (excelRow, bodyRow) in mappedRows)
            {
                if (!bodyRow.IsSection)
                {
                    continue;
                }

                ApplySectionFormatting(sheet, excelRow, startCol, tableEndCol, bodyRow.SummaryLabel);
            }

            return true;
        }

        private static List<(int ExcelRow, VatInvoiceListXlsxBodyRow BodyRow)>? TryMapBodyRows(
            ExcelWorksheet sheet,
            int headerRow,
            int startCol,
            int tableEndCol,
            IReadOnlyList<string> headerSignature,
            IReadOnlyList<VatInvoiceListXlsxBodyRow> bodyRows)
        {
            var candidateRows = new List<int>();
            for (var row = headerRow + 1; row <= sheet.Dimension.End.Row; row++)
            {
                if (IsRepeatedHeaderRow(sheet, row, startCol, tableEndCol, headerSignature))
                {
                    continue;
                }

                if (LooksLikeFooterRow(sheet, row, startCol, tableEndCol))
                {
                    break;
                }

                candidateRows.Add(row);
            }

            if (candidateRows.Count == bodyRows.Count)
            {
                // Exact 1:1 after skipping repeated headers — preferred path.
                return candidateRows
                    .Select((excelRow, index) => (excelRow, bodyRows[index]))
                    .ToList();
            }

            // Extra trailing blanks are common; trim to metadata length when prefix maps cleanly.
            if (candidateRows.Count > bodyRows.Count)
            {
                var trimmed = candidateRows.Take(bodyRows.Count).ToList();
                if (CanAcceptPrefixMapping(sheet, trimmed, startCol, tableEndCol, bodyRows))
                {
                    return trimmed
                        .Select((excelRow, index) => (excelRow, bodyRows[index]))
                        .ToList();
                }
            }

            return TryMapByDetailAnchors(sheet, candidateRows, startCol, tableEndCol, bodyRows);
        }

        private static bool CanAcceptPrefixMapping(
            ExcelWorksheet sheet,
            IReadOnlyList<int> excelRows,
            int startCol,
            int tableEndCol,
            IReadOnlyList<VatInvoiceListXlsxBodyRow> bodyRows)
        {
            for (var index = 0; index < bodyRows.Count; index++)
            {
                var bodyRow = bodyRows[index];
                var excelRow = excelRows[index];
                if (bodyRow.IsDetail)
                {
                    if (!LooksLikeDetailRow(sheet, excelRow, startCol, tableEndCol)
                        || !DetailFingerprintMatches(sheet, excelRow, startCol, tableEndCol, bodyRow))
                    {
                        return false;
                    }
                }
                else if (bodyRow.IsSection && LooksLikeDetailRow(sheet, excelRow, startCol, tableEndCol))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<(int ExcelRow, VatInvoiceListXlsxBodyRow BodyRow)>? TryMapByDetailAnchors(
            ExcelWorksheet sheet,
            List<int> candidateRows,
            int startCol,
            int tableEndCol,
            IReadOnlyList<VatInvoiceListXlsxBodyRow> bodyRows)
        {
            var metaDetails = bodyRows.Where(row => row.IsDetail).ToList();
            if (metaDetails.Count == 0)
            {
                // Empty report: only SECTION rows. Accept candidate count == section count,
                // or rebuild when Excel dropped empty SECTION rows entirely.
                var metaSections = bodyRows.Where(row => row.IsSection).ToList();
                if (candidateRows.Count == metaSections.Count)
                {
                    return candidateRows
                        .Select((excelRow, index) => (excelRow, bodyRows[index]))
                        .ToList();
                }

                return null;
            }

            var excelDetailRows = candidateRows
                .Where(row => LooksLikeDetailRow(sheet, row, startCol, tableEndCol))
                .ToList();
            if (excelDetailRows.Count != metaDetails.Count)
            {
                return null;
            }

            // Verify DETAIL fingerprints when available.
            for (var index = 0; index < metaDetails.Count; index++)
            {
                if (!DetailFingerprintMatches(sheet, excelDetailRows[index], startCol, tableEndCol, metaDetails[index]))
                {
                    return null;
                }
            }

            var mapping = new List<(int ExcelRow, VatInvoiceListXlsxBodyRow BodyRow)>(bodyRows.Count);
            var detailCursor = 0;
            var candidateCursor = 0;

            foreach (var bodyRow in bodyRows)
            {
                if (bodyRow.IsDetail)
                {
                    var excelDetailRow = excelDetailRows[detailCursor++];
                    while (candidateCursor < candidateRows.Count
                           && candidateRows[candidateCursor] < excelDetailRow)
                    {
                        candidateCursor++;
                    }

                    if (candidateCursor < candidateRows.Count
                        && candidateRows[candidateCursor] == excelDetailRow)
                    {
                        candidateCursor++;
                    }

                    mapping.Add((excelDetailRow, bodyRow));
                    continue;
                }

                if (!bodyRow.IsSection)
                {
                    continue;
                }

                // Prefer a non-detail candidate between previous mapped row and next detail.
                var previousExcelRow = mapping.Count > 0
                    ? mapping[^1].ExcelRow
                    : (candidateRows.Count > 0 ? candidateRows[0] - 1 : 0);
                var nextDetailRow = detailCursor < excelDetailRows.Count
                    ? excelDetailRows[detailCursor]
                    : (candidateRows.Count > 0 ? candidateRows[^1] + 1 : previousExcelRow + 1);

                var sectionExcelRow = -1;
                for (var i = candidateCursor; i < candidateRows.Count; i++)
                {
                    var row = candidateRows[i];
                    if (row <= previousExcelRow)
                    {
                        continue;
                    }

                    if (row >= nextDetailRow)
                    {
                        break;
                    }

                    if (!LooksLikeDetailRow(sheet, row, startCol, tableEndCol))
                    {
                        sectionExcelRow = row;
                        candidateCursor = i + 1;
                        break;
                    }
                }

                if (sectionExcelRow < 0)
                {
                    // SECTION row missing in Excel — insert before next detail / after previous.
                    var insertAt = nextDetailRow > previousExcelRow + 1
                        ? previousExcelRow + 1
                        : nextDetailRow;
                    sheet.InsertRow(insertAt, 1);
                    ShiftRows(candidateRows, insertAt, 1);
                    ShiftRows(excelDetailRows, insertAt, 1);
                    sectionExcelRow = insertAt;
                    candidateCursor = FindIndexOrCount(candidateRows, sectionExcelRow) + 1;
                }

                mapping.Add((sectionExcelRow, bodyRow));
            }

            return mapping;
        }

        private static void RebuildBodyFromMetadata(
            ExcelWorksheet sheet,
            VatInvoiceListXlsxExportMetadata metadata)
        {
            var dim = sheet.Dimension;
            var startCol = dim.Start.Column;
            var endCol = dim.End.Column;
            var headerRow = FindHeaderRow(sheet, dim.Start.Row, dim.End.Row, startCol, endCol);
            if (headerRow <= 0)
            {
                // No table header — write a compact structured sheet after existing content.
                headerRow = dim.End.Row;
                startCol = 1;
                endCol = Math.Max(1, metadata.VisibleColumnFields.Count);
            }

            var tableEndCol = ResolveTableEndColumn(sheet, headerRow, startCol, endCol);
            if (tableEndCol < startCol)
            {
                tableEndCol = startCol + Math.Max(0, metadata.VisibleColumnFields.Count - 1);
            }

            var columnCount = Math.Max(1, tableEndCol - startCol + 1);
            var fields = metadata.VisibleColumnFields;
            if (fields.Count == 0)
            {
                fields = Enumerable.Range(0, columnCount).Select(index => $"COL_{index}").ToList();
            }

            // Remove existing body rows until footer; then rewrite from metadata.
            var firstBodyRow = headerRow + 1;
            var lastBodyRow = firstBodyRow - 1;
            for (var row = firstBodyRow; row <= sheet.Dimension.End.Row; row++)
            {
                if (LooksLikeFooterRow(sheet, row, startCol, tableEndCol))
                {
                    break;
                }

                lastBodyRow = row;
            }

            if (lastBodyRow >= firstBodyRow)
            {
                sheet.DeleteRow(firstBodyRow, lastBodyRow - firstBodyRow + 1);
            }

            if (metadata.BodyRows.Count > 0)
            {
                sheet.InsertRow(firstBodyRow, metadata.BodyRows.Count);
            }

            for (var index = 0; index < metadata.BodyRows.Count; index++)
            {
                var excelRow = firstBodyRow + index;
                var bodyRow = metadata.BodyRows[index];
                if (bodyRow.IsSection)
                {
                    ApplySectionFormatting(sheet, excelRow, startCol, tableEndCol, bodyRow.SummaryLabel);
                    continue;
                }

                for (var colOffset = 0; colOffset < columnCount; colOffset++)
                {
                    var field = colOffset < fields.Count ? fields[colOffset] : null;
                    var cell = sheet.Cells[excelRow, startCol + colOffset];
                    if (field != null && bodyRow.CellValues.TryGetValue(field, out var value))
                    {
                        cell.Value = value;
                    }

                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
                    cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                }
            }
        }

        private static void ApplySectionFormatting(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int tableEndCol,
            string summaryLabel)
        {
            var label = Normalize(summaryLabel);
            UnmergeRows(sheet, row, row);

            for (var col = startCol; col <= tableEndCol; col++)
            {
                var cell = sheet.Cells[row, col];
                cell.Value = null;
                cell.Style.Font.Bold = true;
                cell.Style.WrapText = true;
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                cell.Style.Border.Left.Style = col == startCol ? ExcelBorderStyle.Thin : ExcelBorderStyle.None;
                cell.Style.Border.Right.Style = col == tableEndCol ? ExcelBorderStyle.Thin : ExcelBorderStyle.None;
            }

            if (tableEndCol > startCol)
            {
                sheet.Cells[row, startCol, row, tableEndCol].Merge = true;
            }

            var labelCell = sheet.Cells[row, startCol];
            labelCell.Value = label;
            labelCell.Style.Font.Bold = true;
            labelCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            labelCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            labelCell.Style.WrapText = true;
            sheet.Row(row).Height = Math.Max(
                sheet.Row(row).Height,
                EstimateRowHeight(label, tableEndCol - startCol + 1));
        }

        private static bool DetailFingerprintMatches(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int endCol,
            VatInvoiceListXlsxBodyRow detailRow)
        {
            if (detailRow.CellValues.Count == 0)
            {
                return true;
            }

            var rowText = new StringBuilder();
            for (var col = startCol; col <= endCol; col++)
            {
                var text = GetCellText(sheet, row, col);
                if (text.Length > 0)
                {
                    if (rowText.Length > 0)
                    {
                        rowText.Append('|');
                    }

                    rowText.Append(text);
                }
            }

            var combined = rowText.ToString();
            if (combined.Length == 0)
            {
                return false;
            }

            // Require at least one distinctive non-numeric token from metadata to appear.
            foreach (var pair in detailRow.CellValues)
            {
                var value = Normalize(pair.Value);
                if (value.Length < 2 || LooksNumericOrDate(value))
                {
                    continue;
                }

                if (combined.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Fall back: any shared numeric/date token.
            foreach (var pair in detailRow.CellValues)
            {
                var value = Normalize(pair.Value);
                if (value.Length == 0)
                {
                    continue;
                }

                if (combined.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeDetailRow(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            if (IsRowEffectivelyEmpty(sheet, row, startCol, endCol))
            {
                return false;
            }

            var label = ResolveJoinedRowText(sheet, row, startCol, endCol);
            if (SectionLabelRegex.IsMatch(label))
            {
                return false;
            }

            var nonEmpty = 0;
            var numericish = 0;
            for (var col = startCol; col <= endCol; col++)
            {
                var text = GetCellText(sheet, row, col);
                if (text.Length == 0)
                {
                    continue;
                }

                nonEmpty++;
                if (LooksNumericOrDate(text))
                {
                    numericish++;
                }
            }

            // DETAIL rows usually carry several filled cells and/or amounts/dates.
            return nonEmpty >= 2 || numericish > 0;
        }

        private static bool LooksLikeFooterRow(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            var text = ResolveJoinedRowText(sheet, row, startCol, Math.Min(endCol, startCol + 8));
            if (text.Length == 0)
            {
                return false;
            }

            foreach (var marker in FooterMarkers)
            {
                if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRepeatedHeaderRow(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int endCol,
            IReadOnlyList<string> headerSignature)
        {
            if (headerSignature.Count == 0)
            {
                return false;
            }

            var matches = 0;
            var compared = 0;
            for (var offset = 0; offset < headerSignature.Count; offset++)
            {
                var expected = headerSignature[offset];
                if (expected.Length == 0)
                {
                    continue;
                }

                compared++;
                var actual = GetCellText(sheet, row, startCol + offset);
                if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    matches++;
                }
            }

            return compared > 0 && matches >= Math.Max(3, (compared + 1) / 2);
        }

        private static IReadOnlyList<string> CaptureRowSignature(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int endCol)
        {
            var values = new List<string>(Math.Max(0, endCol - startCol + 1));
            for (var col = startCol; col <= endCol; col++)
            {
                values.Add(GetCellText(sheet, row, col));
            }

            return values;
        }

        private static int FindHeaderRow(ExcelWorksheet sheet, int startRow, int endRow, int startCol, int endCol)
        {
            var bestRow = -1;
            var bestCount = 0;
            for (var row = startRow; row <= endRow; row++)
            {
                var filled = 0;
                var hasFill = false;
                for (var col = startCol; col <= endCol; col++)
                {
                    if (GetCellText(sheet, row, col).Length > 0)
                    {
                        filled++;
                    }

                    var fill = sheet.Cells[row, col].Style.Fill;
                    if (fill.PatternType != ExcelFillStyle.None)
                    {
                        hasFill = true;
                    }
                }

                if (filled < 4)
                {
                    continue;
                }

                var score = filled + (hasFill ? 20 : 0);
                if (score > bestCount)
                {
                    bestCount = score;
                    bestRow = row;
                }
            }

            return bestRow;
        }

        private static int ResolveTableEndColumn(ExcelWorksheet sheet, int headerRow, int startCol, int endCol)
        {
            var last = startCol;
            for (var col = startCol; col <= endCol; col++)
            {
                if (GetCellText(sheet, headerRow, col).Length > 0
                    || sheet.Cells[headerRow, col].Style.Fill.PatternType != ExcelFillStyle.None)
                {
                    last = col;
                }
            }

            return last;
        }

        private static int ResolveTitleMergeEndCol(ExcelWorksheet sheet, int nearRow, int startCol, int endCol)
        {
            var headerRow = FindHeaderRow(
                sheet,
                sheet.Dimension.Start.Row,
                Math.Min(sheet.Dimension.End.Row, nearRow + 12),
                startCol,
                endCol);
            if (headerRow > 0)
            {
                return ResolveTableEndColumn(sheet, headerRow, startCol, endCol);
            }

            var best = startCol;
            var from = Math.Max(sheet.Dimension.Start.Row, nearRow - 2);
            var to = Math.Min(sheet.Dimension.End.Row, nearRow + 8);
            for (var row = from; row <= to; row++)
            {
                for (var col = endCol; col >= startCol; col--)
                {
                    if (GetCellText(sheet, row, col).Length > 0)
                    {
                        best = Math.Max(best, col);
                        break;
                    }
                }
            }

            return Math.Max(best, startCol);
        }

        private static int FindExistingTitleRow(
            ExcelWorksheet sheet,
            int startRow,
            int endRow,
            int startCol,
            int endCol,
            string reportTitle)
        {
            for (var row = startRow; row <= endRow; row++)
            {
                if (RowContainsNormalized(sheet, row, startCol, endCol, reportTitle))
                {
                    return row;
                }
            }

            return -1;
        }

        private static int FindMarkerRow(
            ExcelWorksheet sheet,
            int startRow,
            int endRow,
            int startCol,
            int endCol,
            IReadOnlyList<string> markers)
        {
            for (var row = startRow; row <= endRow; row++)
            {
                for (var col = startCol; col <= Math.Min(endCol, startCol + 8); col++)
                {
                    var text = GetCellText(sheet, row, col);
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    foreach (var marker in markers)
                    {
                        if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                        {
                            return row;
                        }
                    }
                }
            }

            return -1;
        }

        private static bool RowLooksLikePartialTitle(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int endCol,
            string reportTitle)
        {
            var text = ResolveJoinedRowText(sheet, row, startCol, endCol);
            if (text.Length == 0)
            {
                return false;
            }

            if (text.Contains(reportTitle, StringComparison.OrdinalIgnoreCase)
                || reportTitle.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Truncated title fragment from overlapping XRLabel export.
            return text.Length >= 8
                && reportTitle.StartsWith(text, StringComparison.OrdinalIgnoreCase);
        }

        private static bool RowContainsNormalized(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int endCol,
            string expected)
        {
            var needle = Normalize(expected);
            if (needle.Length == 0)
            {
                return false;
            }

            for (var col = startCol; col <= endCol; col++)
            {
                var text = GetCellText(sheet, row, col);
                if (text.Length == 0)
                {
                    continue;
                }

                if (string.Equals(text, needle, StringComparison.OrdinalIgnoreCase)
                    || text.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || needle.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void UnmergeRows(ExcelWorksheet sheet, int startRow, int endRow)
        {
            // EPPlus can leave null entries in MergedCells after prior unmerge/export.
            var existingMerges = sheet.MergedCells
                .Where(address => !string.IsNullOrWhiteSpace(address))
                .Where(address =>
                {
                    try
                    {
                        var range = sheet.Cells[address];
                        return range.Start.Row <= endRow && range.End.Row >= startRow;
                    }
                    catch (ArgumentException)
                    {
                        return false;
                    }
                })
                .ToList();

            foreach (var address in existingMerges)
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    continue;
                }

                try
                {
                    sheet.Cells[address].Merge = false;
                }
                catch (ArgumentException)
                {
                    // Skip stale/invalid merge addresses left by DevExpress/EPPlus export.
                }
            }
        }

        private static void ShiftRows(IList<int> rows, int insertAt, int count)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index] >= insertAt)
                {
                    rows[index] += count;
                }
            }
        }

        private static int FindIndexOrCount(IReadOnlyList<int> rows, int value)
        {
            for (var index = 0; index < rows.Count; index++)
            {
                if (rows[index] == value)
                {
                    return index;
                }
            }

            return rows.Count;
        }

        private static double EstimateRowHeight(string text, int columnSpan)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 18;
            }

            var span = Math.Max(1, columnSpan);
            var approxCharsPerLine = Math.Max(12, span * 8);
            var lines = Math.Max(1, (int)Math.Ceiling(text.Length / (double)approxCharsPerLine));
            return Math.Min(72, 16 + (lines - 1) * 14);
        }

        private static bool IsRowEffectivelyEmpty(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            if (sheet.Dimension == null
                || row < sheet.Dimension.Start.Row
                || row > sheet.Dimension.End.Row + 1)
            {
                return true;
            }

            for (var col = startCol; col <= endCol; col++)
            {
                if (GetCellText(sheet, row, col).Length > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool LooksNumericOrDate(string text)
        {
            var compact = text.Replace(",", string.Empty).Replace(" ", string.Empty).Replace("%", string.Empty);
            if (decimal.TryParse(
                    compact,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out _))
            {
                return true;
            }

            return Regex.IsMatch(text, @"^\d{1,2}[/-]\d{1,2}[/-]\d{2,4}$");
        }

        private static string ResolveJoinedRowText(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            var joined = new StringBuilder();
            for (var col = startCol; col <= endCol; col++)
            {
                var text = GetCellText(sheet, row, col);
                if (text.Length == 0)
                {
                    continue;
                }

                if (joined.Length > 0
                    && !char.IsWhiteSpace(joined[^1])
                    && !char.IsWhiteSpace(text[0]))
                {
                    joined.Append(' ');
                }

                joined.Append(text);
            }

            return Normalize(joined.ToString());
        }

        private static string GetCellText(ExcelWorksheet sheet, int row, int col)
        {
            if (row < 1 || col < 1)
            {
                return string.Empty;
            }

            return Normalize(sheet.Cells[row, col].Text);
        }

        private static string Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

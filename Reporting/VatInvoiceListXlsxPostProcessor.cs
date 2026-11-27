using System.Text;
using System.Text.RegularExpressions;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace API_AMNOTE_WEB.Reporting
{
    /// <summary>
    /// Web Document Viewer exports the same cached bricks for PDF and XLSX, so SECTION merge /
    /// TITLE_MAIN cannot be fixed in BeforePrint alone. Post-process the XLSX bytes instead.
    /// </summary>
    internal static class VatInvoiceListXlsxPostProcessor
    {
        private static readonly Regex SectionLabelRegex = new(
            @"^\s*\d+[a-zA-Z]?\.\s+\S+",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly string[] TitleMarkers =
        {
            "BẢNG KÊ",
            "BANG KE",
            "LIST OF INVOICE",
            "LIST OF INVOICES",
        };

        private static readonly string[] FormNoMarkers =
        {
            "Mẫu số",
            "Mau so",
            "Form No",
            "Form No:",
        };

        public static byte[] Apply(byte[] xlsxBytes, string? reportTitle)
        {
            if (xlsxBytes == null || xlsxBytes.Length == 0)
            {
                return xlsxBytes ?? Array.Empty<byte>();
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

                EnsureTitle(sheet, reportTitle);
                MergeSectionRows(sheet);
            }

            return package.GetAsByteArray();
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
            var endRow = Math.Min(dim.End.Row, startRow + 20);

            if (FindTitleRow(sheet, startRow, endRow, startCol, endCol) > 0)
            {
                return;
            }

            var formRow = FindMarkerRow(sheet, startRow, endRow, startCol, endCol, FormNoMarkers);
            if (formRow <= 0)
            {
                // No form row — insert title at the top.
                sheet.InsertRow(startRow, 1);
                WriteTitleCell(sheet, startRow, startCol, ResolveTitleMergeEndCol(sheet, startRow + 1, startCol, endCol), title);
                return;
            }

            // Prefer an empty reserved row under form no (DevExpress often keeps height, drops text).
            var titleRow = formRow + 1;
            for (var row = formRow + 1; row <= Math.Min(formRow + 4, endRow + 1); row++)
            {
                if (IsRowEffectivelyEmpty(sheet, row, startCol, endCol))
                {
                    titleRow = row;
                    break;
                }
            }

            if (!IsRowEffectivelyEmpty(sheet, titleRow, startCol, endCol)
                && !RowLooksLikeTitleCandidate(sheet, titleRow, startCol, endCol))
            {
                sheet.InsertRow(formRow + 1, 1);
                titleRow = formRow + 1;
            }

            var mergeEnd = ResolveTitleMergeEndCol(sheet, titleRow, startCol, endCol);
            WriteTitleCell(sheet, titleRow, startCol, mergeEnd, title);
        }

        private static void WriteTitleCell(
            ExcelWorksheet sheet,
            int row,
            int startCol,
            int endCol,
            string title)
        {
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
            sheet.Row(row).Height = Math.Max(sheet.Row(row).Height, 22);
        }

        private static void MergeSectionRows(ExcelWorksheet sheet)
        {
            var dim = sheet.Dimension;
            var startCol = dim.Start.Column;
            var endCol = dim.End.Column;
            var headerRow = FindHeaderRow(sheet, dim.Start.Row, dim.End.Row, startCol, endCol);
            if (headerRow <= 0)
            {
                return;
            }

            var tableEndCol = ResolveTableEndColumn(sheet, headerRow, startCol, endCol);
            if (tableEndCol <= startCol)
            {
                return;
            }

            for (var row = headerRow + 1; row <= dim.End.Row; row++)
            {
                if (!IsSectionRow(sheet, row, startCol, tableEndCol))
                {
                    continue;
                }

                var label = ResolveSectionLabel(sheet, row, startCol, tableEndCol);
                if (label.Length == 0)
                {
                    continue;
                }

                // Clear fragments from overlay export, then merge full table width.
                for (var col = startCol; col <= tableEndCol; col++)
                {
                    var cell = sheet.Cells[row, col];
                    cell.Value = null;
                    cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                    cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    cell.Style.Border.Left.Style = col == startCol ? ExcelBorderStyle.Thin : ExcelBorderStyle.None;
                    cell.Style.Border.Right.Style = col == tableEndCol ? ExcelBorderStyle.Thin : ExcelBorderStyle.None;
                }

                if (tableEndCol > startCol)
                {
                    // Unmerge any partial merges first.
                    var existingMerges = sheet.MergedCells
                        .Where(address =>
                        {
                            var range = sheet.Cells[address];
                            return range.Start.Row <= row && range.End.Row >= row;
                        })
                        .ToList();
                    foreach (var address in existingMerges)
                    {
                        sheet.Cells[address].Merge = false;
                    }

                    sheet.Cells[row, startCol, row, tableEndCol].Merge = true;
                }

                var labelCell = sheet.Cells[row, startCol];
                labelCell.Value = label;
                labelCell.Style.Font.Bold = true;
                labelCell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                labelCell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                labelCell.Style.WrapText = true;
            }
        }

        private static bool IsSectionRow(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            if (IsRowEffectivelyEmpty(sheet, row, startCol, endCol))
            {
                return false;
            }

            var label = ResolveSectionLabel(sheet, row, startCol, endCol);
            if (SectionLabelRegex.IsMatch(label))
            {
                return true;
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

            // Overlay-fragmented section: a few short left cells, no amounts/dates.
            return numericish == 0
                && nonEmpty > 0
                && nonEmpty <= 10
                && (label.Contains("Hàng", StringComparison.OrdinalIgnoreCase)
                    || label.Contains("Hang", StringComparison.OrdinalIgnoreCase)
                    || label.Contains("Goods", StringComparison.OrdinalIgnoreCase)
                    || label.Contains("dịch vụ", StringComparison.OrdinalIgnoreCase)
                    || label.Contains("dich vu", StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveSectionLabel(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            var first = GetCellText(sheet, row, startCol);
            if (SectionLabelRegex.IsMatch(first) && first.Length >= 8)
            {
                return first;
            }

            // Prefer the longest cell (cell[0] SUMMARY_LABEL) over overlay fragments.
            string? longest = null;
            for (var col = startCol; col <= endCol; col++)
            {
                var text = GetCellText(sheet, row, col);
                if (text.Length == 0)
                {
                    continue;
                }

                if (longest == null || text.Length > longest.Length)
                {
                    longest = text;
                }
            }

            if (longest != null && (SectionLabelRegex.IsMatch(longest) || longest.Length >= first.Length))
            {
                return longest;
            }

            // Fallback: join left-to-right fragments from overlay export.
            var joined = new StringBuilder();
            for (var col = startCol; col <= endCol; col++)
            {
                var text = GetCellText(sheet, row, col);
                if (text.Length == 0)
                {
                    if (joined.Length > 0)
                    {
                        break;
                    }

                    continue;
                }

                if (joined.Length > 0
                    && !char.IsWhiteSpace(joined[^1])
                    && !char.IsWhiteSpace(text[0])
                    && !char.IsPunctuation(joined[^1]))
                {
                    joined.Append(' ');
                }

                joined.Append(text);
            }

            return Normalize(joined.ToString());
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

                if (filled < 5)
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
            // Prefer width of densest nearby row (header / form area).
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

            return Math.Max(best, Math.Min(endCol, startCol + 12));
        }

        private static int FindTitleRow(ExcelWorksheet sheet, int startRow, int endRow, int startCol, int endCol)
        {
            for (var row = startRow; row <= endRow; row++)
            {
                for (var col = startCol; col <= endCol; col++)
                {
                    var text = GetCellText(sheet, row, col);
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    foreach (var marker in TitleMarkers)
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

        private static bool RowLooksLikeTitleCandidate(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            var text = GetCellText(sheet, row, startCol);
            foreach (var marker in TitleMarkers)
            {
                if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRowEffectivelyEmpty(ExcelWorksheet sheet, int row, int startCol, int endCol)
        {
            if (row < sheet.Dimension.Start.Row || row > sheet.Dimension.End.Row + 1)
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
            if (decimal.TryParse(compact, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                return true;
            }

            return Regex.IsMatch(text, @"^\d{1,2}[/-]\d{1,2}[/-]\d{2,4}$");
        }

        private static string GetCellText(ExcelWorksheet sheet, int row, int col)
        {
            if (row < 1 || col < 1)
            {
                return string.Empty;
            }

            var value = sheet.Cells[row, col].Text;
            return Normalize(value);
        }

        private static string Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

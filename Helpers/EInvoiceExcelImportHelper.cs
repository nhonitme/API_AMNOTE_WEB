using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;
using OfficeOpenXml;
using System.Globalization;

namespace API_AMNOTE_WEB.Helpers;

public sealed class EInvoiceExcelImportParseResult
{
    public List<EInvoiceSaveRequest> Invoices { get; init; } = new();
    public int SkippedInvoices { get; init; }
    public List<ExcelImportResultRowDto> Errors { get; init; } = new();
}

public static class EInvoiceExcelImportHelper
{
    private static readonly string[] HeaderSheetNames = ["INFO", "HEADER", "INVOICE", "EINVOICE_INFO", "HOA_DON", "HOADON"];
    private static readonly string[] DetailSheetNames = ["DETAIL", "DETAILS", "EINVOICE_DETAIL", "CHI_TIET", "CHITIET", "DONG_HANG"];
    private static readonly string[] InvoiceKeyAliases = ["INVOICE_KEY", "GROUP_KEY", "MA_HOA_DON", "Ma hoa don"];

    public static async Task<EInvoiceExcelImportParseResult> ParseInvoicesAsync(
        Stream fileStream,
        string companyCd,
        IReadOnlyList<EInvoiceSellerInfo> sellers,
        CancellationToken cancellationToken = default)
    {
        ExcelPackage.License.SetNonCommercialPersonal("AMNote System");

        using var package = new ExcelPackage(fileStream);
        var workbook = package.Workbook;
        if (workbook.Worksheets.Count == 0)
        {
            return new EInvoiceExcelImportParseResult
            {
                Errors =
                [
                    new ExcelImportResultRowDto
                    {
                        RowNo = 0,
                        Status = "ERROR",
                        Message = "Excel file does not contain any worksheet."
                    }
                ]
            };
        }

        var headerSheet = FindWorksheet(workbook, HeaderSheetNames) ?? workbook.Worksheets[0];
        var detailSheet = FindWorksheet(workbook, DetailSheetNames);
        var headerRows = ReadSheetRows(headerSheet, cancellationToken);
        var detailRows = detailSheet != null && detailSheet != headerSheet
            ? ReadSheetRows(detailSheet, cancellationToken)
            : new List<Dictionary<string, object>>();

        var groups = detailSheet != null && detailSheet != headerSheet
            ? GroupHeaderDetailSheets(headerRows, detailRows)
            : GroupSingleSheetRows(headerRows);

        var invoices = new List<EInvoiceSaveRequest>();
        var errors = new List<ExcelImportResultRowDto>();
        var skipped = 0;

        for (var index = 0; index < groups.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = groups[index];
            var rowNo = ExcelImportHandlerHelper.GetExcelRowNo(group.HeaderRow, index);

            try
            {
                var invoice = BuildInvoice(group.HeaderRow, group.DetailRows, companyCd, sellers);
                var activeDetails = invoice.DETAILS.Where(x => x.ISDEL != 1).ToList();
                if (activeDetails.Count == 0 || activeDetails.All(x => string.IsNullOrWhiteSpace(x.THHDVU)))
                {
                    skipped++;
                    continue;
                }

                invoices.Add(invoice);
            }
            catch (Exception ex)
            {
                errors.Add(new ExcelImportResultRowDto
                {
                    RowNo = rowNo,
                    Status = "ERROR",
                    Message = ex.Message
                });
            }
        }

        if (invoices.Count == 0 && errors.Count == 0)
        {
            errors.Add(new ExcelImportResultRowDto
            {
                RowNo = 0,
                Status = "ERROR",
                Message = "Excel file does not contain valid e-invoice rows."
            });
        }

        return new EInvoiceExcelImportParseResult
        {
            Invoices = invoices,
            SkippedInvoices = skipped,
            Errors = errors
        };
    }

    private static ExcelWorksheet? FindWorksheet(ExcelWorkbook workbook, IEnumerable<string> candidates)
    {
        foreach (var worksheet in workbook.Worksheets)
        {
            var name = worksheet.Name?.Trim() ?? string.Empty;
            if (candidates.Any(candidate => name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return worksheet;
            }
        }

        return null;
    }

    private static List<Dictionary<string, object>> ReadSheetRows(ExcelWorksheet worksheet, CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object>>();
        if (worksheet.Dimension == null)
        {
            return rows;
        }

        var lastColumn = worksheet.Dimension.End.Column;
        var lastRow = worksheet.Dimension.End.Row;
        var headers = new string[lastColumn];

        for (var col = 1; col <= lastColumn; col++)
        {
            headers[col - 1] = NormalizeHeaderKey(worksheet.Cells[1, col].Text);
        }

        for (var row = 2; row <= lastRow; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var hasData = false;

            for (var col = 1; col <= lastColumn; col++)
            {
                var header = headers[col - 1];
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var cellValue = worksheet.Cells[row, col].Value;
                if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                {
                    hasData = true;
                }

                rowData[header] = cellValue ?? string.Empty;
            }

            if (!hasData)
            {
                break;
            }

            rowData[ExcelHelper.ExcelImportRowNoKey] = row;
            rows.Add(rowData);
        }

        return rows;
    }

    private static string NormalizeHeaderKey(string header)
    {
        var text = (header ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        foreach (var pair in HeaderAliasMap)
        {
            if (pair.Value.Any(alias => alias.Equals(text, StringComparison.OrdinalIgnoreCase)))
            {
                return pair.Key;
            }
        }

        return text.ToUpperInvariant();
    }

    private static List<(Dictionary<string, object> HeaderRow, List<Dictionary<string, object>> DetailRows)> GroupSingleSheetRows(
        IReadOnlyList<Dictionary<string, object>> rows)
    {
        var groups = new Dictionary<string, (Dictionary<string, object> HeaderRow, List<Dictionary<string, object>> DetailRows)>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var key = ReadInvoiceGroupKey(row, index);
            if (groups.TryGetValue(key, out var current))
            {
                current.DetailRows.Add(row);
                continue;
            }

            groups[key] = (row, new List<Dictionary<string, object>> { row });
        }

        return groups.Values.ToList();
    }

    private static List<(Dictionary<string, object> HeaderRow, List<Dictionary<string, object>> DetailRows)> GroupHeaderDetailSheets(
        IReadOnlyList<Dictionary<string, object>> headerRows,
        IReadOnlyList<Dictionary<string, object>> detailRows)
    {
        var detailGroups = new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < detailRows.Count; index++)
        {
            var key = ReadInvoiceGroupKey(detailRows[index], index);
            if (!detailGroups.TryGetValue(key, out var list))
            {
                list = new List<Dictionary<string, object>>();
                detailGroups[key] = list;
            }

            list.Add(detailRows[index]);
        }

        var linkedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = new List<(Dictionary<string, object>, List<Dictionary<string, object>>)>();

        for (var index = 0; index < headerRows.Count; index++)
        {
            var row = headerRows[index];
            var key = ReadInvoiceGroupKey(row, index);
            linkedKeys.Add(key);
            groups.Add((row, detailGroups.TryGetValue(key, out var details) ? details : new List<Dictionary<string, object>>()));
        }

        foreach (var pair in detailGroups)
        {
            if (linkedKeys.Contains(pair.Key) || pair.Value.Count == 0)
            {
                continue;
            }

            groups.Add((pair.Value[0], pair.Value));
        }

        return groups;
    }

    private static string ReadInvoiceGroupKey(Dictionary<string, object> row, int rowIndex)
    {
        var explicitKey = ReadText(row, InvoiceKeyAliases);
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return explicitKey.ToUpperInvariant();
        }

        var identity = string.Join("|", new[]
        {
            ReadText(row, "MCCQT"),
            ReadText(row, "MTRACUU"),
            ReadText(row, "KHMSHDON"),
            ReadText(row, "KHHDON"),
            ReadText(row, "SHDON"),
            ReadDate(row, "NLAP")?.ToString("yyyy-MM-dd") ?? string.Empty,
            ReadText(row, "NMUA_MST")
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(identity) ? $"ROW:{rowIndex + 2}" : identity.ToUpperInvariant();
    }

    private static EInvoiceSaveRequest BuildInvoice(
        Dictionary<string, object> headerRow,
        IReadOnlyList<Dictionary<string, object>> detailRows,
        string companyCd,
        IReadOnlyList<EInvoiceSellerInfo> sellers)
    {
        var invoice = new EInvoiceSaveRequest
        {
            INVOICE_ID = 0,
            COMPANY_CD = companyCd,
            THDON = NullIfEmpty(ReadText(headerRow, "THDON")),
            KHMSHDON = NullIfEmpty(ReadText(headerRow, "KHMSHDON")),
            KHHDON = NullIfEmpty(ReadText(headerRow, "KHHDON")),
            SHDON = null,
            MHSO = NullIfEmpty(ReadText(headerRow, "MHSO")),
            NLAP = ReadDate(headerRow, "NLAP"),
            DVTTE = NullIfEmpty(ReadText(headerRow, "DVTTE")) ?? "VND",
            TGIA = ReadDecimal(headerRow, "TGIA", 1m),
            HTTTOAN = NullIfEmpty(ReadText(headerRow, "HTTTOAN")),
            MSTTCGP = NullIfEmpty(ReadText(headerRow, "MSTTCGP")) ?? EInvoiceMessageXmlBuilder.MessageCodePrefix,
            NMUA_TEN = NullIfEmpty(ReadText(headerRow, "NMUA_TEN")),
            NMUA_MST = NullIfEmpty(ReadText(headerRow, "NMUA_MST")),
            NMUA_MDVQHNSACH = NullIfEmpty(ReadText(headerRow, "NMUA_MDVQHNSACH")),
            NMUA_DCHI = NullIfEmpty(ReadText(headerRow, "NMUA_DCHI")),
            NMUA_MKHANG = NullIfEmpty(ReadText(headerRow, "NMUA_MKHANG")),
            NMUA_SDTHOAI = NullIfEmpty(ReadText(headerRow, "NMUA_SDTHOAI")),
            NMUA_CCCDAN = NullIfEmpty(ReadText(headerRow, "NMUA_CCCDAN")),
            NMUA_SHCHIEU = NullIfEmpty(ReadText(headerRow, "NMUA_SHCHIEU")),
            NMUA_DCTDTU = NullIfEmpty(ReadText(headerRow, "NMUA_DCTDTU")),
            NMUA_HVTNMHANG = NullIfEmpty(ReadText(headerRow, "NMUA_HVTNMHANG")),
            NMUA_STKNHANG = NullIfEmpty(ReadText(headerRow, "NMUA_STKNHANG")),
            NMUA_TNHANG = NullIfEmpty(ReadText(headerRow, "NMUA_TNHANG")),
            TGTTTBCHU = NullIfEmpty(ReadText(headerRow, "TGTTTBCHU")),
            MTRACUU = NullIfEmpty(ReadText(headerRow, "MTRACUU")),
            IS_SIGNED = 0,
            ISDEL = 0
        };

        if (!string.Equals(invoice.DVTTE, "VND", StringComparison.OrdinalIgnoreCase))
        {
            invoice.TGIA = ReadDecimal(headerRow, "TGIA", invoice.TGIA ?? 1m);
        }
        else
        {
            invoice.TGIA = 1m;
        }

        ApplySeller(invoice, headerRow, sellers);

        var details = detailRows
            .Where(IsDetailRecord)
            .Select((row, index) => BuildDetail(row, index + 1, companyCd, invoice.DVTTE, invoice.TGIA ?? 1m))
            .Where(detail => !string.IsNullOrWhiteSpace(detail.THHDVU))
            .ToList();

        invoice.DETAILS = details.Count > 0 ? details : new List<EInvoiceDetailDto>();
        return invoice;
    }

    private static void ApplySeller(EInvoiceSaveRequest invoice, Dictionary<string, object> row, IReadOnlyList<EInvoiceSellerInfo> sellers)
    {
        var xslId = ReadLong(row, "XSL_ID");
        var sellerId = ReadLong(row, "SELLER_ID");
        EInvoiceSellerInfo? seller = null;

        if (xslId > 0)
        {
            seller = sellers.FirstOrDefault(item => item.XSL_ID == xslId);
        }

        if (sellerId > 0)
        {
            seller ??= sellers.FirstOrDefault(item => item.SELLER_ID == sellerId);
        }

        if (seller == null)
        {
            var sellerCode = ReadText(row, "SELLER_CD");
            if (!string.IsNullOrWhiteSpace(sellerCode))
            {
                seller = sellers.FirstOrDefault(item => string.Equals(item.SELLER_CD, sellerCode, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (seller == null && (!string.IsNullOrWhiteSpace(invoice.KHHDON) || !string.IsNullOrWhiteSpace(invoice.KHMSHDON)))
        {
            seller = sellers.FirstOrDefault(item =>
                (string.IsNullOrWhiteSpace(invoice.KHHDON) || string.Equals(item.KHHDON, invoice.KHHDON, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(invoice.KHMSHDON) || string.Equals(item.KHMSHDON, invoice.KHMSHDON, StringComparison.OrdinalIgnoreCase)));
        }

        seller ??= sellers.FirstOrDefault(item => item.XSL_IS_DEFAULT == 1) ?? sellers.FirstOrDefault();
        if (seller == null)
        {
            throw new InvalidOperationException("E-invoice seller is not configured");
        }

        invoice.SELLER_ID = seller.SELLER_ID;
        invoice.XSL_ID = seller.XSL_ID > 0 ? seller.XSL_ID : invoice.XSL_ID;
        invoice.THDON = string.IsNullOrWhiteSpace(invoice.THDON) ? seller.THDON : invoice.THDON;
        invoice.KHMSHDON = string.IsNullOrWhiteSpace(invoice.KHMSHDON) ? seller.KHMSHDON : invoice.KHMSHDON;
        invoice.KHHDON = string.IsNullOrWhiteSpace(invoice.KHHDON) ? seller.KHHDON : invoice.KHHDON;
    }

    private static EInvoiceDetailDto BuildDetail(
        Dictionary<string, object> row,
        int stt,
        string companyCd,
        string currencyCode,
        decimal rate)
    {
        var detail = new EInvoiceDetailDto
        {
            DETAIL_ID = 0,
            INVOICE_ID = 0,
            COMPANY_CD = companyCd,
            STT = stt,
            TCHAT = ReadInt(row, "TCHAT", 1),
            MHHDVU = NullIfEmpty(ReadText(row, "MHHDVU")),
            THHDVU = NullIfEmpty(ReadText(row, "THHDVU")) ?? string.Empty,
            DVTINH = NullIfEmpty(ReadText(row, "DVTINH")),
            SLUONG = ReadDecimal(row, "SLUONG", 0m),
            DGIA = ReadDecimal(row, "DGIA", 0m),
            TLCKHAU = ReadDecimal(row, "TLCKHAU", 0m),
            STCKHAU = ReadDecimal(row, "STCKHAU", 0m),
            THTIEN = ReadDecimal(row, "THTIEN", 0m),
            TSUAT = NullIfEmpty(ReadText(row, "TSUAT")),
            ISDEL = 0
        };

        if (string.Equals(currencyCode, "VND", StringComparison.OrdinalIgnoreCase))
        {
            detail.DGIA_VND = detail.DGIA;
            detail.STCKHAU_VND = detail.STCKHAU;
            detail.THTIEN_VND = detail.THTIEN;
        }
        else
        {
            detail.DGIA_VND = ReadDecimal(row, "DGIA_VND", (detail.DGIA ?? 0m) * rate);
            detail.STCKHAU_VND = ReadDecimal(row, "STCKHAU_VND", (detail.STCKHAU ?? 0m) * rate);
            detail.THTIEN_VND = ReadDecimal(row, "THTIEN_VND", (detail.THTIEN ?? 0m) * rate);
        }

        return detail;
    }

    private static bool IsDetailRecord(Dictionary<string, object> row)
    {
        return HasValue(row, "THHDVU")
            || HasValue(row, "MHHDVU")
            || HasValue(row, "SLUONG")
            || HasValue(row, "DGIA");
    }

    private static bool HasValue(Dictionary<string, object> row, string key)
    {
        return !string.IsNullOrWhiteSpace(ReadText(row, key));
    }

    private static string ReadText(Dictionary<string, object> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && value != null)
            {
                var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
                if (text.Length > 0)
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static long ReadLong(Dictionary<string, object> row, params string[] keys)
    {
        var text = ReadText(row, keys);
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static int ReadInt(Dictionary<string, object> row, string key, int fallback)
    {
        var text = ReadText(row, key);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static decimal ReadDecimal(Dictionary<string, object> row, string key, decimal fallback)
    {
        var text = ReadText(row, key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var compact = text.Replace(" ", string.Empty, StringComparison.Ordinal);
        var commaIndex = compact.LastIndexOf(',');
        var dotIndex = compact.LastIndexOf('.');
        if (commaIndex >= 0 && dotIndex >= 0)
        {
            compact = commaIndex > dotIndex
                ? compact.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal)
                : compact.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (commaIndex >= 0)
        {
            compact = compact.Replace(",", ".", StringComparison.Ordinal);
        }

        return decimal.TryParse(compact, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    private static DateTime? ReadDate(Dictionary<string, object> row, string key)
    {
        if (row.TryGetValue(key, out var value) && value is DateTime dateTime)
        {
            return dateTime.Date;
        }

        var text = ReadText(row, key);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.Date;
        }

        var isoMatch = System.Text.RegularExpressions.Regex.Match(text, @"^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})");
        if (isoMatch.Success)
        {
            return new DateTime(int.Parse(isoMatch.Groups[1].Value), int.Parse(isoMatch.Groups[2].Value), int.Parse(isoMatch.Groups[3].Value));
        }

        return null;
    }

    private static readonly Dictionary<string, string[]> HeaderAliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["THDON"] = ["THDON", "THDon", "Tên hóa đơn", "Ten hoa don"],
        ["KHMSHDON"] = ["KHMSHDON", "KHMSHDon", "Ký hiệu mẫu số", "Ky hieu mau so"],
        ["KHHDON"] = ["KHHDON", "KHHDon", "Ký hiệu hóa đơn", "Ky hieu hoa don"],
        ["SHDON"] = ["SHDON", "SHDon", "Số hóa đơn", "So hoa don"],
        ["NLAP"] = ["NLAP", "NLap", "Ngày lập", "Ngay lap", "Invoice date"],
        ["DVTTE"] = ["DVTTE", "DVTTe", "Tiền tệ", "Tien te", "Currency"],
        ["TGIA"] = ["TGIA", "TGia", "Tỷ giá", "Ty gia", "Rate"],
        ["HTTTOAN"] = ["HTTTOAN", "HTTToan", "Hình thức thanh toán", "Hinh thuc thanh toan"],
        ["SELLER_CD"] = ["SELLER_CD", "MA_NGUOI_BAN", "Mã người bán", "Ma nguoi ban"],
        ["SELLER_ID"] = ["SELLER_ID", "SELLERID"],
        ["XSL_ID"] = ["XSL_ID", "XSLID"],
        ["NMUA_TEN"] = ["NMUA_TEN", "NMua_Ten", "Tên người mua", "Ten nguoi mua", "Buyer name"],
        ["NMUA_MST"] = ["NMUA_MST", "MST người mua", "MST nguoi mua", "Buyer tax code"],
        ["NMUA_DCHI"] = ["NMUA_DCHI", "Địa chỉ người mua", "Dia chi nguoi mua", "Buyer address"],
        ["NMUA_MKHANG"] = ["NMUA_MKHANG", "Mã khách hàng", "Ma khach hang", "Customer code"],
        ["MHHDVU"] = ["MHHDVU", "MHHDVu", "Mã hàng", "Ma hang", "Item code"],
        ["THHDVU"] = ["THHDVU", "THHDVu", "Tên hàng", "Ten hang", "Item name"],
        ["DVTINH"] = ["DVTINH", "DVTinh", "ĐVT", "DVT", "Unit"],
        ["SLUONG"] = ["SLUONG", "SLuong", "Số lượng", "So luong"],
        ["DGIA"] = ["DGIA", "DGia", "Đơn giá", "Don gia"],
        ["TLCKHAU"] = ["TLCKHAU", "TLCKhau"],
        ["STCKHAU"] = ["STCKHAU", "STCKhau"],
        ["THTIEN"] = ["THTIEN", "THTien", "Thành tiền", "Thanh tien"],
        ["TSUAT"] = ["TSUAT", "TSuat", "Thuế suất", "Thue suat"],
        ["TCHAT"] = ["TCHAT", "TChat", "Tính chất", "Tinh chat"],
        ["MCCQT"] = ["MCCQT", "Mã CQT", "Ma CQT"],
        ["MTRACUU"] = ["MTRACUU", "MaTraCuu", "Mã tra cứu", "Ma tra cuu", "Mã tra cứu hóa đơn", "Ma tra cuu hoa don"]
    };
}

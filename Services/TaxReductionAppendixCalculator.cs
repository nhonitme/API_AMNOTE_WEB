using System.Data;
using System.Globalization;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services;

public sealed record TaxReductionInvoice(int Type, string InvoiceId, string Json,
    string Currency = "", decimal ExchangeRate = 0, string Status = "");

/// <summary>Desktop-compatible line calculations; amounts are VND, rounded away from zero.</summary>
public static class TaxReductionAppendixCalculator
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public static decimal Round(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    public static DataTable Calculate(IEnumerable<TaxReductionInvoice> invoices)
    {
        var table = new DataTable("TAX_VAT_REDUCTION_APPENDIX");
        foreach (var (name, type, caption) in new (string, Type, string)[] {
            ("__ROW_KEY", typeof(string), ""), ("__DRILL_TYPE", typeof(string), ""),
            ("TYPE", typeof(int), "Nhóm"), ("MHDON", typeof(string), "Mã hóa đơn"),
            ("ITEM_KIND", typeof(string), "Nhóm hóa đơn"),
            ("PRODUCT_NAME", typeof(string), "Tên hàng hóa, dịch vụ"),
            ("BILL_AMOUNT", typeof(decimal), "Thành tiền"), ("VAT", typeof(string), "Thuế VAT"),
            ("VAT_AMOUNT", typeof(decimal), "Tiền thuế GTGT"),
            ("BILL_NO", typeof(string), "Số hóa đơn"), ("BILL_YMD", typeof(DateTime), "Ngày hóa đơn"),
            ("STATUS_TEXT", typeof(string), "Trạng thái"),
            ("CURRENCY_TYPE", typeof(string), "Loại tiền"),
            ("REDUCTION_AMOUNT", typeof(decimal), "Thuế GTGT được giảm") })
            table.Columns.Add(new DataColumn(name, type) { Caption = caption });

        foreach (var invoice in invoices)
        {
            if (string.IsNullOrWhiteSpace(invoice.Json))
                throw new InvalidOperationException($"Hóa đơn {invoice.InvoiceId} chưa có JSON chi tiết.");
            try
            {
                using var document = JsonDocument.Parse(invoice.Json);
                var root = document.RootElement;
                if (!root.TryGetProperty("hdhhdvu", out var lines) || lines.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException($"Hóa đơn {invoice.InvoiceId} thiếu danh sách hàng hóa.");
                var eligible = lines.EnumerateArray().Select((line, index) => (line, index))
                    .Where(x => Text(x.line, "tchat") != "4" && Rate(x.line) == 0.08m).ToList();
                if (eligible.Count == 0) continue;
                var currency = Text(root, "dvtte");
                if (currency.Length == 0) currency = invoice.Currency.Trim();
                if (currency.Length == 0)
                    throw new InvalidOperationException($"Hóa đơn {invoice.InvoiceId} thiếu loại tiền.");
                var rate = Number(root, "tgia");
                if (rate <= 0) rate = invoice.ExchangeRate;
                if (currency.Equals("VND", StringComparison.OrdinalIgnoreCase)) rate = 1;
                if (rate <= 0)
                    throw new InvalidOperationException($"Hóa đơn {invoice.InvoiceId} thiếu tỷ giá hợp lệ.");
                var date = DateTimeOffset.Parse(Text(root, "tdlap"), Invariant,
                    DateTimeStyles.AssumeUniversal).ToOffset(TimeSpan.FromHours(7)).Date;
                var status = string.IsNullOrWhiteSpace(invoice.Status) ? Text(root, "tthai") : invoice.Status;
                foreach (var (line, index) in eligible)
                {
                    var amount = Number(line, "thtien");
                    // Preserve the two legacy rounding sequences, including negative adjustment lines.
                    var localAmount = invoice.Type == 1 ? Round(amount * rate) : amount * rate;
                    var vat = invoice.Type == 1 ? Round(localAmount * 0.08m) : Round(Round(amount * 0.08m) * rate);
                    table.Rows.Add($"{invoice.Type}:{invoice.InvoiceId}:{index}", "NONE", invoice.Type,
                        invoice.InvoiceId, invoice.Type == 1 ? "I. Hàng hóa, dịch vụ mua vào" : "II. Hàng hóa, dịch vụ bán ra",
                        Text(line, "ten"), localAmount, "8%", vat, Text(root, "shdon"), date,
                        StatusText(status), currency.ToUpperInvariant(), invoice.Type == 2 ? Round(localAmount * 0.02m) : 0m);
                }
            }
            catch (Exception ex) when (ex is JsonException or FormatException or OverflowException)
            {
                throw new InvalidOperationException($"Không đọc được dữ liệu hóa đơn {invoice.InvoiceId}.", ex);
            }
        }
        table.DefaultView.Sort = "TYPE ASC, BILL_YMD ASC, BILL_NO ASC, __ROW_KEY ASC";
        return table.DefaultView.ToTable();
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString().Trim() : "";
    private static decimal Number(JsonElement element, string name)
    {
        var text = Text(element, name);
        return text.Length == 0 ? 0 : decimal.Parse(text, NumberStyles.Float, Invariant);
    }
    private static decimal Rate(JsonElement line)
    {
        var text = Text(line, "tsuat");
        if (text.Length == 0) text = Text(line, "ltsuat");
        if (!decimal.TryParse(text.TrimEnd('%'), NumberStyles.Float, Invariant, out var rate)) return -1;
        return text.EndsWith('%') || rate > 1 ? rate / 100 : rate;
    }
    private static string StatusText(string status) => status switch {
        "1" => "Hóa đơn mới", "2" => "Hóa đơn thay thế", "3" => "Hóa đơn điều chỉnh",
        "4" => "Hóa đơn bị thay thế", "5" => "Hóa đơn bị điều chỉnh", "6" => "Hóa đơn hủy",
        "998" => "Chưa có JSON", _ => status };
}

using System.Data;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Services;

public sealed class TaxReductionAppendixService(DapperExecutor db)
{
    // Invoked by ConfiguredReportService. Company connection comes from the authenticated context.
    public async Task<DataTable> GetDataAsync(string fromYmd, string toYmd,
        string? status = null, string? invoiceKind = null, string? fcType = null,
        string? searchText = null, CancellationToken cancellationToken = default)
    {
        var from = Common.NormalizeNullableYmdText(fromYmd, nameof(fromYmd));
        var to = Common.NormalizeNullableYmdText(toYmd, nameof(toYmd));
        if (from == null || to == null) throw new ArgumentException("Vui lòng chọn từ ngày và đến ngày.");
        Common.ValidateYmdRange(from, to, nameof(fromYmd), nameof(toYmd));
        var statuses = Codes(status);
        if (statuses.Any(x => !new[] { "1", "2", "3", "4", "5", "6", "998" }.Contains(x)))
            throw new ArgumentException("Trạng thái hóa đơn không hợp lệ.");
        var kinds = Codes(invoiceKind);
        if (kinds.Any(x => !new[] { "1", "2", "3", "4", "5", "6", "7" }.Contains(x)))
            throw new ArgumentException("Loại hóa đơn không hợp lệ.");
        var currencies = Codes(fcType);
        var invoices = new List<TaxReductionInvoice>();
        foreach (var (type, header, json) in new[] {
            (1, "buy_list_einvoice", "buy_list_json"), (2, "sell_list_einvoice", "sell_list_json") })
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Table names are constants. All user-controlled filters are bound parameters.
            var sql = $"""
                SELECT F.mhdon AS InvoiceId, J.json AS Json, F.dvtte AS Currency,
                       F.tgia AS ExchangeRate, F.tthai AS Status
                FROM {header} F
                LEFT JOIN {json} J ON J.mhdon = F.mhdon AND IFNULL(J.ISDEL,'0') <> '1'
                WHERE IFNULL(F.ISDEL,'0') <> '1'
                  AND F.tdlap BETWEEN @FromYmd AND @ToYmd
                  AND (@AllStatuses = 1 OR CAST(F.tthai AS CHAR) IN @Statuses
                       OR (@MissingJson = 1 AND (J.mhdon IS NULL OR IFNULL(J.json,'') = '')))
                  AND (@AllCurrencies = 1 OR F.dvtte IN @Currencies)
                  AND (@AllKinds = 1 OR (CAST(F.khmshdon AS CHAR) IN @OrdinaryKinds)
                       OR (@Internal = 1 AND F.khmshdon = '6' AND SUBSTRING(F.khhdon,4,1) = 'N')
                       OR (@Agent = 1 AND F.khmshdon = '6' AND SUBSTRING(F.khhdon,4,1) = 'B'))
                """;
            var rows = await db.QueryAsync<SourceInvoice>(Net_DB.Net_DB_Company, sql, new {
                FromYmd = from, ToYmd = to, AllStatuses = statuses.Length == 0,
                Statuses = statuses, MissingJson = statuses.Contains("998"),
                AllCurrencies = currencies.Length == 0, Currencies = currencies,
                AllKinds = kinds.Length == 0, OrdinaryKinds = kinds.Where(x => x != "6" && x != "7").ToArray(),
                Internal = kinds.Contains("6"), Agent = kinds.Contains("7") });
            invoices.AddRange(rows.Select(x => new TaxReductionInvoice(type, x.InvoiceId,
                x.Json ?? "", x.Currency ?? "", x.ExchangeRate ?? 0, x.Status ?? "")));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var table = TaxReductionAppendixCalculator.Calculate(invoices);
        var keyword = searchText?.Trim();
        if (!string.IsNullOrEmpty(keyword))
            foreach (var row in table.Rows.Cast<DataRow>().Where(r =>
                !new[] { "PRODUCT_NAME", "BILL_NO", "MHDON", "STATUS_TEXT" }.Any(field =>
                    Convert.ToString(r[field])!.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList())
                table.Rows.Remove(row);
        return table;
    }

    private static string[] Codes(string? value) => (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !x.Equals("ALL", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private sealed class SourceInvoice
    {
        public string InvoiceId { get; set; } = "";
        public string? Json { get; set; }
        public string? Currency { get; set; }
        public decimal? ExchangeRate { get; set; }
        public string? Status { get; set; }
    }
}

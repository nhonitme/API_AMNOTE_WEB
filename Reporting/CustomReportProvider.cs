using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using DevExpress.XtraReports.Services;
using DevExpress.XtraReports.UI;

namespace API_AMNOTE_WEB.Reporting
{
    public class CustomReportProvider : IReportProviderAsync
    {
        private readonly IConfiguredReportService _configuredReportService;
        private readonly IEInvoicePrintService _einvoicePrintService;

        public CustomReportProvider(
            IConfiguredReportService configuredReportService,
            IEInvoicePrintService einvoicePrintService)
        {
            _configuredReportService = configuredReportService ?? throw new ArgumentNullException(nameof(configuredReportService));
            _einvoicePrintService = einvoicePrintService ?? throw new ArgumentNullException(nameof(einvoicePrintService));
        }

        public async Task<XtraReport> GetReportAsync(string id, ReportProviderContext context)
        {
            var request = ParseReportRequest(id);
            var companyCd = Common.GetCompanyCode();
            var reportCode = ResolveReportCode(request.ReportName, request.Query);
            var menuCode = GetOptionalQueryValue(request.Query, "menuCode");

            if (reportCode.Equals("einvoice", StringComparison.OrdinalIgnoreCase))
            {
                var invoiceId = ParsePositiveLong(GetOptionalQueryValue(request.Query, "invoiceId"));
                if (invoiceId <= 0)
                {
                    throw new ArgumentException("invoiceId is required for e-invoice report.");
                }

                return await _einvoicePrintService.BuildReportAsync(companyCd, invoiceId, null, CancellationToken.None);
            }

            return await _configuredReportService.BuildReportAsync(
                companyCd,
                reportCode,
                menuCode,
                request.Query,
                CancellationToken.None);
        }

        private static ReportRequest ParseReportRequest(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Report id is required.");
            }

            var parts = id.Split('?', 2, StringSplitOptions.RemoveEmptyEntries);
            var reportName = parts[0].Trim();
            var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                var items = parts[1].Split('&', StringSplitOptions.RemoveEmptyEntries);

                foreach (var item in items)
                {
                    var kv = item.Split('=', 2);
                    if (kv.Length == 2 && !IsCompanyQueryKey(kv[0]))
                    {
                        query[kv[0]] = Uri.UnescapeDataString(kv[1]);
                    }
                }
            }

            return new ReportRequest(reportName, query);
        }

        private static string ResolveReportCode(string reportName, IReadOnlyDictionary<string, string> query)
        {
            var reportCode = GetOptionalQueryValue(query, "reportCode");
            if (!string.IsNullOrWhiteSpace(reportCode))
            {
                return reportCode;
            }

            if (!string.IsNullOrWhiteSpace(GetOptionalQueryValue(query, "menuCode")) &&
                reportName.Equals("configured", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return reportName.Equals("configured", StringComparison.OrdinalIgnoreCase)
                ? throw new ArgumentException("reportCode or menuCode is required.")
                : reportName;
        }

        private static string? GetOptionalQueryValue(IReadOnlyDictionary<string, string> query, string key)
        {
            if (query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            return null;
        }

        private static bool IsCompanyQueryKey(string key)
        {
            return Common.NormalizeToken(key) is "companycd" or "pcompanycd";
        }

        private static long ParsePositiveLong(string? value)
        {
            return long.TryParse(value, out var number) && number > 0 ? number : 0;
        }

        private sealed record ReportRequest(string ReportName, IReadOnlyDictionary<string, string> Query);
    }
}

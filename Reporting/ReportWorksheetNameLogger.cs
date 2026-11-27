using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.Web;
using DevExpress.XtraReports.Web.ClientControls;
using DevExpress.XtraReports.Web.WebDocumentViewer;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Reporting;

internal sealed class ReportWorksheetNameLogger : WebDocumentViewerOperationLogger
{
    private const int MaxSheetNameLength = 31;
    private static readonly Regex InvalidSheetNameCharacters = new(@"[\\/:*?\[\]\p{Cc}]");
    private readonly ConcurrentDictionary<string, AccountWorkbook> _reports = new();
    private readonly ConcurrentDictionary<string, AccountWorkbook> _documents = new();
    private readonly ConcurrentDictionary<string, VatInvoiceListXlsxExportMetadata> _vatInvoiceReports = new();
    private readonly ConcurrentDictionary<string, VatInvoiceListXlsxExportMetadata> _vatInvoiceDocuments = new();
    private readonly ConditionalWeakTable<PrintingSystemBase, object> _pageNameHandlers = new();

    public override void ReportOpening(
        string reportId, string reportUrl, string documentId, XtraReport report, CachedReportSourceWeb cachedReportSource)
    {
        var configuredReport = report as DynamicConfiguredReport ?? cachedReportSource?.Report as DynamicConfiguredReport;
        if (configuredReport?.ExportAccountWorkbook is { } exportAccountWorkbook)
        {
            var workbook = new AccountWorkbook(configuredReport.DisplayName, exportAccountWorkbook);
            _reports[reportId] = workbook;
            if (!string.IsNullOrEmpty(documentId))
            {
                _documents[documentId] = workbook;
            }
        }

        CaptureVatInvoiceMetadata(reportId, documentId, configuredReport);

        base.ReportOpening(reportId, reportUrl, documentId, report, cachedReportSource);
    }

    public override Action BuildStarting(string reportId, string reportUrl, XtraReport report, ReportBuildProperties buildProperties)
    {
        if (report is DynamicConfiguredReport { ExportAccountWorkbook: { } exportAccountWorkbook })
        {
            _reports[reportId] = new AccountWorkbook(report.DisplayName, exportAccountWorkbook);
        }

        if (report is DynamicConfiguredReport configuredReport)
        {
            CaptureVatInvoiceMetadata(reportId, documentId: null, configuredReport);
        }

        return base.BuildStarting(reportId, reportUrl, report, buildProperties);
    }

    public override void BuildStarted(
        string reportId, string reportUrl, string documentId, ReportBuildProperties buildProperties)
    {
        if (_reports.TryGetValue(reportId, out var workbook))
        {
            _documents[documentId] = workbook;
        }

        if (_vatInvoiceReports.TryGetValue(reportId, out var metadata))
        {
            _vatInvoiceDocuments[documentId] = metadata;
        }

        base.BuildStarted(reportId, reportUrl, documentId, buildProperties);
    }

    public override ExportedDocument ExportDocumentStarting(
        string documentId, string asyncExportOperationId, string format, ExportOptions options,
        PrintingSystemBase printingSystem, Func<ExportedDocument> doExportSynchronously)
    {
        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
            && _documents.TryGetValue(documentId, out var workbook))
        {
            return new ExportedDocument(
                workbook.Export(options.Xlsx),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                workbook.DisplayName + ".xlsx");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
            && _vatInvoiceDocuments.TryGetValue(documentId, out var vatMetadata))
        {
            var exported = doExportSynchronously();
            var processed = VatInvoiceListXlsxPostProcessor.Apply(exported.Bytes ?? Array.Empty<byte>(), vatMetadata);
            return new ExportedDocument(
                processed,
                string.IsNullOrWhiteSpace(exported.ContentType)
                    ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                    : exported.ContentType,
                string.IsNullOrWhiteSpace(exported.FileName)
                    ? "TAX_VAT_INVOICE_LIST.xlsx"
                    : exported.FileName);
        }

        return base.ExportDocumentStarting(
            documentId, asyncExportOperationId, format, options, printingSystem, doExportSynchronously);
    }

    public override void ExportDocument(string documentId, string format, ExportOptions options, PrintingSystemBase printingSystem)
    {
        if (!string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase)
            || options.Xlsx.ExportMode != XlsxExportMode.SingleFilePageByPage
            || _documents.ContainsKey(documentId))
        {
            return;
        }

        lock (_pageNameHandlers)
        {
            if (_pageNameHandlers.TryAdd(printingSystem, new object()))
            {
                printingSystem.XlSheetCreated += (_, args) => args.SheetName = GetPageName(args.Index);
            }
        }
    }

    public override void CachedDocumentReleased(string documentId)
    {
        _documents.TryRemove(documentId, out _);
        _vatInvoiceDocuments.TryRemove(documentId, out _);
        base.CachedDocumentReleased(documentId);
    }

    public override void CachedReportReleased(string reportId)
    {
        _reports.TryRemove(reportId, out _);
        _vatInvoiceReports.TryRemove(reportId, out _);
        base.CachedReportReleased(reportId);
    }

    internal static string[] BuildWorksheetNames(IReadOnlyList<string> accountCodes, string reportTitle)
    {
        var fallbackName = CleanSheetName(reportTitle);
        if (string.IsNullOrWhiteSpace(fallbackName))
        {
            fallbackName = "Report";
        }

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "History" };
        var nextSuffixes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var names = new string[accountCodes.Count];
        for (var index = 0; index < names.Length; index++)
        {
            var baseName = CleanSheetName(accountCodes[index]);
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = fallbackName;
            }

            var preferredName = baseName[..Math.Min(baseName.Length, MaxSheetNameLength)].TrimEnd('\'');
            var name = preferredName;
            var number = nextSuffixes.GetValueOrDefault(preferredName, 2);
            while (!usedNames.Add(name))
            {
                var suffix = "_" + number.ToString(CultureInfo.InvariantCulture);
                name = baseName[..Math.Min(baseName.Length, MaxSheetNameLength - suffix.Length)] + suffix;
                number++;
            }

            nextSuffixes[preferredName] = number;
            names[index] = name;
        }

        return names;
    }

    private void CaptureVatInvoiceMetadata(
        string reportId,
        string? documentId,
        DynamicConfiguredReport? configuredReport)
    {
        var metadata = configuredReport?.VatInvoiceListXlsxMetadata;
        if (metadata == null)
        {
            return;
        }

        _vatInvoiceReports[reportId] = metadata;
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            _vatInvoiceDocuments[documentId] = metadata;
        }
    }

    private static string CleanSheetName(string name) => InvalidSheetNameCharacters.Replace(name, "_").Trim(' ', '\'');

    private static string GetPageName(int index) => $"Trang {(index + 1).ToString(CultureInfo.InvariantCulture)}";

    private sealed record AccountWorkbook(string DisplayName, Func<XlsxExportOptions, byte[]> Export);
}

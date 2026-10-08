using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceXmlStorageService : IEInvoiceXmlStorageService
    {
        private readonly ILogger<EInvoiceXmlStorageService> _logger;

        public EInvoiceXmlStorageService(ILogger<EInvoiceXmlStorageService> logger)
        {
            _logger = logger;
        }

        public Task<string> UploadInvoiceXmlAsync(
            string companyCd,
            long invoiceId,
            string xml,
            DateTime? invoiceDate = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                throw new ArgumentException("COMPANY_CD is required", nameof(companyCd));
            }

            if (invoiceId <= 0)
            {
                throw new ArgumentException("INVOICE_ID is required", nameof(invoiceId));
            }

            if (string.IsNullOrWhiteSpace(xml))
            {
                throw new ArgumentException("XML is required", nameof(xml));
            }

            if (!EInvoiceFtpClient.IsConfigured())
            {
                throw new InvalidOperationException("FTP is not configured");
            }

            var remotePath = BuildInvoiceXmlPath(companyCd, invoiceId, invoiceDate, xml);

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                EInvoiceFtpClient.UploadText(remotePath, xml, Encoding.UTF8);
                _logger.LogInformation(
                    "Uploaded e-invoice XML to FTP. CompanyCd={CompanyCd}, InvoiceId={InvoiceId}, Path={Path}",
                    companyCd,
                    invoiceId,
                    remotePath);
                return remotePath;
            }, cancellationToken);
        }

        public Task<string> DownloadAsync(string xmlFtpPath, CancellationToken cancellationToken = default)
        {
            var normalizedPath = Common.NormalizeRequiredText(xmlFtpPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                throw new ArgumentException("XML_FTP_PATH is required", nameof(xmlFtpPath));
            }

            if (!EInvoiceFtpClient.IsConfigured())
            {
                throw new InvalidOperationException("FTP is not configured");
            }

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return EInvoiceFtpClient.DownloadText(normalizedPath, Encoding.UTF8);
            }, cancellationToken);
        }

        public async Task<string?> TryDownloadAsync(string xmlFtpPath, CancellationToken cancellationToken = default)
        {
            var normalizedPath = Common.NormalizeNullableText(xmlFtpPath);
            if (string.IsNullOrWhiteSpace(normalizedPath) || !EInvoiceFtpClient.IsConfigured())
            {
                return null;
            }

            try
            {
                return await DownloadAsync(normalizedPath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to download e-invoice XML from FTP path {Path}", normalizedPath);
                return null;
            }
        }

        public async Task<string?> ResolveSignedInvoiceXmlAsync(
            string companyCd,
            EInvoiceInfo invoice,
            CancellationToken cancellationToken = default)
        {
            if (invoice.IS_SIGNED != 1)
            {
                return null;
            }

            foreach (var candidatePath in BuildCandidatePaths(companyCd, invoice))
            {
                var xml = await TryDownloadAsync(candidatePath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(xml))
                {
                    if (!string.Equals(
                            Common.NormalizeNullableText(invoice.XML_FTP_PATH),
                            candidatePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Resolved signed e-invoice XML from fallback FTP path. CompanyCd={CompanyCd}, InvoiceId={InvoiceId}, Path={Path}",
                            companyCd,
                            invoice.INVOICE_ID,
                            candidatePath);
                    }

                    return xml;
                }
            }

            return null;
        }

        public IReadOnlyList<string> BuildCandidatePaths(string companyCd, EInvoiceInfo invoice)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddPath(string? path)
            {
                var normalized = Common.NormalizeNullableText(path);
                if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
                {
                    return;
                }

                paths.Add(normalized);
            }

            AddPath(invoice.XML_FTP_PATH);
            AddPath(BuildInvoiceXmlPath(companyCd, invoice.INVOICE_ID, invoice.NLAP));
            AddPath(BuildLegacyInvoiceXmlPath(companyCd, invoice.INVOICE_ID));

            return paths;
        }

        public bool IsRemotePath(string? path)
            => EInvoiceFtpClient.IsRemotePath(path);

        public static string BuildInvoiceXmlPath(
            string companyCd,
            long invoiceId,
            DateTime? invoiceDate = null,
            string? xml = null)
        {
            var safeCompanyCd = SanitizePathPart(companyCd, "COMPANY");
            var folderDate = ResolveFolderDate(invoiceDate, xml);
            var fileName = $"INV_{invoiceId}.xml";
            return $"{EInvoiceFtpClient.DefaultXmlRemoteDirectory}/{safeCompanyCd}/{folderDate:yyyy}/{folderDate:MM}/{folderDate:dd}/{fileName}";
        }

        public static string BuildLegacyInvoiceXmlPath(string companyCd, long invoiceId)
        {
            var safeCompanyCd = SanitizePathPart(companyCd, "COMPANY");
            return $"/DATA2/AttachFile/EinvoiceXml/{safeCompanyCd}/INV_{invoiceId}.xml";
        }

        private static DateTime ResolveFolderDate(DateTime? invoiceDate, string? xml)
        {
            if (invoiceDate.HasValue)
            {
                return invoiceDate.Value.Date;
            }

            var fromXml = TryParseInvoiceDateFromXml(xml);
            if (fromXml.HasValue)
            {
                return fromXml.Value.Date;
            }

            return DateTime.Today;
        }

        private static DateTime? TryParseInvoiceDateFromXml(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            try
            {
                var document = XDocument.Parse(xml);
                var nlapValue = document
                    .Descendants()
                    .FirstOrDefault(x => string.Equals(x.Name.LocalName, "NLap", StringComparison.OrdinalIgnoreCase))
                    ?.Value
                    ?.Trim();

                if (string.IsNullOrWhiteSpace(nlapValue))
                {
                    return null;
                }

                if (DateTime.TryParseExact(
                        nlapValue,
                        new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy" },
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsed))
                {
                    return parsed;
                }

                return DateTime.TryParse(nlapValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                    ? parsed
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static string SanitizePathPart(string value, string fallback)
        {
            var normalized = Regex.Replace(value.Trim(), @"[^\w\-]", "_");
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }
    }
}

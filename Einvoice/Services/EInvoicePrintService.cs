using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Reporting;
using DevExpress.XtraReports.UI;
using PuppeteerSharp;
using System.Xml.Linq;
namespace API_AMNOTE_WEB.Services
{
    public class EInvoicePrintService : IEInvoicePrintService
    {
        private readonly IEInvoiceRepository _repository;
        private readonly IEInvoiceSettingRepository _settingRepository;
        private readonly IEInvoiceSellerRepository _sellerRepository;
        private readonly IEInvoiceHtmlToPdfService _htmlToPdfService;
        private readonly IEInvoiceXmlStorageService _xmlStorageService;
        private readonly IWebHostEnvironment _environment;
        public EInvoicePrintService(
            IEInvoiceRepository repository,
            IEInvoiceSettingRepository settingRepository,
            IEInvoiceSellerRepository sellerRepository,
            IEInvoiceHtmlToPdfService htmlToPdfService,
            IEInvoiceXmlStorageService xmlStorageService,
            IWebHostEnvironment environment)
        {
            _repository = repository;
            _settingRepository = settingRepository;
            _sellerRepository = sellerRepository;
            _htmlToPdfService = htmlToPdfService;
            _xmlStorageService = xmlStorageService;
            _environment = environment;
        }
        public Task<string> GetHtmlAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return BuildHtmlAsync(companyCd, invoiceId, options, cancellationToken);
        }
        public async Task<XtraReport> BuildReportAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var (invoice, html) = await BuildPrintContentAsync(companyCd, invoiceId, options, requireSignedXml: false, cancellationToken);
            return new EInvoiceHtmlReport(html, BuildReportTitle(invoice));
        }
        public async Task<byte[]> ExportPdfAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var (invoice, html) = await BuildPrintContentAsync(companyCd, invoiceId, options, requireSignedXml: false, cancellationToken);
            return await ExportHtmlToPdfAsync(html, BuildReportTitle(invoice), cancellationToken);
        }
        public async Task<byte[]> ExportPdfFromSignedXmlAsync(
            string companyCd,
            long invoiceId,
            CancellationToken cancellationToken = default)
        {
            var (invoice, html) = await BuildPrintContentAsync(companyCd, invoiceId, options: null, requireSignedXml: true, cancellationToken);
            return await ExportHtmlToPdfAsync(html, BuildReportTitle(invoice), cancellationToken);
        }
        public async Task<byte[]> ExportPdfFromSignedXmlContentAsync(
            string companyCd,
            long invoiceId,
            string signedXml,
            CancellationToken cancellationToken = default)
        {
            var html = await BuildHtmlFromSignedXmlContentAsync(
                companyCd,
                invoiceId,
                signedXml,
                options: null,
                cancellationToken);
            var invoice = await ResolvePrintInvoiceAsync(companyCd, invoiceId, options: null, cancellationToken)
                ?? throw new KeyNotFoundException("E-invoice not found");
            return await ExportHtmlToPdfAsync(html, BuildReportTitle(invoice), cancellationToken);
        }
        private Task<string> BuildHtmlAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options,
            CancellationToken cancellationToken)
            => BuildHtmlAsync(companyCd, invoiceId, options, requireSignedXml: false, cancellationToken);
        private async Task<string> BuildHtmlAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options,
            bool requireSignedXml,
            CancellationToken cancellationToken)
        {
            var (_, html) = await BuildPrintContentAsync(companyCd, invoiceId, options, requireSignedXml, cancellationToken);
            return html;
        }
        private async Task<(EInvoiceInfo Invoice, string Html)> BuildPrintContentAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options,
            bool requireSignedXml,
            CancellationToken cancellationToken)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");
            }
            cancellationToken.ThrowIfCancellationRequested();
            var invoice = await ResolvePrintInvoiceAsync(companyCd, invoiceId, options, cancellationToken)
                ?? throw new KeyNotFoundException("E-invoice not found");
            var xml = await ResolveInvoiceXmlAsync(companyCd, invoice, options, requireSignedXml, cancellationToken);
            var html = await BuildHtmlFromInvoiceXmlAsync(companyCd, invoice, xml, options, cancellationToken);
            return (invoice, html);
        }
        private async Task<string> BuildHtmlFromSignedXmlContentAsync(
            string companyCd,
            long invoiceId,
            string signedXml,
            EInvoicePrintOptions? options,
            CancellationToken cancellationToken)
        {
            ValidateCompany(companyCd);
            if (invoiceId <= 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("INVOICE_ID");
            }
            cancellationToken.ThrowIfCancellationRequested();
            ValidateXml(signedXml, "XML");
            var invoice = await ResolvePrintInvoiceAsync(companyCd, invoiceId, options, cancellationToken)
                ?? throw new KeyNotFoundException("E-invoice not found");
            return await BuildHtmlFromInvoiceXmlAsync(companyCd, invoice, signedXml, options, cancellationToken);
        }
        private async Task<string> BuildHtmlFromInvoiceXmlAsync(
            string companyCd,
            EInvoiceInfo invoice,
            string xml,
            EInvoicePrintOptions? options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            xml = EInvoicePrintXmlHelper.ApplyPrintInfo(xml, options, ResolveConvertedByName(options), invoice.MTRACUU);
            var seller = await ResolveSellerForPrintAsync(companyCd, invoice, includeXslContent: true);
            var xsl = Common.NormalizeNullableText(seller.XSL_CONTENT);
            if (string.IsNullOrWhiteSpace(xsl))
            {
                throw new InvalidOperationException("Seller XSL template is not configured.");
            }
            return EInvoiceXmlHtmlTransformService.Transform(
                xml,
                xsl,
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.LOGO_PATH, "logoImage", _environment),
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.BACKGROUND_PATH, "backgroundImage", _environment),
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.INVOICE_BACKGROUND_PATH, "nenImage", _environment),
                EInvoicePrintImageHelper.ResolveTransformImage(xsl, seller.INVOICE_BORDER_PATH, "vienHdImage", _environment),
                lookupCompanyCd: companyCd);
        }
        private static string BuildReportTitle(EInvoiceInfo invoice)
        {
            var parts = new[]
            {
                Common.NormalizeNullableText(invoice.KHHDON),
                Common.NormalizeNullableText(invoice.SHDON),
            }.Where(x => !string.IsNullOrWhiteSpace(x));
            var suffix = string.Join("-", parts);
            return string.IsNullOrWhiteSpace(suffix) ? "E-Invoice" : $"E-Invoice {suffix}";
        }
        private async Task<EInvoiceInfo?> ResolvePrintInvoiceAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options,
            CancellationToken cancellationToken)
        {
            if (CanUseFastSignedPrintPath(options))
            {
                return BuildInvoiceFromPrintHints(companyCd, invoiceId, options!);
            }
            var header = await LoadInvoiceHeaderAsync(companyCd, invoiceId, cancellationToken);
            if (header == null)
            {
                return null;
            }
            ApplyPrintHints(header, options);
            return header;
        }
        private static bool CanUseFastSignedPrintPath(EInvoicePrintOptions? options)
        {
            if (options == null)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(options.XmlFtpPath) || options.SellerId is not > 0)
            {
                return false;
            }
            return options.IsSigned is null or 1;
        }
        private static EInvoiceInfo BuildInvoiceFromPrintHints(string companyCd, long invoiceId, EInvoicePrintOptions options)
        {
            return new EInvoiceInfo
            {
                INVOICE_ID = invoiceId,
                COMPANY_CD = companyCd,
                XML_FTP_PATH = options.XmlFtpPath,
                SELLER_ID = options.SellerId,
                XSL_ID = options.XslId,
                KHHDON = options.Khhdon,
                SHDON = options.Shdon,
                MTRACUU = options.Mtracuu,
                IS_SIGNED = options.IsSigned ?? 1,
            };
        }
        private static void ApplyPrintHints(EInvoiceInfo invoice, EInvoicePrintOptions? options)
        {
            if (options == null)
            {
                return;
            }
            var xmlFtpPath = Common.NormalizeNullableText(options.XmlFtpPath);
            if (!string.IsNullOrWhiteSpace(xmlFtpPath))
            {
                invoice.XML_FTP_PATH = xmlFtpPath;
            }
            if (options.SellerId is > 0)
            {
                invoice.SELLER_ID = options.SellerId;
            }
            if (options.XslId is > 0)
            {
                invoice.XSL_ID = options.XslId;
            }
            var khhdon = Common.NormalizeNullableText(options.Khhdon);
            if (!string.IsNullOrWhiteSpace(khhdon))
            {
                invoice.KHHDON = khhdon;
            }
            var shdon = Common.NormalizeNullableText(options.Shdon);
            if (!string.IsNullOrWhiteSpace(shdon))
            {
                invoice.SHDON = shdon;
            }
            var mtracuu = Common.NormalizeNullableText(options.Mtracuu);
            if (!string.IsNullOrWhiteSpace(mtracuu))
            {
                invoice.MTRACUU = mtracuu;
            }
            if (options.IsSigned is 0 or 1)
            {
                invoice.IS_SIGNED = options.IsSigned.Value;
            }
        }
        private async Task<EInvoiceInfo?> LoadInvoiceHeaderAsync(string companyCd, long invoiceId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await _repository.GetHeadersAsync(companyCd, invoiceId, null, null, null)).FirstOrDefault();
        }
        private async Task EnsureInvoiceDetailsAsync(string companyCd, EInvoiceInfo invoice, CancellationToken cancellationToken)
        {
            if (invoice.DETAILS.Count > 0)
            {
                return;
            }
            cancellationToken.ThrowIfCancellationRequested();
            var bundle = await _repository.GetDetailsBundleAsync(companyCd, new[] { invoice.INVOICE_ID });
            var specialByDetailId = bundle.Specials.ToDictionary(x => x.DETAIL_ID);
            var details = bundle.Details.ToList();
            foreach (var detail in details)
            {
                if (specialByDetailId.TryGetValue(detail.DETAIL_ID, out var special))
                {
                    detail.SPECIAL = special;
                }
            }
            invoice.DETAILS = details;
            invoice.PXK_INFO = bundle.PxkItems.FirstOrDefault(x => x.INVOICE_ID == invoice.INVOICE_ID);
            invoice.RELATED = bundle.RelatedItems.FirstOrDefault(x => x.INVOICE_ID == invoice.INVOICE_ID);
        }
        private async Task<byte[]> ExportHtmlToPdfAsync(string html, string title, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await _htmlToPdfService.ConvertAsync(html, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or PuppeteerSharp.ProcessException)
            {
                return ExportHtmlToPdfWithDevExpress(html, title);
            }
        }
        private static byte[] ExportHtmlToPdfWithDevExpress(string html, string title)
        {
            using var report = new EInvoiceHtmlReport(html, title);
            using var stream = new MemoryStream();
            report.ExportToPdf(stream);
            var pdf = stream.ToArray();
            if (pdf.Length <= 512
                || pdf[0] != (byte)'%'
                || pdf[1] != (byte)'P'
                || pdf[2] != (byte)'D'
                || pdf[3] != (byte)'F')
            {
                throw new InvalidOperationException("Generated PDF is empty.");
            }
            return pdf;
        }
        private async Task<string> ResolveInvoiceXmlAsync(
            string companyCd,
            EInvoiceInfo invoice,
            EInvoicePrintOptions? options,
            bool requireSignedXml,
            CancellationToken cancellationToken = default)
        {
            var hintedPath = Common.NormalizeNullableText(options?.XmlFtpPath);
            if (!string.IsNullOrWhiteSpace(hintedPath))
            {
                var hintedXml = await _xmlStorageService.TryDownloadAsync(hintedPath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(hintedXml))
                {
                    ValidateXml(hintedXml, "XML");
                    return hintedXml;
                }
            }
            var storedXml = await _xmlStorageService.ResolveSignedInvoiceXmlAsync(companyCd, invoice, cancellationToken);
            if (!string.IsNullOrWhiteSpace(storedXml))
            {
                ValidateXml(storedXml, "XML");
                return storedXml;
            }
            if (requireSignedXml)
            {
                throw new InvalidOperationException("Signed invoice XML is required to generate PDF from XSL template");
            }
            await EnsureInvoiceDetailsAsync(companyCd, invoice, cancellationToken);
            var rawXml = await BuildRawXmlAsync(companyCd, invoice);
            ValidateXml(rawXml, "RAW_XML");
            return rawXml;
        }
        private async Task<string> BuildRawXmlAsync(string companyCd, EInvoiceInfo invoice)
        {
            var seller = await ResolveSellerForPrintAsync(companyCd, invoice);
            var decimalSettings = await _settingRepository.GetDecimalSettingsAsync(companyCd, null, null, null, null, false, seller.XSL_ID);
            var formatter = EInvoiceDecimalFormatter.Create(decimalSettings);
            var rawXml = new EInvoiceXmlBuilder(formatter, invoice.DVTTE, invoice.KHMSHDON).Build(invoice, seller);
            if (string.IsNullOrWhiteSpace(rawXml))
            {
                throw new InvalidOperationException("Failed to generate e-invoice XML");
            }
            return rawXml;
        }
        private async Task<EInvoiceSellerInfo> ResolveSellerForPrintAsync(
            string companyCd,
            EInvoiceInfo invoice,
            bool includeXslContent = false)
        {
            if (invoice.XSL_ID is > 0)
            {
                var matched = includeXslContent
                    ? await _sellerRepository.GetSellerWithXslAsync(
                        companyCd,
                        sellerId: invoice.SELLER_ID,
                        xslId: invoice.XSL_ID,
                        includeInactive: true)
                    : (await _sellerRepository.GetSellersAsync(
                        companyCd,
                        sellerId: invoice.SELLER_ID,
                        xslId: invoice.XSL_ID,
                        includeInactive: true)).FirstOrDefault();
                if (matched != null)
                {
                    return matched;
                }

                throw new InvalidOperationException("E-invoice XSL template not found");
            }

            if (invoice.SELLER_ID is > 0)
            {
                var matched = includeXslContent
                    ? await _sellerRepository.GetSellerWithXslAsync(
                        companyCd,
                        sellerId: invoice.SELLER_ID,
                        khhdon: invoice.KHHDON,
                        includeInactive: true)
                    : (await _sellerRepository.GetSellersAsync(
                        companyCd,
                        khhdon: invoice.KHHDON,
                        sellerId: invoice.SELLER_ID,
                        includeInactive: true)).FirstOrDefault();
                if (matched != null)
                {
                    return matched;
                }
            }
            var khhdon = Common.NormalizeNullableText(invoice.KHHDON);
            var fallbackSellers = (await _sellerRepository.GetSellersAsync(
                companyCd,
                khhdon: khhdon,
                includeInactive: false,
                includeXslContent: includeXslContent)).ToList();
            if (fallbackSellers.Count == 0)
            {
                fallbackSellers = (await _sellerRepository.GetSellersAsync(
                    companyCd,
                    includeInactive: false,
                    includeXslContent: includeXslContent)).ToList();
            }
            if (fallbackSellers.Count == 0)
            {
                throw new InvalidOperationException("E-invoice seller is not configured");
            }
            return fallbackSellers.FirstOrDefault(x => x.XSL_IS_DEFAULT == 1) ?? fallbackSellers[0];
        }
        private static void ValidateXml(string xml, string fieldName)
        {
            try
            {
                XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{fieldName} is invalid XML: {ex.Message}");
            }
        }
        private static string? ResolveConvertedByName(EInvoicePrintOptions? options)
        {
            var requested = Common.NormalizeNullableText(options?.ConvertedByNm);
            if (!string.IsNullOrWhiteSpace(requested))
            {
                return requested;
            }
            try
            {
                return Common.NormalizeNullableText(Common.GetUserName());
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
        private static void ValidateCompany(string companyCd)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                throw new UnauthorizedAccessException("Company code not found");
            }
        }
    }
}

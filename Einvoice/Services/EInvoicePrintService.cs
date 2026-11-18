using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Xml.Linq;
namespace API_AMNOTE_WEB.Services
{
    public class EInvoicePrintService : IEInvoicePrintService
    {
        private readonly ElectronicDocumentPdfService _pdf;
        private readonly IEInvoiceRepository _repository;
        private readonly IEInvoiceSettingRepository _settingRepository;
        private readonly IEInvoiceSellerRepository _sellerRepository;
        private readonly IEInvoiceXmlStorageService _xmlStorageService;
        private readonly IWebHostEnvironment _environment;
        public EInvoicePrintService(
            IEInvoiceRepository repository,
            IEInvoiceSettingRepository settingRepository,
            IEInvoiceSellerRepository sellerRepository,
            IEInvoiceXmlStorageService xmlStorageService,
            IWebHostEnvironment environment, ElectronicDocumentPdfService pdf)
        {
            _repository = repository;
            _pdf = pdf;
            _settingRepository = settingRepository;
            _sellerRepository = sellerRepository;
            _xmlStorageService = xmlStorageService;
            _environment = environment;
        }
        public async Task<byte[]> ExportPdfAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var normalized = new EInvoicePrintOptions { IsConvertedPrint = options?.IsConvertedPrint ?? false,
                ConvertedByNm = options?.IsConvertedPrint == true ? ResolveConvertedByName(options) : null };
            return await _pdf.GetAsync(companyCd, "invoice", invoiceId,
                new { normalized.IsConvertedPrint, normalized.ConvertedByNm },
                ct => BuildHtmlAsync(companyCd, invoiceId, normalized, ct), cancellationToken);
        }
        public Task<byte[]> ExportPdfFromSignedXmlAsync(string companyCd, long invoiceId, CancellationToken cancellationToken = default)
            => ExportSignedPdfAsync(companyCd, invoiceId, cancellationToken);

        private async Task<byte[]> ExportSignedPdfAsync(string companyCd, long invoiceId, CancellationToken cancellationToken)
        {
            var invoice = await LoadInvoiceHeaderAsync(companyCd, invoiceId, cancellationToken)
                ?? throw new KeyNotFoundException("E-invoice not found");
            if (invoice.IS_SIGNED != 1) throw new InvalidOperationException("Signed XML is required.");
            return await ExportPdfAsync(companyCd, invoiceId, cancellationToken: cancellationToken);
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
        private async Task<EInvoiceInfo?> ResolvePrintInvoiceAsync(
            string companyCd,
            long invoiceId,
            EInvoicePrintOptions? options,
            CancellationToken cancellationToken)
        {
            return await LoadInvoiceHeaderAsync(companyCd, invoiceId, cancellationToken);
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
        private async Task<string> ResolveInvoiceXmlAsync(
            string companyCd,
            EInvoiceInfo invoice,
            EInvoicePrintOptions? options,
            bool requireSignedXml,
            CancellationToken cancellationToken = default)
        {
            if (invoice.IS_SIGNED == 1 || requireSignedXml)
            {
                if (string.IsNullOrWhiteSpace(invoice.XML_FTP_PATH)) throw new InvalidOperationException("Signed XML FTP reference is missing.");
                var storedXml = await _xmlStorageService.DownloadAsync(invoice.XML_FTP_PATH, cancellationToken);
                ValidateXml(storedXml, "XML");
                return storedXml;
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
            throw new InvalidOperationException("Document seller/XSL template is not configured.");
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

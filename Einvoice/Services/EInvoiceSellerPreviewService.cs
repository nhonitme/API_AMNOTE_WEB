using System.Linq;
using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Logging;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceSellerPreviewService : IEInvoiceSellerPreviewService
    {
        private readonly IEInvoiceSellerRepository _repository;
        private readonly IEInvoiceHtmlToPdfService _htmlToPdfService;
        private readonly IWebHostEnvironment _environment;
        private readonly DapperExecutor _db;
        private readonly ILogger<EInvoiceSellerPreviewService> _logger;

        public EInvoiceSellerPreviewService(
            IEInvoiceSellerRepository repository,
            IEInvoiceHtmlToPdfService htmlToPdfService,
            IWebHostEnvironment environment,
            DapperExecutor db,
            ILogger<EInvoiceSellerPreviewService> logger)
        {
            _repository = repository;
            _htmlToPdfService = htmlToPdfService;
            _environment = environment;
            _db = db;
            _logger = logger;
        }

        public async Task<EInvoiceSellerPreviewDto> GetPreviewAsync(string companyCd, long sellerId, long? xslId = null)
        {
            // Template "Xem" = same XML shape as designer (seller + draft signature only).
            var built = await BuildPreviewAsync(companyCd, sellerId, xslId, previewOptions: DesignerPreviewOptions());
            return new EInvoiceSellerPreviewDto
            {
                SELLER_ID = built.Seller.SELLER_ID,
                XSL_ID = built.Seller.XSL_ID,
                SELLER_NM = built.Seller.SELLER_NM,
                KHMSHDON = built.Seller.KHMSHDON,
                KHHDON = built.Seller.KHHDON,
                XSL_TEMPLATE_NM = built.Seller.XSL_TEMPLATE_NM,
                XML = built.Xml,
                XSL = built.Xsl,
                HTML = built.Html,
            };
        }

        public async Task<string> GetHtmlAsync(string companyCd, long sellerId, long? xslId = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var built = await BuildPreviewAsync(companyCd, sellerId, xslId, previewOptions: DesignerPreviewOptions());
            return built.Html;
        }

        public async Task<string> GetDecimalDemoHtmlAsync(
            string companyCd,
            long sellerId,
            EInvoiceSellerDecimalPreviewRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rules = (request.DecimalSettings ?? [])
                .Select(MapDecimalSetting)
                .ToList();
            var options = new EInvoiceSellerPreviewOptions
            {
                CurrencyCode = request.CurrencyCode ?? "VND",
                Formatter = EInvoiceDecimalFormatter.Create(rules),
            };
            var built = await BuildPreviewAsync(companyCd, sellerId, request.XslId, previewOptions: options);
            return built.Html;
        }

        public async Task<string> GetDesignerHtmlAsync(
            string companyCd,
            long sellerId,
            long? xslId = null,
            long? designId = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var built = await BuildPreviewAsync(
                companyCd,
                sellerId,
                xslId,
                preferDesignXsl: true,
                designId,
                previewOptions: DesignerPreviewOptions());
            return built.Html;
        }

        public async Task<string> GetDesignerLiveHtmlAsync(
            string companyCd,
            long sellerId,
            string xslContent,
            long? xslId = null,
            long? designId = null,
            IReadOnlyDictionary<string, string>? extraParameters = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(xslContent))
            {
                throw EInvoiceValidationMessages.RequiredArgument("XSL_CONTENT");
            }

            var built = await BuildPreviewAsync(
                companyCd,
                sellerId,
                xslId,
                preferDesignXsl: true,
                designId,
                xslContentOverride: xslContent,
                extraParameters: extraParameters,
                previewOptions: DesignerPreviewOptions());
            return built.Html;
        }

        private static EInvoiceSellerPreviewOptions DesignerPreviewOptions()
            => new() { IncludeDemoContent = false };

        public async Task<byte[]> ExportPdfAsync(string companyCd, long sellerId, long? xslId = null, CancellationToken cancellationToken = default)
        {
            var html = await GetHtmlAsync(companyCd, sellerId, xslId, cancellationToken);
            return await _htmlToPdfService.ConvertAsync(html, cancellationToken);
        }

        public async Task<byte[]> ExportDesignerPdfAsync(
            string companyCd,
            long sellerId,
            long? xslId = null,
            long? designId = null,
            string? xslContent = null,
            IReadOnlyDictionary<string, string>? extraParameters = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string html;
            if (!string.IsNullOrWhiteSpace(xslContent))
            {
                html = await GetDesignerLiveHtmlAsync(
                    companyCd,
                    sellerId,
                    xslContent,
                    xslId,
                    designId,
                    extraParameters,
                    cancellationToken);
            }
            else
            {
                html = await GetDesignerHtmlAsync(companyCd, sellerId, xslId, designId, cancellationToken);
            }

            return await _htmlToPdfService.ConvertAsync(html, cancellationToken);
        }

        private async Task<BuiltSellerPreview> BuildPreviewAsync(
            string companyCd,
            long sellerId,
            long? xslId,
            bool preferDesignXsl = false,
            long? designId = null,
            string? xslContentOverride = null,
            IReadOnlyDictionary<string, string>? extraParameters = null,
            EInvoiceSellerPreviewOptions? previewOptions = null)
        {
            ValidateCompany(companyCd);
            if (sellerId <= 0)
            {
                throw EInvoiceValidationMessages.RequiredArgument("SELLER_ID");
            }

            var seller = await _repository.GetSellerWithXslAsync(companyCd, sellerId: sellerId, xslId: xslId, includeInactive: true)
                ?? throw new KeyNotFoundException("E-invoice seller not found");

            var xsl = Common.NormalizeNullableText(preferDesignXsl ? seller.XSL_DESIGN_CONTENT : seller.XSL_CONTENT)
                ?? Common.NormalizeNullableText(seller.XSL_CONTENT)
                ?? Common.NormalizeNullableText(seller.XSL_DESIGN_CONTENT);
            var logoPath = seller.LOGO_PATH;
            var backgroundPath = seller.BACKGROUND_PATH;
            var nenPath = seller.INVOICE_BACKGROUND_PATH;
            var vienPath = seller.INVOICE_BORDER_PATH;

            if (designId is > 0)
            {
                var templateXslId = xslId is > 0 ? xslId.Value : seller.XSL_ID;
                const string designSql = "CALL getEInvoiceSellerXslTemplateDesign(@p_COMPANY_CD, @p_XSL_ID, @p_DESIGN_ID);";
                var design = (await _db.QueryAsync<EInvoiceTemplateDesignerDesign>(
                    Net_DB.Net_DB_Company,
                    designSql,
                    new { p_COMPANY_CD = companyCd, p_XSL_ID = templateXslId, p_DESIGN_ID = designId })).FirstOrDefault();
                if (design != null)
                {
                    xsl = Common.NormalizeNullableText(design.XSL_CONTENT) ?? xsl;
                    logoPath = design.LOGO_PATH;
                    backgroundPath = design.BACKGROUND_PATH;
                    nenPath = design.INVOICE_BACKGROUND_PATH;
                    vienPath = design.INVOICE_BORDER_PATH;
                }
                else
                {
                    _logger.LogWarning(
                        "E-invoice designer preview design not found. designId={DesignId} xslId={XslId} sellerXslId={SellerXslId}. Falling back to default design image paths.",
                        designId,
                        templateXslId,
                        seller.XSL_ID);
                }
            }

            if (!string.IsNullOrWhiteSpace(xslContentOverride))
            {
                xsl = Common.NormalizeNullableText(xslContentOverride) ?? xsl;
            }

            if (string.IsNullOrWhiteSpace(xsl))
            {
                throw new InvalidOperationException("Seller XSL template is not configured.");
            }

            var xml = await EInvoiceSellerPreviewXmlBuilder.BuildAsync(seller, previewOptions);
            var logoImage = EInvoicePrintImageHelper.ResolveTransformImage(xsl, logoPath, "logoImage", _environment, _logger);
            var backgroundImage = EInvoicePrintImageHelper.ResolveTransformImage(xsl, backgroundPath, "backgroundImage", _environment, _logger);
            var nenImage = EInvoicePrintImageHelper.ResolveTransformImage(xsl, nenPath, "nenImage", _environment, _logger);
            var vienImage = EInvoicePrintImageHelper.ResolveTransformImage(xsl, vienPath, "vienHdImage", _environment, _logger);

            var html = EInvoiceXmlHtmlTransformService.Transform(
                xml,
                xsl,
                logoImage,
                backgroundImage,
                nenImage,
                vienImage,
                lookupCompanyCd: companyCd,
                extraParameters: extraParameters);

            return new BuiltSellerPreview(seller, xml, xsl, html);
        }

        private static EInvoiceDecimalSetting MapDecimalSetting(EInvoiceDecimalSettingDto dto)
        {
            return new EInvoiceDecimalSetting
            {
                SETTING_ID = dto.SETTING_ID,
                COMPANY_CD = dto.COMPANY_CD ?? string.Empty,
                XSL_ID = dto.XSL_ID,
                APPLY_TARGET = dto.APPLY_TARGET ?? string.Empty,
                FIELD_SCOPE = dto.FIELD_SCOPE ?? string.Empty,
                FIELD_NAME = dto.FIELD_NAME ?? string.Empty,
                LABEL_TEXT = dto.LABEL_TEXT,
                CAPTION = dto.CAPTION,
                CURRENCY_SCOPE = dto.CURRENCY_SCOPE ?? "ANY",
                DECIMAL_SCALE = dto.DECIMAL_SCALE,
                ROUND_MODE = dto.ROUND_MODE ?? "ROUND",
                IS_ACTIVE = dto.IS_ACTIVE,
                SORT_ORDER = dto.SORT_ORDER,
                NOTE = dto.NOTE ?? string.Empty,
            };
        }

        private static void ValidateCompany(string companyCd)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                throw new UnauthorizedAccessException("Company code not found");
            }
        }

        private sealed record BuiltSellerPreview(EInvoiceSellerInfo Seller, string Xml, string Xsl, string Html);
    }
}

using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceMinutePreviewService : IEInvoiceMinutePreviewService
    {
        public const string DefaultMinuteTemplateCd = "BBDCTT";

        private readonly IEInvoiceMinuteService _minuteService;
        private readonly IEInvoiceTemplateRepository _templateRepository;
        private readonly ElectronicDocumentPdfService _pdf;

        public EInvoiceMinutePreviewService(
            IEInvoiceMinuteService minuteService,
            IEInvoiceTemplateRepository templateRepository,
            ElectronicDocumentPdfService pdf)
        {
            _minuteService = minuteService;
            _templateRepository = templateRepository;
            _pdf = pdf;
        }

        private async Task<string> BuildHtmlAsync(string companyCd, long bbanId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var built = await BuildPreviewAsync(companyCd, bbanId, cancellationToken);
            return built.Html;
        }

        public async Task<byte[]> ExportPdfAsync(string companyCd, long bbanId, CancellationToken cancellationToken = default)
        {
            return await _pdf.GetAsync(companyCd, "minute", bbanId, null,
                ct => BuildHtmlAsync(companyCd, bbanId, ct), cancellationToken);
        }

        private async Task<EInvoiceXslPreviewResult> BuildPreviewAsync(
            string companyCd,
            long bbanId,
            CancellationToken cancellationToken)
        {
            EInvoiceXslPreviewHelper.ValidateCompany(companyCd);
            if (bbanId <= 0)
                throw EInvoiceValidationMessages.RequiredArgument("BBAN_ID");

            cancellationToken.ThrowIfCancellationRequested();

            var xml = await _minuteService.GetPreviewXmlAsync(companyCd, bbanId);
            var template = await _templateRepository.GetActiveTemplateAsync(
                    EInvoiceXslPreviewHelper.TemplateTypeXsl,
                    DefaultMinuteTemplateCd)
                ?? throw new InvalidOperationException(
                    $"E-invoice minute XSL template '{DefaultMinuteTemplateCd}' is not configured.");

            var xsl = Common.NormalizeNullableText(template.CONTENT);
            if (string.IsNullOrWhiteSpace(xsl))
                throw new InvalidOperationException(
                    $"E-invoice minute XSL template '{DefaultMinuteTemplateCd}' is empty.");

            var html = EInvoiceXmlHtmlTransformService.Transform(xml, xsl, lookupCompanyCd: companyCd);
            return new EInvoiceXslPreviewResult(xml, xsl, html);
        }
    }
}

using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceDeclarationPreviewService : IEInvoiceDeclarationPreviewService
    {
        public const string DefaultDeclarationTemplateCd = "01/ĐKTĐ-HĐĐT";

        private readonly IEInvoiceDeclarationRepository _declarationRepository;
        private readonly IEInvoiceTemplateRepository _templateRepository;
        private readonly ElectronicDocumentPdfService _pdf;

        public EInvoiceDeclarationPreviewService(
            IEInvoiceDeclarationRepository declarationRepository,
            IEInvoiceTemplateRepository templateRepository,
            ElectronicDocumentPdfService pdf)
        {
            _declarationRepository = declarationRepository;
            _templateRepository = templateRepository;
            _pdf = pdf;
        }

        private async Task<string> BuildHtmlAsync(string companyCd, long tkhaiId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var built = await BuildPreviewAsync(companyCd, tkhaiId);
            return built.Html;
        }

        public async Task<byte[]> ExportPdfAsync(string companyCd, long tkhaiId, CancellationToken cancellationToken = default)
        {
            return await _pdf.GetAsync(companyCd, "declaration", tkhaiId, null,
                ct => BuildHtmlAsync(companyCd, tkhaiId, ct), cancellationToken);
        }

        private Task<EInvoiceXslPreviewResult> BuildPreviewAsync(string companyCd, long tkhaiId)
            => EInvoiceXslPreviewHelper.BuildAsync(
                companyCd,
                tkhaiId,
                new EInvoiceXslPreviewRequest<EInvoiceDeclarationInfo>(
                    DocumentLabel: "Declaration",
                    IdFieldName: "TKHAI_ID",
                    DefaultTemplateCd: DefaultDeclarationTemplateCd,
                    NotFoundMessage: "E-invoice declaration not found",
                    LoadEntityAsync: LoadDeclarationAsync,
                    GetTemplateCd: declaration => declaration.MSO,
                    ResolveXml: ResolveDeclarationXml),
                _templateRepository);

        private async Task<EInvoiceDeclarationInfo?> LoadDeclarationAsync(string companyCd, long tkhaiId)
        {
            var declaration = (await _declarationRepository.GetHeadersAsync(companyCd, tkhaiId, null, null, null, null)).FirstOrDefault();
            if (declaration == null)
            {
                return null;
            }

            declaration.DETAILS = (await _declarationRepository.GetDetailsAsync(companyCd, tkhaiId)).ToList();
            return declaration;
        }

        private static string ResolveDeclarationXml(EInvoiceDeclarationInfo declaration)
            => declaration.IS_SIGNED == 1 && string.IsNullOrWhiteSpace(declaration.XML)
                ? throw new InvalidOperationException("Signed document XML is missing.")
                : EInvoiceXslPreviewHelper.ResolveStoredOrBuiltXml(
                declaration,
                entity => entity.XML,
                EInvoiceDeclarationXmlBuilder.Build,
                "Failed to generate e-invoice declaration XML");
    }
}

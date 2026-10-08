using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services
{
    public class EInvoiceErrorNoticePreviewService : IEInvoiceErrorNoticePreviewService
    {
        public const string DefaultErrorNoticeTemplateCd = "04/SS-HĐĐT";

        private readonly IEInvoiceErrorNoticeRepository _noticeRepository;
        private readonly IEInvoiceTemplateRepository _templateRepository;
        private readonly IEInvoiceHtmlToPdfService _htmlToPdfService;

        public EInvoiceErrorNoticePreviewService(
            IEInvoiceErrorNoticeRepository noticeRepository,
            IEInvoiceTemplateRepository templateRepository,
            IEInvoiceHtmlToPdfService htmlToPdfService)
        {
            _noticeRepository = noticeRepository;
            _templateRepository = templateRepository;
            _htmlToPdfService = htmlToPdfService;
        }

        public async Task<string> GetHtmlAsync(string companyCd, long tbaoId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var built = await BuildPreviewAsync(companyCd, tbaoId);
            return built.Html;
        }

        public async Task<byte[]> ExportPdfAsync(string companyCd, long tbaoId, CancellationToken cancellationToken = default)
        {
            var html = await GetHtmlAsync(companyCd, tbaoId, cancellationToken);
            return await _htmlToPdfService.ConvertAsync(html, cancellationToken);
        }

        private Task<EInvoiceXslPreviewResult> BuildPreviewAsync(string companyCd, long tbaoId)
            => EInvoiceXslPreviewHelper.BuildAsync(
                companyCd,
                tbaoId,
                new EInvoiceXslPreviewRequest<EInvoiceErrorNoticeInfo>(
                    DocumentLabel: "Error notice",
                    IdFieldName: "TBAO_ID",
                    DefaultTemplateCd: DefaultErrorNoticeTemplateCd,
                    NotFoundMessage: "E-invoice error notice not found",
                    LoadEntityAsync: LoadNoticeAsync,
                    GetTemplateCd: notice => notice.MSO,
                    ResolveXml: ResolveErrorNoticeXml),
                _templateRepository);

        private async Task<EInvoiceErrorNoticeInfo?> LoadNoticeAsync(string companyCd, long tbaoId)
        {
            var notice = (await _noticeRepository.GetHeadersAsync(companyCd, tbaoId, null, null, null, null)).FirstOrDefault();
            if (notice == null)
            {
                return null;
            }

            notice.DETAILS = (await _noticeRepository.GetDetailsAsync(companyCd, tbaoId)).ToList();
            return notice;
        }

        private static string ResolveErrorNoticeXml(EInvoiceErrorNoticeInfo notice)
            => EInvoiceXslPreviewHelper.ResolveStoredOrBuiltXml(
                notice,
                entity => entity.XML,
                EInvoiceErrorNoticeXmlBuilder.Build,
                "Failed to generate e-invoice error notice XML");
    }
}

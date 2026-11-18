using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed partial class PitService(
    IPitRepository repository,
    IPitXslService xslTemplates,
    IPitMessageOutboundService transmission,
    ICompanyDatabaseResolver databases,
    IEInvoiceMessageRepository messages,
    IEInvoiceTemplateRepository einvoiceTemplates,
    ElectronicDocumentPdfService pdf,
    IPitCertificateService certificates,
    IPitDeclarationService declarations,
    IPitErrorNoticeService notices,
    IMailSettingRepository mailSettings,
    IMailService mailService,
    IEInvoiceEmailHistoryRepository mailHistory,
    IConfiguration configuration,
    ILogger<PitService> logger) : IPitService
{
    private const string DeclarationPrintTemplateCode = "01/ĐKTĐ-CTĐT";
    private const string ErrorNoticePrintTemplateCode = "04/SS-CTĐT";

    public Task<(List<PitDocument> Items, int TotalRecords)> Search(
        string company,
        string kind,
        DateTime? from,
        DateTime? to,
        string? keyword,
        int? signed,
        int? cqtStatus,
        int pageNumber,
        int pageSize)
    {
        if (from > to) throw new ArgumentException("Khoảng ngày không hợp lệ.");
        if (pageNumber < 0 || pageSize < 0 || pageSize > 500) throw new ArgumentException("Phân trang không hợp lệ.");
        return repository.Search(company, kind, from: from, to: to, keyword: keyword, signed: signed, cqtStatus: cqtStatus, pageNumber: pageNumber, pageSize: pageSize);
    }
    public Task<PitDocument> Get(string company, string kind, long id) => repository.Get(company, kind, id);
    public async Task<PitDocument> SaveCertificate(string company, string user, long id, PitCertificateDto data)
    {
        var savedId = await certificates.SaveAsync(company, user, id, data.DOC_VERSION, data.XSL_ID, data);
        return await repository.Get(company, PitKinds.Certificate, savedId);
    }

    public async Task<PitDocument> SaveDeclaration(string company, string user, long id, PitDeclarationDto data)
    {
        var savedId = await declarations.SaveAsync(company, user, id, data.DOC_VERSION, data);
        return await repository.Get(company, PitKinds.Declaration, savedId);
    }

    public async Task<PitDocument> SaveErrorNotice(string company, string user, long id, PitErrorNoticeDto data)
    {
        var savedId = await notices.SaveAsync(company, user, id, data.DOC_VERSION, data);
        return await repository.Get(company, PitKinds.ErrorNotice, savedId);
    }
    public Task Delete(string company, string user, string kind, long id, int version) => repository.Delete(company, user, kind, id, version);
    public async Task<PitSigningPayload> Prepare(string company, string kind, long id)
    {
        if (kind == PitKinds.Certificate)
            await certificates.ValidateSigningAsync(company, id);
        var row = await repository.Prepare(company, kind, id);
        return new(row.DOC_VERSION, row.RAW_XML!, "PIT");
    }
    public async Task<PitDocument> Sign(string company, string user, string kind, long id, PitSignRequest request)
    {
        if (kind == PitKinds.Certificate) await certificates.ValidateSigningAsync(company, id, acceptingSignature: true);
        await repository.Sign(company, user, kind, id, request);
        try { await transmission.DispatchAsync(await databases.ResolveDatabaseNameAsync(company), company); }
        catch (Exception ex) { logger.LogWarning(ex, "PIT document {Id} signed; outbox retry pending", id); }
        return await repository.Get(company, kind, id);
    }
    public async Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> History(string company, string kind, long id)
    {
        var row = await repository.Get(company, kind, id);
        return string.IsNullOrWhiteSpace(row.MTDIEP) ? Array.Empty<EInvoiceMessageReceiveInfo>() : await messages.GetReceiveMessagesByLookupCodeAsync(company, row.MTDIEP);
    }
    public async Task<string> Xml(string company, string kind, long id)
    {
        var row = await repository.Get(company, kind, id);
        return row.IS_SIGNED == 1 ? row.SIGNED_XML ?? throw new InvalidOperationException("XML đã ký bị thiếu.") : PitXmlBuilder.Build(row);
    }
    private async Task<string> BuildPrintHtml(string company, string kind, long id)
    {
        var row = await repository.Get(company, kind, id);
        var xml = row.IS_SIGNED == 1
            ? row.SIGNED_XML ?? throw new InvalidOperationException("XML đã ký bị thiếu.")
            : PitXmlBuilder.Build(row);
        if (kind == PitKinds.Certificate)
            xml = PitCertificatePrintXmlHelper.EnsureLookupInfo(xml, row.MTRACUU);
        if (kind is PitKinds.Declaration or PitKinds.ErrorNotice)
        {
            var templateCode = kind == PitKinds.Declaration ? DeclarationPrintTemplateCode : ErrorNoticePrintTemplateCode;
            var template = await einvoiceTemplates.GetActiveTemplateAsync("XSL", templateCode)
                ?? throw new InvalidOperationException($"Chưa cấu hình mẫu in {templateCode}.");
            if (string.IsNullOrWhiteSpace(template.CONTENT))
                throw new InvalidOperationException($"Mẫu in {templateCode} chưa có nội dung XSL.");
            return EInvoiceXmlHtmlTransformService.Transform(xml, template.CONTENT, lookupCompanyCd: company);
        }
        if (kind != PitKinds.Certificate)
            throw new InvalidOperationException("Loại chứng từ này chưa hỗ trợ mẫu in XSL.");
        var certificateTemplate = await xslTemplates.ResolveForPrintAsync(company, row.XSL_ID);
        return await xslTemplates.TransformAsync(company, xml, certificateTemplate);
    }

    public async Task<byte[]> PrintPdf(
        string company,
        string kind,
        long id,
        CancellationToken cancellationToken = default)
    {
        return await pdf.GetAsync(company, "pit-" + kind, id, null,
            _ => BuildPrintHtml(company, kind, id), cancellationToken);
    }

}

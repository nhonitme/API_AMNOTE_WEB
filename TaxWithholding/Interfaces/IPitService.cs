using API_AMNOTE_WEB.TaxWithholding;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitService
{
    Task<(List<PitDocument> Items, int TotalRecords)> Search(
        string company,
        string kind,
        DateTime? from,
        DateTime? to,
        string? keyword,
        int? signed,
        int? cqtStatus,
        int pageNumber,
        int pageSize);

    Task<PitDocument> Get(string company, string kind, long id);

    Task<PitSendMailResult> SendMail(string company, string user, string kind, long id, PitSendMailRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EInvoiceEmailHistoryInfo>> MailHistory(string company, string kind, long id);

    Task<PitDocument> SaveCertificate(string company, string user, long id, PitCertificateDto data);

    Task<PitDocument> SaveDeclaration(string company, string user, long id, PitDeclarationDto data);

    Task<PitDocument> SaveErrorNotice(string company, string user, long id, PitErrorNoticeDto data);

    Task Delete(string company, string user, string kind, long id, int version);

    Task<PitSigningPayload> Prepare(string company, string kind, long id);

    Task<PitDocument> Sign(string company, string user, string kind, long id, PitSignRequest request);

    Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> History(string company, string kind, long id);

    Task<string> Xml(string company, string kind, long id);


    Task<byte[]> PrintPdf(
        string company,
        string kind,
        long id,
        CancellationToken cancellationToken = default);
}

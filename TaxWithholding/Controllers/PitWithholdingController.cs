using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using API_AMNOTE_WEB.Controllers;
using API_AMNOTE_WEB.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.TaxWithholding;

[ApiController, Authorize, Route("api/pit-withholding")]
public sealed class PitWithholdingController(IPitService service) : BaseApiController
{

    [HttpGet("{kind}")]
    public async Task<IActionResult> Search(
        string kind,
        [FromQuery] string? fromYmd,
        [FromQuery] string? toYmd,
        [FromQuery] string? keyword,
        [FromQuery] int? signed,
        [FromQuery] int? cqtStatus,
        [FromQuery] int pageNumber = 0,
        [FromQuery] int pageSize = 0)
    {
        var fromDate = Common.ParseNullableYmdDate(fromYmd, nameof(fromYmd));
        var toDate = Common.ParseNullableYmdDate(toYmd, nameof(toYmd));
        var result = await service.Search(Common.GetCompanyCode(), kind, fromDate, toDate, keyword, signed, cqtStatus, pageNumber, pageSize);
        return pageSize > 0
            ? PagedSuccess(result.Items, pageNumber, pageSize, result.TotalRecords)
            : Success(result.Items);
    }
    [HttpGet("{kind}/{id:long}")] public async Task<IActionResult> Get(string kind, long id) => Success(await service.Get(Common.GetCompanyCode(), kind, id));

    [HttpPost("certificate")] public async Task<IActionResult> CreateCertificate(PitCertificateDto request) => Success(await service.SaveCertificate(Common.GetCompanyCode(), Common.GetUserId(), 0, request));
    [HttpPut("certificate/{id:long}")] public async Task<IActionResult> UpdateCertificate(long id, PitCertificateDto request) => Success(await service.SaveCertificate(Common.GetCompanyCode(), Common.GetUserId(), id, request));
    [HttpPost("declaration")] public async Task<IActionResult> CreateDeclaration(PitDeclarationDto request) => Success(await service.SaveDeclaration(Common.GetCompanyCode(), Common.GetUserId(), 0, request));
    [HttpPut("declaration/{id:long}")] public async Task<IActionResult> UpdateDeclaration(long id, PitDeclarationDto request) => Success(await service.SaveDeclaration(Common.GetCompanyCode(), Common.GetUserId(), id, request));
    [HttpPost("error-notice")] public async Task<IActionResult> CreateErrorNotice(PitErrorNoticeDto request) => Success(await service.SaveErrorNotice(Common.GetCompanyCode(), Common.GetUserId(), 0, request));
    [HttpPut("error-notice/{id:long}")] public async Task<IActionResult> UpdateErrorNotice(long id, PitErrorNoticeDto request) => Success(await service.SaveErrorNotice(Common.GetCompanyCode(), Common.GetUserId(), id, request));
    [HttpDelete("{kind}/{id:long}")] public async Task<IActionResult> Delete(string kind, long id, [FromQuery] int version) { await service.Delete(Common.GetCompanyCode(), Common.GetUserId(), kind, id, version); return Success(true); }
    [HttpPost("{kind}/{id:long}/signing-payload")] public async Task<IActionResult> Prepare(string kind, long id) => Success(await service.Prepare(Common.GetCompanyCode(), kind, id));
    [HttpPost("{kind}/{id:long}/signature")] public async Task<IActionResult> Sign(string kind, long id, PitSignRequest request) => Success(await service.Sign(Common.GetCompanyCode(), Common.GetUserId(), kind, id, request));
    [HttpGet("{kind}/{id:long}/xml")] public async Task<IActionResult> Xml(string kind, long id) => Content(await service.Xml(Common.GetCompanyCode(), kind, id), "application/xml; charset=utf-8");
    [HttpGet("{kind}/{id:long}/print/pdf")]
    public async Task<IActionResult> PrintPdf(string kind, long id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return File(await service.PrintPdf(Common.GetCompanyCode(), kind, id, cancellationToken), "application/pdf", $"PIT_{kind}_{id}.pdf");
    }
    [HttpGet("{kind}/{id:long}/history")] public async Task<IActionResult> History(string kind, long id) => Success(await service.History(Common.GetCompanyCode(), kind, id));

    [HttpGet("{kind}/{id:long}/mail-history")]
    public async Task<IActionResult> MailHistory(string kind, long id)
        => Success(await service.MailHistory(Common.GetCompanyCode(), kind, id));

    [HttpPost("{kind}/{id:long}/send-mail")]
    public async Task<IActionResult> SendMail(string kind, long id, PitSendMailRequest request, CancellationToken cancellationToken)
        => Success(await service.SendMail(Common.GetCompanyCode(), Common.GetUserId(), kind, id, request, cancellationToken));
}

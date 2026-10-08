using API_AMNOTE_WEB.Controllers;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.TaxWithholding;

[ApiController, Authorize, Route("api/pit-withholding/xsl-templates")]
public sealed class PitXslTemplateController(PitXslService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? templateCd)
        => Success(await service.ListAsync(Common.GetCompanyCode(), templateCd));

    [HttpGet("{xslId:long}")]
    public async Task<IActionResult> Get(long xslId)
        => Success(await service.GetAsync(Common.GetCompanyCode(), xslId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PitXslTemplateSaveRequest request)
        => Success(await service.SaveAsync(Common.GetCompanyCode(), Common.GetUserId(), 0, request));

    [HttpPut("{xslId:long}")]
    public async Task<IActionResult> Update(long xslId, [FromBody] PitXslTemplateSaveRequest request)
        => Success(await service.SaveAsync(Common.GetCompanyCode(), Common.GetUserId(), xslId, request));

    [HttpDelete("{xslId:long}")]
    public async Task<IActionResult> Delete(long xslId)
    {
        await service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), xslId);
        return Success(true);
    }

    [HttpPost("{xslId:long}/images/{imageKind}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(long xslId, string imageKind, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var result = await service.UploadImageAsync(
            Common.GetCompanyCode(),
            Common.GetUserId(),
            xslId,
            imageKind,
            file.FileName,
            ms.ToArray(),
            cancellationToken);
        return Success(result);
    }

    [HttpPut("{xslId:long}/images/{imageKind}")]
    public async Task<IActionResult> SelectImage(long xslId, string imageKind, [FromBody] EInvoiceFtpImageSelectRequest? request)
    {
        var result = await service.SelectImageAsync(
            Common.GetCompanyCode(),
            Common.GetUserId(),
            xslId,
            imageKind,
            request?.FILE_NAME,
            request?.PATH);
        return Success(result);
    }

    [HttpGet("{xslId:long}/preview/html")]
    public async Task<IActionResult> PreviewHtml(long xslId)
        => Content(await service.PreviewHtmlAsync(Common.GetCompanyCode(), xslId), "text/html; charset=utf-8");

    [HttpGet("{xslId:long}/preview/pdf")]
    public async Task<IActionResult> PreviewPdf(long xslId, CancellationToken cancellationToken)
        => File(
            await service.PreviewPdfAsync(Common.GetCompanyCode(), xslId, cancellationToken),
            "application/pdf",
            $"PIT_03-TNCN_{xslId}.pdf");
}

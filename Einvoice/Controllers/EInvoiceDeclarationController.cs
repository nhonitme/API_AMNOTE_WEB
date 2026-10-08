using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EInvoiceDeclarationController : BaseApiController
    {
        private readonly IEInvoiceDeclarationService _service;
        private readonly IEInvoiceDeclarationPreviewService _previewService;

        public EInvoiceDeclarationController(
            IEInvoiceDeclarationService service,
            IEInvoiceDeclarationPreviewService previewService)
        {
            _service = service;
            _previewService = previewService;
        }

        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] long? tkhaiId = null,
            [FromQuery] string? fromYmd = null,
            [FromQuery] string? toYmd = null,
            [FromQuery] string? keyword = null,
            [FromQuery] int? isSigned = null,
            [FromQuery] bool includeDetails = false)
        {
            var data = await _service.SearchAsync(Common.GetCompanyCode(), new EInvoiceDeclarationSearchRequest
            {
                TkhaiId = tkhaiId,
                FromDate = Common.ParseNullableYmdDate(fromYmd, nameof(fromYmd)),
                ToDate = Common.ParseNullableYmdDate(toYmd, nameof(toYmd)),
                Keyword = keyword,
                IsSigned = isSigned,
                IncludeDetails = includeDetails
            });
            return Success(data);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            var data = await _service.GetByIdAsync(Common.GetCompanyCode(), id);
            if (data == null)
                return NotFound("E-invoice declaration not found");

            return Success(data);
        }

        [HttpGet("{id:long}/transmission-messages")]
        public async Task<IActionResult> GetTransmissionMessages([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            var data = await _service.GetTransmissionMessagesAsync(Common.GetCompanyCode(), id);
            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EInvoiceDeclarationSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(data, "E-invoice declaration created successfully");
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] EInvoiceDeclarationSaveRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice declaration updated successfully");
        }

        [HttpGet("{id:long}/preview/html")]
        public async Task<IActionResult> GetPreviewHtml([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            return await FromPreviewAsync(async () =>
            {
                var html = await _previewService.GetHtmlAsync(Common.GetCompanyCode(), id, HttpContext.RequestAborted);
                return Content(html, "text/html; charset=utf-8");
            });
        }

        [HttpGet("{id:long}/preview/pdf")]
        public async Task<IActionResult> GetPreviewPdf([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _previewService.ExportPdfAsync(Common.GetCompanyCode(), id, HttpContext.RequestAborted);
                return File(pdf, "application/pdf", $"EInvoiceDeclaration_{id}.pdf");
            });
        }

        [HttpGet("{id:long}/signing-payload")]
        public async Task<IActionResult> GetSigningPayload([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            var data = await _service.GetSigningPayloadAsync(Common.GetCompanyCode(), Common.GetUserId(), id);
            return Success(data);
        }

        [HttpPost("{id:long}/signature")]
        public async Task<IActionResult> SaveSignature([FromRoute] long id, [FromBody] EInvoiceDeclarationSignRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.SaveSignatureAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice declaration signed successfully");
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TKHAI_ID"));

            var affected = await _service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), id);
            return Deleted(new { deleted = affected }, "E-invoice declaration deleted successfully");
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] EInvoiceDeclarationDeleteManyRequest? request)
        {
            if (request?.TkhaiIds == null || request.TkhaiIds.Count == 0)
                return ValidationError(await FieldRequiredAsync("TkhaiIds"));

            var affected = await _service.DeleteManyAsync(Common.GetCompanyCode(), Common.GetUserId(), request.TkhaiIds);
            return Success(new { deleted = affected }, "E-invoice declarations deleted successfully");
        }
    }
}

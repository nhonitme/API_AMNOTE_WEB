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
    public class EInvoiceMinutesController : BaseApiController
    {
        private readonly IEInvoiceMinuteService _service;
        private readonly IEInvoiceMinutePreviewService _previewService;

        public EInvoiceMinutesController(
            IEInvoiceMinuteService service,
            IEInvoiceMinutePreviewService previewService)
        {
            _service = service;
            _previewService = previewService;
        }

        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] long? bbanId = null,
            [FromQuery] string? fromYmd = null,
            [FromQuery] string? toYmd = null,
            [FromQuery] string? keyword = null,
            [FromQuery] int? isSigned = null,
            [FromQuery] bool includeReasons = false)
        {
            var data = await _service.SearchAsync(Common.GetCompanyCode(), new EInvoiceMinuteSearchRequest
            {
                BbanId = bbanId,
                FromDate = Common.ParseNullableYmdDate(fromYmd, nameof(fromYmd)),
                ToDate = Common.ParseNullableYmdDate(toYmd, nameof(toYmd)),
                Keyword = keyword,
                IsSigned = isSigned,
                IncludeReasons = includeReasons
            });
            return Success(data);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("BBAN_ID"));

            var data = await _service.GetByIdAsync(Common.GetCompanyCode(), id);
            if (data == null)
                return NotFound("E-invoice minute not found");

            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EInvoiceMinuteSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(data, "E-invoice minute created successfully");
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] EInvoiceMinuteSaveRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("BBAN_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice minute updated successfully");
        }

        [HttpGet("{id:long}/preview/html")]
        public async Task<IActionResult> GetPreviewHtml([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("BBAN_ID"));

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
                return ValidationError(await FieldRequiredAsync("BBAN_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _previewService.ExportPdfAsync(Common.GetCompanyCode(), id, HttpContext.RequestAborted);
                return File(pdf, "application/pdf", $"EInvoiceMinute_{id}.pdf");
            });
        }

        [HttpGet("{id:long}/signing-payload")]
        public async Task<IActionResult> GetSigningPayload([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("BBAN_ID"));

            var data = await _service.GetSigningPayloadAsync(Common.GetCompanyCode(), Common.GetUserId(), id);
            return Success(data);
        }

        [HttpPost("{id:long}/signature")]
        public async Task<IActionResult> SaveSignature([FromRoute] long id, [FromBody] EInvoiceMinuteSignRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("BBAN_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.SaveSignatureAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice minute signed successfully");
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("BBAN_ID"));

            var affected = await _service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), id);
            return Deleted(new { deleted = affected }, "E-invoice minute deleted successfully");
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] EInvoiceMinuteDeleteManyRequest? request)
        {
            if (request?.BbanIds == null || request.BbanIds.Count == 0)
                return ValidationError(await FieldRequiredAsync("BbanIds"));

            var affected = await _service.DeleteManyAsync(Common.GetCompanyCode(), Common.GetUserId(), request.BbanIds);
            return Success(new { deleted = affected }, "E-invoice minutes deleted successfully");
        }

        [HttpPost("send-mail")]
        public async Task<IActionResult> SendMail([FromBody] EInvoiceMinuteSendMailRequest? request, CancellationToken cancellationToken)
        {
            if (request?.Items == null || request.Items.Count == 0)
                return ValidationError(await FieldRequiredAsync("Items"));

            var result = await _service.SendMailAsync(Common.GetCompanyCode(), Common.GetUserId(), request, cancellationToken);
            return Success(result, "E-invoice minute mail sent successfully");
        }
    }
}

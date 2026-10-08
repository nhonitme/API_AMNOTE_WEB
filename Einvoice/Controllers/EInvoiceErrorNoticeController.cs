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
    public class EInvoiceErrorNoticeController : BaseApiController
    {
        private readonly IEInvoiceErrorNoticeService _service;
        private readonly IEInvoiceErrorNoticePreviewService _previewService;

        public EInvoiceErrorNoticeController(
            IEInvoiceErrorNoticeService service,
            IEInvoiceErrorNoticePreviewService previewService)
        {
            _service = service;
            _previewService = previewService;
        }

        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] long? tbaoId = null,
            [FromQuery] string? fromYmd = null,
            [FromQuery] string? toYmd = null,
            [FromQuery] string? keyword = null,
            [FromQuery] int? isSigned = null,
            [FromQuery] bool includeDetails = false)
        {
            var data = await _service.SearchAsync(Common.GetCompanyCode(), new EInvoiceErrorNoticeSearchRequest
            {
                TbaoId = tbaoId,
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
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

            var data = await _service.GetByIdAsync(Common.GetCompanyCode(), id);
            if (data == null)
                return NotFound("E-invoice error notice not found");

            return Success(data);
        }

        [HttpGet("{id:long}/transmission-messages")]
        public async Task<IActionResult> GetTransmissionMessages([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

            var data = await _service.GetTransmissionMessagesAsync(Common.GetCompanyCode(), id);
            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EInvoiceErrorNoticeSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(data, "E-invoice error notice created successfully");
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] EInvoiceErrorNoticeSaveRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice error notice updated successfully");
        }

        [HttpGet("{id:long}/preview/html")]
        public async Task<IActionResult> GetPreviewHtml([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

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
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _previewService.ExportPdfAsync(Common.GetCompanyCode(), id, HttpContext.RequestAborted);
                return File(pdf, "application/pdf", $"EInvoiceErrorNotice_{id}.pdf");
            });
        }

        [HttpGet("{id:long}/signing-payload")]
        public async Task<IActionResult> GetSigningPayload([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

            var data = await _service.GetSigningPayloadAsync(Common.GetCompanyCode(), Common.GetUserId(), id);
            return Success(data);
        }

        [HttpPost("{id:long}/signature")]
        public async Task<IActionResult> SaveSignature([FromRoute] long id, [FromBody] EInvoiceErrorNoticeSignRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.SaveSignatureAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice error notice signed successfully");
        }

        [HttpPost("send-mail")]
        public async Task<IActionResult> SendMail([FromBody] EInvoiceErrorNoticeSendMailRequest? request, CancellationToken cancellationToken)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var result = await _service.SendMailAsync(Common.GetCompanyCode(), Common.GetUserId(), request, cancellationToken);
            return Success(result, "E-invoice error notice mail sent successfully");
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("TBAO_ID"));

            var affected = await _service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), id);
            return Deleted(new { deleted = affected }, "E-invoice error notice deleted successfully");
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] EInvoiceErrorNoticeDeleteManyRequest? request)
        {
            if (request?.TbaoIds == null || request.TbaoIds.Count == 0)
                return ValidationError(await FieldRequiredAsync("TbaoIds"));

            var affected = await _service.DeleteManyAsync(Common.GetCompanyCode(), Common.GetUserId(), request.TbaoIds);
            return Success(new { deleted = affected }, "E-invoice error notices deleted successfully");
        }
    }
}

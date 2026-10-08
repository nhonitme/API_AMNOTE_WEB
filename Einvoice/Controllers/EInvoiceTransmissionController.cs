using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EInvoiceTransmissionController : BaseApiController
    {
        private readonly IEInvoiceTransmissionPreviewService _previewService;

        public EInvoiceTransmissionController(IEInvoiceTransmissionPreviewService previewService)
        {
            _previewService = previewService;
        }

        [HttpGet("receive/{id:long}/preview/html")]
        public async Task<IActionResult> GetReceivePreviewHtml([FromRoute] long id, [FromQuery] long? invoiceId = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("RECEIVE_ID"));

            return await FromPreviewAsync(async () =>
            {
                var html = await _previewService.BuildReceivePreviewHtmlAsync(Common.GetCompanyCode(), id, invoiceId);
                return Content(html, "text/html; charset=utf-8");
            }, notFoundMessage: "E-invoice receive message not found", includeArgumentException: true);
        }
    }
}

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

        [HttpGet("receive/{id:long}/preview/pdf")]
        public async Task<IActionResult> GetReceivePreviewPdf([FromRoute] long id, [FromQuery] long? invoiceId = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("RECEIVE_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _previewService.ExportPdfAsync(Common.GetCompanyCode(), id, HttpContext.RequestAborted);
                Response.Headers.CacheControl = "private, no-store";
                return File(pdf, "application/pdf");
            }, notFoundMessage: "E-invoice receive message not found", includeArgumentException: true);
        }
    }
}

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
    public class EInvoiceController : BaseApiController
    {
        private readonly IEInvoiceService _service;
        private readonly IEInvoicePrintService _printService;

        public EInvoiceController(IEInvoiceService service, IEInvoicePrintService printService)
        {
            _service = service;
            _printService = printService;
        }

        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] long? invoiceId = null,
            [FromQuery] string? fromYmd = null,
            [FromQuery] string? toYmd = null,
            [FromQuery] string? keyword = null,
            [FromQuery] string? khhdon = null,
            [FromQuery] string? khhdonOp = null,
            [FromQuery] string? shdonFrom = null,
            [FromQuery] string? shdonTo = null,
            [FromQuery] string? nmuaTen = null,
            [FromQuery] string? nmuaTenOp = null,
            [FromQuery] string? nmuaMst = null,
            [FromQuery] string? nmuaMstOp = null,
            [FromQuery] int? invoiceStatus = null,
            [FromQuery] int? cqtStatus = null,
            [FromQuery] int? isSigned = null,
            [FromQuery] int? tchdon = null,
            [FromQuery] int? mailStatus = null,
            [FromQuery] bool? cashRegister = null,
            [FromQuery] bool includeDetails = false,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 0)
        {
            var request = new EInvoiceSearchRequest
            {
                CashRegister = cashRegister,
                InvoiceId = invoiceId,
                FromDate = Common.ParseNullableYmdDate(fromYmd, nameof(fromYmd)),
                ToDate = Common.ParseNullableYmdDate(toYmd, nameof(toYmd)),
                Keyword = keyword,
                Khhdon = khhdon,
                KhhdonOp = khhdonOp,
                ShdonFrom = shdonFrom,
                ShdonTo = shdonTo,
                NmuaTen = nmuaTen,
                NmuaTenOp = nmuaTenOp,
                NmuaMst = nmuaMst,
                NmuaMstOp = nmuaMstOp,
                InvoiceStatus = invoiceStatus,
                CqtStatus = cqtStatus,
                IsSigned = isSigned,
                Tchdon = tchdon,
                MailStatus = mailStatus,
                IncludeDetails = includeDetails,
                PageNumber = invoiceId is > 0 ? 1 : pageNumber,
                PageSize = invoiceId is > 0 ? 1 : pageSize,
            };

            if (request.PageSize > 0)
            {
                if (request.PageNumber <= 0)
                {
                    return ValidationError("pageNumber must be greater than 0");
                }

                var pageResult = await _service.SearchPagedAsync(Common.GetCompanyCode(), request);
                return PagedSuccess(pageResult.Items, request.PageNumber, request.PageSize, pageResult.TotalRecords);
            }

            var data = await _service.SearchAsync(Common.GetCompanyCode(), request);
            return Success(data);
        }

        [HttpGet("sellers")]
        public async Task<IActionResult> GetSellers(
            [FromQuery] string? khhdon = null,
            [FromQuery] long? sellerId = null,
            [FromQuery] string? keyword = null,
            [FromQuery] bool includeInactive = false,
            [FromQuery] bool includeAllTemplates = false)
        {
            var data = await _service.GetSellersAsync(Common.GetCompanyCode(), new EInvoiceSellerSearchRequest
            {
                Khhdon = khhdon,
                SellerId = sellerId,
                Keyword = keyword,
                IncludeInactive = includeInactive,
                IncludeAllTemplates = includeAllTemplates
            });
            return Success(data);
        }

        [HttpGet("bke/next-no")]
        public async Task<IActionResult> GetNextBkeNo([FromQuery] int? year = null)
        {
            var nextNo = await _service.GetNextBkeNoAsync(Common.GetCompanyCode(), year);
            return Success(new { NEXT_SBKE = nextNo, Year = year is > 0 ? year : DateTime.Today.Year });
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> Lookup([FromQuery] string? mtracuu = null)
        {
            var lookupCode = Common.NormalizeNullableText(mtracuu);
            if (string.IsNullOrWhiteSpace(lookupCode))
                return ValidationError(await FieldRequiredAsync("MTRACUU"));

            var data = await _service.LookupByCodeAsync(Common.GetCompanyCode(), lookupCode);
            if (data == null)
                return NotFound("E-invoice not found");

            return Success(data);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            var data = await _service.GetByIdAsync(Common.GetCompanyCode(), id);
            if (data == null)
                return NotFound("E-invoice not found");

            return Success(data);
        }

        [HttpGet("{id:long}/transmission-messages")]
        public async Task<IActionResult> GetTransmissionMessages([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            var data = await _service.GetTransmissionMessagesAsync(Common.GetCompanyCode(), id);
            return Success(data);
        }

        [HttpGet("{id:long}/mail-history")]
        public async Task<IActionResult> GetMailHistory([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            var data = await _service.GetMailHistoryAsync(Common.GetCompanyCode(), id);
            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EInvoiceSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(data, "E-invoice created successfully");
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] EInvoiceSaveRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice updated successfully");
        }

        [HttpPut("{id:long}/buyer-email")]
        public async Task<IActionResult> UpdateBuyerEmail([FromRoute] long id, [FromBody] EInvoiceUpdateBuyerEmailRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.UpdateBuyerEmailAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice buyer email updated successfully");
        }

        [HttpGet("{id:long}/signing-payload")]
        public async Task<IActionResult> GetSigningPayload([FromRoute] long id)
        {
            if (id <= 0) return ValidationError(await FieldRequiredAsync("INVOICE_ID"));
            return Success(await _service.GetSigningPayloadAsync(Common.GetCompanyCode(), Common.GetUserId(), id));
        }

        [HttpPost("{id:long}/mtt/issue")]
        public async Task<IActionResult> IssueMtt([FromRoute] long id)
        {
            if (id <= 0) return ValidationError(await FieldRequiredAsync("INVOICE_ID"));
            return Success(await _service.IssueMttAsync(Common.GetCompanyCode(), Common.GetUserId(), id));
        }

        [HttpPost("mtt/signing-payload")]
        public async Task<IActionResult> PrepareMttBatch([FromBody] EInvoiceMttBatchRequest request)
            => Success(await _service.PrepareMttBatchAsync(Common.GetCompanyCode(), Common.GetUserId(), request));

        [HttpPost("mtt/xml")]
        public async Task<IActionResult> ExportMttBatch([FromBody] EInvoiceMttBatchRequest request)
            => Content(await _service.GetMttBatchXmlAsync(Common.GetCompanyCode(), Common.GetUserId(), request), "application/xml; charset=utf-8");

        [HttpPost("mtt/{batchId}/signature")]
        public async Task<IActionResult> SaveMttBatch([FromRoute] string batchId, [FromBody] EInvoiceMttBatchSignRequest request)
            => Success(await _service.SaveMttBatchAsync(Common.GetCompanyCode(), Common.GetUserId(), batchId, request));

        [HttpPost("{id:long}/signature")]
        public Task<IActionResult> SaveSignature([FromRoute] long id, [FromBody] EInvoiceSignRequest? request)
            => ExecuteSaveSignatureAsync(id, request);

        [HttpPost("{id:long}/save-signed-xml")]
        public Task<IActionResult> SaveSignedXml([FromRoute] long id, [FromBody] EInvoiceSaveSignedXmlRequest? request)
            => ExecuteSaveSignatureAsync(id, request?.ToSignRequest());

        private async Task<IActionResult> ExecuteSaveSignatureAsync(long id, EInvoiceSignRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.SaveSignatureAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(data, "E-invoice signed successfully");
        }

        [HttpGet("{id:long}/xml")]
        public async Task<IActionResult> GetXml([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            return await FromPreviewAsync(async () =>
            {
                var xml = await _service.GetXmlAsync(Common.GetCompanyCode(), id);
                return Content(xml, "application/xml; charset=utf-8");
            }, notFoundMessage: "E-invoice not found", includeArgumentException: true);
        }

        [HttpGet("{id:long}/print/html")]
        public async Task<IActionResult> ExportPrintHtml(
            [FromRoute] long id,
            [FromQuery] string? convertedPrint = null,
            [FromQuery] string? convertedByNm = null,
            [FromQuery] string? xmlFtpPath = null,
            [FromQuery] long? sellerId = null,
            [FromQuery] long? xslId = null,
            [FromQuery] string? khhdon = null,
            [FromQuery] string? shdon = null,
            [FromQuery] string? mtracuu = null,
            [FromQuery] int? isSigned = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            return await FromPreviewAsync(async () =>
            {
                var html = await _printService.GetHtmlAsync(
                    Common.GetCompanyCode(),
                    id,
                    BuildPrintOptions(convertedPrint, convertedByNm, xmlFtpPath, sellerId, xslId, khhdon, shdon, mtracuu, isSigned),
                    HttpContext.RequestAborted);
                return Content(html, "text/html; charset=utf-8");
            }, notFoundMessage: "E-invoice not found");
        }

        [HttpGet("{id:long}/print/pdf")]
        public async Task<IActionResult> ExportPrintPdf(
            [FromRoute] long id,
            [FromQuery] string? convertedPrint = null,
            [FromQuery] string? convertedByNm = null,
            [FromQuery] string? xmlFtpPath = null,
            [FromQuery] long? sellerId = null,
            [FromQuery] long? xslId = null,
            [FromQuery] string? khhdon = null,
            [FromQuery] string? shdon = null,
            [FromQuery] string? mtracuu = null,
            [FromQuery] int? isSigned = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _printService.ExportPdfAsync(
                    Common.GetCompanyCode(),
                    id,
                    BuildPrintOptions(convertedPrint, convertedByNm, xmlFtpPath, sellerId, xslId, khhdon, shdon, mtracuu, isSigned),
                    HttpContext.RequestAborted);
                var fileName = $"EInvoice_{id}.pdf";
                return File(pdf, "application/pdf", fileName);
            }, notFoundMessage: "E-invoice not found");
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("INVOICE_ID"));

            var affected = await _service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), id);
            return Deleted(new { deleted = affected }, "E-invoice deleted successfully");
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] EInvoiceDeleteManyRequest? request)
        {
            if (request?.InvoiceIds == null || request.InvoiceIds.Count == 0)
                return ValidationError(await FieldRequiredAsync("InvoiceIds"));

            var affected = await _service.DeleteManyAsync(Common.GetCompanyCode(), Common.GetUserId(), request.InvoiceIds);
            return Success(new { deleted = affected }, "E-invoices deleted successfully");
        }

        [HttpPost("send-mail")]
        public async Task<IActionResult> SendMail([FromBody] EInvoiceSendMailRequest? request, CancellationToken cancellationToken)
        {
            if (request?.Items == null || request.Items.Count == 0)
                return ValidationError(await FieldRequiredAsync("Items"));

            var result = await _service.SendMailAsync(Common.GetCompanyCode(), Common.GetUserId(), request, cancellationToken);
            return Success(result, "E-invoice mail sent successfully");
        }

        private static EInvoicePrintOptions BuildPrintOptions(
            string? convertedPrint,
            string? convertedByNm,
            string? xmlFtpPath = null,
            long? sellerId = null,
            long? xslId = null,
            string? khhdon = null,
            string? shdon = null,
            string? mtracuu = null,
            int? isSigned = null)
        {
            return new EInvoicePrintOptions
            {
                IsConvertedPrint = IsConvertedPrintQuery(convertedPrint),
                ConvertedByNm = Common.NormalizeNullableText(convertedByNm),
                XmlFtpPath = Common.NormalizeNullableText(xmlFtpPath),
                SellerId = sellerId is > 0 ? sellerId : null,
                XslId = xslId is > 0 ? xslId : null,
                Khhdon = Common.NormalizeNullableText(khhdon),
                Shdon = Common.NormalizeNullableText(shdon),
                Mtracuu = Common.NormalizeNullableText(mtracuu),
                IsSigned = isSigned is 0 or 1 ? isSigned : null,
            };
        }

        private static bool IsConvertedPrintQuery(string? convertedPrint)
        {
            if (string.IsNullOrWhiteSpace(convertedPrint))
            {
                return false;
            }

            return convertedPrint.Trim() is "1" or "true" or "True" or "TRUE" or "yes" or "Yes" or "YES";
        }
    }
}

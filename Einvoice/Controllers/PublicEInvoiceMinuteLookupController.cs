using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Middleware;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/public/einvoice/minutes/lookup")]
    public class PublicEInvoiceMinuteLookupController : BaseApiController
    {
        private readonly IEInvoiceMinuteService _service;
        private readonly IEInvoiceMinutePreviewService _previewService;
        private readonly ILogger<PublicEInvoiceMinuteLookupController> _logger;

        public PublicEInvoiceMinuteLookupController(
            IEInvoiceMinuteService service,
            IEInvoiceMinutePreviewService previewService,
            ILogger<PublicEInvoiceMinuteLookupController> logger)
        {
            _service = service;
            _previewService = previewService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        [EnableRateLimiting("public-einvoice-lookup")]
        public async Task<IActionResult> Lookup(
            [FromQuery] string? companyCd = null,
            [FromQuery] string? taxCode = null,
            [FromQuery] string? mtracuu = null,
            [FromQuery] string? mst = null,
            [FromQuery] string? buyerTaxCode = null,
            [FromQuery] string? sellerTaxCode = null,
            [FromQuery] string? code = null,
            [FromQuery] string? lookupCode = null,
            [FromQuery] string? format = null)
        {
            var requestTaxCode = ResolveTaxCode(companyCd, taxCode, mst, buyerTaxCode, sellerTaxCode);
            if (string.IsNullOrWhiteSpace(requestTaxCode))
            {
                return ValidationError(await FieldRequiredAsync("TAX_CD"));
            }

            var requestLookupCode = ResolveLookupCode(mtracuu, code, lookupCode);
            if (string.IsNullOrWhiteSpace(requestLookupCode))
            {
                return ValidationError(await FieldRequiredAsync("MTRACUU"));
            }

            var requestFormat = (Common.NormalizeNullableText(format) ?? "json").ToLowerInvariant();
            if (requestFormat is not ("json" or "pdf" or "xml" or "xsl" or "html"))
            {
                return ValidationError("format must be json, pdf, xml, xsl, or html");
            }

            _logger.LogInformation(
                "Public e-invoice minute lookup requested. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
                MaskValue(requestTaxCode),
                requestFormat,
                MaskValue(requestLookupCode),
                GetRemoteIp());

            if (requestFormat == "json")
            {
                var data = await _service.LookupPublicAsync(requestTaxCode!, requestLookupCode!, HttpContext.RequestAborted);
                if (data == null)
                {
                    LogLookupMiss(requestTaxCode!, requestLookupCode!, requestFormat);
                    return NotFound("E-invoice minute not found");
                }

                return Success(data);
            }

            var downloadInfo = await _service.GetPublicDownloadInfoAsync(requestTaxCode!, requestLookupCode!);
            if (downloadInfo == null)
            {
                LogLookupMiss(requestTaxCode!, requestLookupCode!, requestFormat);
                return NotFound("E-invoice minute not found");
            }

            SetCompanyContext(downloadInfo.COMPANY_CD);

            if (requestFormat == "pdf")
            {
                return await FromPreviewAsync(async () =>
                {
                    var pdf = await _previewService.ExportPdfAsync(
                        downloadInfo.COMPANY_CD!,
                        downloadInfo.BBAN_ID,
                        HttpContext.RequestAborted);

                    return File(pdf, "application/pdf", BuildFileName(downloadInfo, "pdf"));
                }, notFoundMessage: "E-invoice minute PDF not found");
            }

            if (requestFormat == "xml")
            {
                var xml = await _service.GetPublicXmlAsync(requestTaxCode!, requestLookupCode!);
                if (string.IsNullOrWhiteSpace(xml))
                {
                    return NotFound("E-invoice minute XML not found");
                }

                return File(Encoding.UTF8.GetBytes(xml), "application/xml", BuildFileName(downloadInfo, "xml"));
            }

            if (requestFormat == "xsl")
            {
                var xsl = await _service.GetPublicXslAsync(requestTaxCode!, requestLookupCode!);
                if (string.IsNullOrWhiteSpace(xsl))
                {
                    return NotFound("E-invoice minute XSL not found");
                }

                return File(Encoding.UTF8.GetBytes(xsl), "application/xml", BuildFileName(downloadInfo, "xsl"));
            }

            var html = await _service.GetPublicHtmlAsync(requestTaxCode!, requestLookupCode!);
            if (string.IsNullOrWhiteSpace(html))
            {
                return NotFound("E-invoice minute HTML not found");
            }

            return Content(html, "text/html; charset=utf-8");
        }

        [HttpPost("signature")]
        [AllowAnonymous]
        [EnableRateLimiting("public-einvoice-lookup")]
        public async Task<IActionResult> SaveBuyerSignature(
            [FromBody] EInvoiceMinuteSignRequest? request,
            [FromQuery] string? companyCd = null,
            [FromQuery] string? taxCode = null,
            [FromQuery] string? mtracuu = null,
            [FromQuery] string? mst = null,
            [FromQuery] string? buyerTaxCode = null,
            [FromQuery] string? sellerTaxCode = null,
            [FromQuery] string? code = null,
            [FromQuery] string? lookupCode = null)
        {
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var requestTaxCode = ResolveTaxCode(companyCd, taxCode, mst, buyerTaxCode, sellerTaxCode);
            if (string.IsNullOrWhiteSpace(requestTaxCode))
            {
                return ValidationError(await FieldRequiredAsync("TAX_CD"));
            }

            var requestLookupCode = ResolveLookupCode(mtracuu, code, lookupCode);
            if (string.IsNullOrWhiteSpace(requestLookupCode))
            {
                return ValidationError(await FieldRequiredAsync("MTRACUU"));
            }

            var data = await _service.SavePublicBuyerSignatureAsync(
                requestTaxCode!,
                requestLookupCode!,
                request,
                HttpContext.RequestAborted);
            if (data == null)
            {
                LogLookupMiss(requestTaxCode!, requestLookupCode!, "signature");
                return NotFound("E-invoice minute not found");
            }

            _logger.LogInformation(
                "Public e-invoice minute buyer signature saved. TaxCode: {TaxCode}, LookupCode: {LookupCode}, IP: {IP}",
                MaskValue(requestTaxCode),
                MaskValue(requestLookupCode),
                GetRemoteIp());

            return Updated(data, "E-invoice minute buyer signed successfully");
        }

        private void LogLookupMiss(string taxCode, string lookupCode, string format)
        {
            _logger.LogWarning(
                "Public e-invoice minute lookup failed. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
                MaskValue(taxCode),
                format,
                MaskValue(lookupCode),
                GetRemoteIp());
        }

        private void SetCompanyContext(string? companyCd)
        {
            if (CompanyRouteContextMiddleware.IsValidCompanyCode(companyCd))
            {
                HttpContext.Items[CompanyRouteContextMiddleware.CompanyCdItemKey] = companyCd!.Trim();
            }
        }

        private string GetRemoteIp()
        {
            var ip = ClientPublicIpResolver.ResolveFromRequest(HttpContext);
            return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
        }

        private static string? ResolveTaxCode(
            string? companyCd,
            string? taxCode,
            string? mst,
            string? buyerTaxCode,
            string? sellerTaxCode)
        {
            return Common.NormalizeNullableText(taxCode)
                ?? Common.NormalizeNullableText(mst)
                ?? Common.NormalizeNullableText(sellerTaxCode)
                ?? Common.NormalizeNullableText(buyerTaxCode)
                ?? Common.NormalizeNullableText(companyCd);
        }

        private static string? ResolveLookupCode(string? mtracuu, string? code, string? lookupCode)
        {
            return Common.NormalizeNullableText(mtracuu)
                ?? Common.NormalizeNullableText(code)
                ?? Common.NormalizeNullableText(lookupCode);
        }

        private static string BuildFileName(EInvoiceMinutePublicDownloadInfo minute, string extension)
        {
            var parts = new[]
            {
                "EInvoiceMinute",
                minute.SBBAN,
                minute.MTRACUU,
            }.Where(x => !string.IsNullOrWhiteSpace(x));

            return $"{SafeFilePart(string.Join("_", parts))}.{extension}";
        }

        private static string SafeFilePart(string value)
        {
            var safeValue = Regex.Replace(value, @"[^A-Za-z0-9._-]+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(safeValue) ? "EInvoiceMinute" : safeValue;
        }

        private static string MaskValue(string? value)
        {
            var text = Common.NormalizeNullableText(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            if (text.Length <= 6)
            {
                return new string('*', text.Length);
            }

            return $"{text[..4]}***{text[^2..]}";
        }
    }
}

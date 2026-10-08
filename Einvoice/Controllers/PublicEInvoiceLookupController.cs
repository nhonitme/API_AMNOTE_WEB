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
    [Route("api/public/einvoice/lookup")]
    public class PublicEInvoiceLookupController : BaseApiController
    {
        private readonly IEInvoiceService _service;
        private readonly IEInvoicePrintService _printService;
        private readonly ILogger<PublicEInvoiceLookupController> _logger;

        public PublicEInvoiceLookupController(
            IEInvoiceService service,
            IEInvoicePrintService printService,
            ILogger<PublicEInvoiceLookupController> logger)
        {
            _service = service;
            _printService = printService;
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
            var requestTaxCode = Common.NormalizeNullableText(taxCode)
                ?? Common.NormalizeNullableText(mst)
                ?? Common.NormalizeNullableText(sellerTaxCode)
                ?? Common.NormalizeNullableText(buyerTaxCode)
                ?? Common.NormalizeNullableText(companyCd);
            if (string.IsNullOrWhiteSpace(requestTaxCode))
            {
                return ValidationError(await FieldRequiredAsync("TAX_CD"));
            }

            var requestLookupCode = Common.NormalizeNullableText(mtracuu)
                ?? Common.NormalizeNullableText(code)
                ?? Common.NormalizeNullableText(lookupCode);
            if (string.IsNullOrWhiteSpace(requestLookupCode))
            {
                return ValidationError(await FieldRequiredAsync("MTRACUU"));
            }

            var requestFormat = (Common.NormalizeNullableText(format) ?? "json").ToLowerInvariant();
            if (requestFormat is not ("json" or "pdf" or "xml"))
            {
                return ValidationError("format must be json, pdf, or xml");
            }

            _logger.LogInformation(
                "Public e-invoice lookup requested. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
                MaskValue(requestTaxCode),
                requestFormat,
                MaskValue(requestLookupCode),
                GetRemoteIp());

            if (requestFormat == "json")
            {
                var data = await _service.LookupPublicAsync(requestTaxCode!, requestLookupCode!);
                if (data == null)
                {
                    LogLookupMiss(requestTaxCode!, requestLookupCode!, requestFormat);
                    return NotFound("E-invoice not found");
                }

                _logger.LogInformation(
                    "Public e-invoice lookup succeeded. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
                    MaskValue(requestTaxCode),
                    requestFormat,
                    MaskValue(requestLookupCode),
                    GetRemoteIp());
                return Success(data);
            }

            var downloadInfo = await _service.GetPublicDownloadInfoAsync(requestTaxCode!, requestLookupCode!);
            if (downloadInfo == null)
            {
                LogLookupMiss(requestTaxCode!, requestLookupCode!, requestFormat);
                return NotFound("E-invoice not found");
            }

            SetCompanyContext(downloadInfo.COMPANY_CD);

            if (requestFormat == "xml")
            {
                var xml = await _service.GetPublicXmlAsync(requestTaxCode!, requestLookupCode!);
                if (string.IsNullOrWhiteSpace(xml))
                {
                    return NotFound("E-invoice XML not found");
                }

                _logger.LogInformation(
                    "Public e-invoice XML downloaded. Company: {CompanyCd}, TaxCode: {TaxCode}, LookupCode: {LookupCode}, IP: {IP}",
                    downloadInfo.COMPANY_CD,
                    MaskValue(requestTaxCode),
                    MaskValue(requestLookupCode),
                    GetRemoteIp());
                return File(Encoding.UTF8.GetBytes(xml), "application/xml", BuildFileName(downloadInfo, "xml"));
            }

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _printService.ExportPdfAsync(
                    downloadInfo.COMPANY_CD!,
                    downloadInfo.INVOICE_ID,
                    null,
                    HttpContext.RequestAborted);

                _logger.LogInformation(
                    "Public e-invoice PDF downloaded. Company: {CompanyCd}, TaxCode: {TaxCode}, LookupCode: {LookupCode}, IP: {IP}",
                    downloadInfo.COMPANY_CD,
                    MaskValue(requestTaxCode),
                    MaskValue(requestLookupCode),
                    GetRemoteIp());
                return File(pdf, "application/pdf", BuildFileName(downloadInfo, "pdf"));
            }, notFoundMessage: "E-invoice not found");
        }

        private void LogLookupMiss(string taxCode, string lookupCode, string format)
        {
            _logger.LogWarning(
                "Public e-invoice lookup failed. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
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

        private static string BuildFileName(EInvoicePublicDownloadInfo invoice, string extension)
        {
            var parts = new[]
            {
                "EInvoice",
                invoice.KHHDON,
                invoice.SHDON,
                invoice.MTRACUU,
            }.Where(x => !string.IsNullOrWhiteSpace(x));

            return $"{SafeFilePart(string.Join("_", parts))}.{extension}";
        }

        private static string SafeFilePart(string value)
        {
            var safeValue = Regex.Replace(value, @"[^A-Za-z0-9._-]+", "_").Trim('_');
            return string.IsNullOrWhiteSpace(safeValue) ? "EInvoice" : safeValue;
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

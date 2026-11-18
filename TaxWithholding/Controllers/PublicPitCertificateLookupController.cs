using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Middleware;
using API_AMNOTE_WEB.Services;
using API_AMNOTE_WEB.TaxWithholding;
using API_AMNOTE_WEB.TaxWithholding.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Controllers;

[ApiController]
[Route("api/public/pit/certificate/lookup")]
public class PublicPitCertificateLookupController : BaseApiController
{
    private readonly IPitCertificateService _certificates;
    private readonly IPitService _pitService;
    private readonly ILogger<PublicPitCertificateLookupController> _logger;

    public PublicPitCertificateLookupController(
        IPitCertificateService certificates,
        IPitService pitService,
        ILogger<PublicPitCertificateLookupController> logger)
    {
        _certificates = certificates;
        _pitService = pitService;
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
            return ValidationError(await FieldRequiredAsync("TAX_CD"));

        var requestLookupCode = Common.NormalizeNullableText(mtracuu)
            ?? Common.NormalizeNullableText(code)
            ?? Common.NormalizeNullableText(lookupCode);
        if (string.IsNullOrWhiteSpace(requestLookupCode))
            return ValidationError(await FieldRequiredAsync("MTRACUU"));

        var requestFormat = (Common.NormalizeNullableText(format) ?? "json").ToLowerInvariant();
        if (requestFormat is not ("json" or "pdf" or "xml"))
            return ValidationError("format must be json, pdf, or xml");

        _logger.LogInformation(
            "Public PIT certificate lookup requested. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
            MaskValue(requestTaxCode),
            requestFormat,
            MaskValue(requestLookupCode),
            GetRemoteIp());

        if (requestFormat == "json")
        {
            var data = await _certificates.LookupPublicAsync(requestTaxCode!, requestLookupCode!);
            if (data == null)
            {
                LogLookupMiss(requestTaxCode!, requestLookupCode!, requestFormat);
                return NotFound("PIT certificate not found");
            }

            _logger.LogInformation(
                "Public PIT certificate lookup succeeded. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
                MaskValue(requestTaxCode),
                requestFormat,
                MaskValue(requestLookupCode),
                GetRemoteIp());
            return Success(data);
        }

        var downloadInfo = await _certificates.GetPublicDownloadInfoAsync(requestTaxCode!, requestLookupCode!);
        if (downloadInfo == null)
        {
            LogLookupMiss(requestTaxCode!, requestLookupCode!, requestFormat);
            return NotFound("PIT certificate not found");
        }

        SetCompanyContext(downloadInfo.COMPANY_CD);

        if (requestFormat == "xml")
        {
            var xml = await _certificates.GetPublicXmlAsync(requestTaxCode!, requestLookupCode!);
            if (string.IsNullOrWhiteSpace(xml))
                return NotFound("PIT certificate XML not found");

            _logger.LogInformation(
                "Public PIT certificate XML downloaded. Company: {CompanyCd}, TaxCode: {TaxCode}, LookupCode: {LookupCode}, IP: {IP}",
                downloadInfo.COMPANY_CD,
                MaskValue(requestTaxCode),
                MaskValue(requestLookupCode),
                GetRemoteIp());
            return File(Encoding.UTF8.GetBytes(xml), "application/xml", BuildFileName(downloadInfo, "xml"));
        }

        return await FromPreviewAsync(async () =>
        {
            var pdf = await _pitService.PrintPdf(
                downloadInfo.COMPANY_CD!,
                PitKinds.Certificate,
                downloadInfo.CTU_ID,
                HttpContext.RequestAborted);

            _logger.LogInformation(
                "Public PIT certificate PDF downloaded. Company: {CompanyCd}, TaxCode: {TaxCode}, LookupCode: {LookupCode}, IP: {IP}",
                downloadInfo.COMPANY_CD,
                MaskValue(requestTaxCode),
                MaskValue(requestLookupCode),
                GetRemoteIp());
            Response.Headers.CacheControl = "private, no-store";
            return File(pdf, "application/pdf");
        }, notFoundMessage: "PIT certificate not found");
    }

    private void LogLookupMiss(string taxCode, string lookupCode, string format)
    {
        _logger.LogWarning(
            "Public PIT certificate lookup failed. TaxCode: {TaxCode}, Format: {Format}, LookupCode: {LookupCode}, IP: {IP}",
            MaskValue(taxCode),
            format,
            MaskValue(lookupCode),
            GetRemoteIp());
    }

    private void SetCompanyContext(string? companyCd)
    {
        if (CompanyRouteContextMiddleware.IsValidCompanyCode(companyCd))
            HttpContext.Items[CompanyRouteContextMiddleware.CompanyCdItemKey] = companyCd!.Trim();
    }

    private string GetRemoteIp()
    {
        var ip = ClientPublicIpResolver.ResolveFromRequest(HttpContext);
        return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
    }

    private static string BuildFileName(PitCertificatePublicDownloadInfo certificate, string extension)
    {
        var parts = new[]
        {
            "PitCertificate",
            certificate.KHCTU,
            certificate.SCTU?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            certificate.MTRACUU,
        }.Where(x => !string.IsNullOrWhiteSpace(x));

        return $"{SafeFilePart(string.Join("_", parts))}.{extension}";
    }

    private static string SafeFilePart(string value)
    {
        var safeValue = Regex.Replace(value, @"[^A-Za-z0-9._-]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(safeValue) ? "PitCertificate" : safeValue;
    }

    private static string MaskValue(string? value)
    {
        var text = Common.NormalizeNullableText(value);
        if (string.IsNullOrWhiteSpace(text)) return "";
        if (text.Length <= 6) return new string('*', text.Length);
        return $"{text[..4]}***{text[^2..]}";
    }
}

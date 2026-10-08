using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Services;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitXslService(
    PitXslTemplateRepository repository,
    PitIncomePayerRepository incomePayerRepository,
    ICompanyInfoRepository companyInfoRepository,
    IEInvoiceHtmlToPdfService htmlToPdfService,
    IWebHostEnvironment environment,
    ILogger<PitXslService> logger)
{
    public const string DefaultFtpFileName = "Mau03-TNCN.xsl";
    private static readonly Regex SeriesPattern = new(@"^CT\d{2}[A-Z]{2}$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<PitXslTemplate>> ListAsync(string companyCd, string? templateCd = null)
    {
        templateCd = string.IsNullOrWhiteSpace(templateCd) ? PitXslCodes.Certificate : templateCd.Trim();
        var rows = await repository.ListAsync(companyCd, templateCd);
        return rows.Select(row =>
        {
            row.XSL_CONTENT = null;
            return row;
        }).ToList();
    }

    public async Task<IReadOnlyList<PitXslTemplate>> ListActiveCatalogAsync(string companyCd)
    {
        var rows = await ListAsync(companyCd, PitXslCodes.Certificate);
        return rows.Where(r => r.IS_ACTIVE == 1 && !string.IsNullOrWhiteSpace(r.SERIES)).ToList();
    }

    public async Task<PitXslTemplate> GetAsync(string companyCd, long xslId)
    {
        var row = await repository.GetAsync(companyCd, xslId)
            ?? throw new KeyNotFoundException("Không tìm thấy mẫu số ký hiệu chứng từ TNCN.");
        row.HAS_XSL_CONTENT = string.IsNullOrWhiteSpace(row.XSL_CONTENT) ? 0 : 1;
        return row;
    }

    public async Task<PitXslTemplate> RequireActiveCatalogAsync(string companyCd, long xslId)
    {
        var row = await GetAsync(companyCd, xslId);
        if (row.IS_ACTIVE != 1)
            throw new ArgumentException("Mẫu số ký hiệu đã ngừng sử dụng.");
        if (string.IsNullOrWhiteSpace(row.SERIES) || !SeriesPattern.IsMatch(row.SERIES.Trim().ToUpperInvariant()))
            throw new ArgumentException("Mẫu số ký hiệu chưa có ký hiệu hợp lệ (ví dụ CT26AA).");
        return row;
    }

    public async Task<PitXslTemplate> SaveAsync(string companyCd, string userId, long xslId, PitXslTemplateSaveRequest request)
    {
        request.TEMPLATE_CD = string.IsNullOrWhiteSpace(request.TEMPLATE_CD) ? PitXslCodes.Certificate : request.TEMPLATE_CD.Trim();
        if (request.TEMPLATE_CD != PitXslCodes.Certificate)
            throw new ArgumentException("Chỉ hỗ trợ mẫu 03/TNCN.");

        var series = (request.SERIES ?? "").Trim().ToUpperInvariant();
        if (!SeriesPattern.IsMatch(series))
            throw new ArgumentException("Ký hiệu phải là CT + hai số năm + hai chữ in hoa (ví dụ CT26AA).");
        request.SERIES = series;

        var from = request.FROM_DOC_NO is > 0 ? request.FROM_DOC_NO.Value : 1;
        if (from > 99999999) throw new ArgumentException("Từ số không hợp lệ.");
        request.FROM_DOC_NO = from;
        if (request.TO_DOC_NO is > 0 && request.TO_DOC_NO < from)
            throw new ArgumentException("Đến số phải lớn hơn hoặc bằng từ số.");
        if (request.TO_DOC_NO is <= 0) request.TO_DOC_NO = null;

        if (string.IsNullOrWhiteSpace(request.TEMPLATE_NM))
            request.TEMPLATE_NM = $"Chứng từ khấu trừ TNCN · {series}";

        if (xslId <= 0 && string.IsNullOrWhiteSpace(request.XSL_CONTENT))
            request.XSL_CONTENT = await ReadDefaultSampleAsync();

        if (!string.IsNullOrWhiteSpace(request.XSL_CONTENT)
            && !EInvoiceFtpClient.LooksLikeXmlOrXsl(request.XSL_CONTENT, out var reason))
            throw new ArgumentException(reason ?? "XSL content không hợp lệ.");

        request.LOGO_PATH = RestrictPitImagePath("logo", companyCd, request.LOGO_PATH);
        request.BACKGROUND_PATH = RestrictPitImagePath("background", companyCd, request.BACKGROUND_PATH);
        request.NEN_PATH = RestrictPitImagePath("invoice-background", companyCd, request.NEN_PATH);

        var savedId = await repository.SaveAsync(companyCd, userId, xslId, request);
        return await GetAsync(companyCd, savedId);
    }

    public Task DeleteAsync(string companyCd, string userId, long xslId)
        => repository.DeleteAsync(companyCd, userId, xslId);

    public async Task<PitXslImageResult> UploadImageAsync(
        string companyCd,
        string userId,
        long xslId,
        string imageKind,
        string originalFileName,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        if (bytes.Length == 0) throw new ArgumentException("file is required");
        if (bytes.Length > 10 * 1024 * 1024) throw new ArgumentException("Image file must not exceed 10 MB");
        if (!EInvoiceFtpClient.IsConfigured()) throw new InvalidOperationException("E-invoice FTP is not configured");

        var kind = NormalizePitImageKind(imageKind);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".webp"))
            throw new ArgumentException("Only PNG, JPG, GIF and WEBP image files are supported");

        _ = await GetAsync(companyCd, xslId);
        if (!EInvoiceFtpClient.TrySanitizeCompanySegment(companyCd, out var company))
            throw new ArgumentException("Company code is required");
        var ftpKind = kind == "nen" ? "invoice-background" : kind;
        var remoteDirectory = EInvoiceFtpClient.ResolveCompanyImageRemoteDirectory(ftpKind, companyCd);
        var remotePath = $"{remoteDirectory}/{company}_pit_{xslId}_{kind}_{Guid.NewGuid():N}{extension}";
        await Task.Run(() =>
        {
            EInvoiceFtpClient.EnsureRemoteDirectoryExists(remoteDirectory);
            EInvoiceFtpClient.UploadBytes(remotePath, bytes);
        }, cancellationToken);
        EInvoicePrintImageHelper.SaveCachedImage(remotePath, bytes, environment);
        await repository.SetImageAsync(companyCd, userId, xslId, kind, remotePath);
        return new PitXslImageResult
        {
            PATH = remotePath,
            FILE_NAME = Path.GetFileName(remotePath),
            IMAGE_KIND = kind,
            XSL_ID = xslId
        };
    }

    public async Task<PitXslImageResult> SelectImageAsync(
        string companyCd,
        string userId,
        long xslId,
        string imageKind,
        string? fileName,
        string? path)
    {
        if (!EInvoiceFtpClient.IsConfigured()) throw new InvalidOperationException("E-invoice FTP is not configured");
        var kind = NormalizePitImageKind(imageKind);
        var ftpKind = kind == "nen" ? "invoice-background" : kind;
        if (!EInvoiceFtpClient.TryResolveAccessibleImagePath(ftpKind, companyCd, fileName, path, out var remotePath))
            throw new ArgumentException("FILE_NAME is required");
        if (!EInvoiceFtpClient.RemoteFileExists(remotePath))
            throw new KeyNotFoundException("Image file not found");

        _ = await GetAsync(companyCd, xslId);
        await repository.SetImageAsync(companyCd, userId, xslId, kind, remotePath);
        return new PitXslImageResult
        {
            PATH = remotePath,
            FILE_NAME = Path.GetFileName(remotePath),
            IMAGE_KIND = kind,
            XSL_ID = xslId
        };
    }

    public async Task<string> PreviewHtmlAsync(string companyCd, long xslId)
    {
        var template = await GetAsync(companyCd, xslId);
        if (string.IsNullOrWhiteSpace(template.XSL_CONTENT))
            template.XSL_CONTENT = await ReadDefaultSampleAsync();

        var incomePayer = await incomePayerRepository.GetAsync(companyCd);
        if (incomePayer == null)
        {
            var company = await companyInfoRepository.GetCompanyInfoAsync(companyCd);
            incomePayer = new PitIncomePayer
            {
                PAYER_NM = company?.COMPANY_NM ?? companyCd,
                TAX_CD = company?.TAX_CD ?? "",
                ADDRESS = company?.ADDRESS ?? "",
                PHONE = company?.TEL,
                EMAIL = company?.EMAIL
            };
        }
        var xml = BuildPreviewXml(template, incomePayer);
        return Transform(companyCd, xml, template);
    }

    public async Task<byte[]> PreviewPdfAsync(
        string companyCd,
        long xslId,
        CancellationToken cancellationToken = default)
    {
        var html = await PreviewHtmlAsync(companyCd, xslId);
        return await htmlToPdfService.ConvertAsync(html, cancellationToken);
    }

    public async Task<PitXslTemplate> ResolveForPrintAsync(string companyCd, long? xslId)
    {
        if (xslId is > 0)
        {
            var byId = await repository.GetAsync(companyCd, xslId.Value);
            if (byId != null && byId.IS_ACTIVE == 1)
            {
                if (string.IsNullOrWhiteSpace(byId.XSL_CONTENT))
                    byId.XSL_CONTENT = await ReadDefaultSampleAsync();
                return byId;
            }
        }

        var list = await repository.ListAsync(companyCd, PitXslCodes.Certificate);
        var chosen = list.FirstOrDefault(x => x.IS_DEFAULT == 1 && x.IS_ACTIVE == 1)
            ?? list.FirstOrDefault(x => x.IS_ACTIVE == 1)
            ?? throw new InvalidOperationException("Chưa cấu hình mẫu số ký hiệu 03/TNCN. Vào Quản lý mẫu số ký hiệu chứng từ TNCN để thêm.");

        var full = await repository.GetAsync(companyCd, chosen.XSL_ID)
            ?? throw new InvalidOperationException("Không tải được mẫu số ký hiệu 03/TNCN.");
        if (string.IsNullOrWhiteSpace(full.XSL_CONTENT))
            full.XSL_CONTENT = await ReadDefaultSampleAsync();
        return full;
    }

    private static string BuildPreviewXml(PitXslTemplate template, PitIncomePayer incomePayer)
    {
        var documentDate = DateTime.UtcNow
            .AddHours(7)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var document = new XDocument(
            new XElement("CTu",
                new XElement("DLCTu",
                    new XAttribute("Id", $"PIT-PREVIEW-{documentDate.Replace("-", "")}"),
                    new XElement("TTChung",
                        new XElement("PBan", "2.1.1"),
                        new XElement("TCTu", "Chứng từ khấu trừ thuế thu nhập cá nhân"),
                        new XElement("MSCTu", PitXslCodes.Certificate),
                        new XElement("KHCTu", template.SERIES ?? ""),
                        new XElement("SCTu", "0"),
                        new XElement("NLap", documentDate)),
                    new XElement("NDCTu",
                        new XElement("TCTTNhap",
                            new XElement("Ten", incomePayer.PAYER_NM),
                            new XElement("MST", incomePayer.TAX_CD),
                            new XElement("DChi", incomePayer.ADDRESS),
                            string.IsNullOrWhiteSpace(incomePayer.PHONE)
                                ? null
                                : new XElement("SDThoai", incomePayer.PHONE),
                            string.IsNullOrWhiteSpace(incomePayer.EMAIL)
                                ? null
                                : new XElement("DCTDTu", incomePayer.EMAIL)))),
                new XElement("DSCKS",
                    new XElement("TCTTNhap"))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    public string Transform(string companyCd, string xml, PitXslTemplate template)
    {
        var logo = EInvoicePrintImageHelper.ResolveTransformImage(template.XSL_CONTENT, template.LOGO_PATH, "logoImage", environment, logger);
        var background = EInvoicePrintImageHelper.ResolveTransformImage(template.XSL_CONTENT, template.BACKGROUND_PATH, "backgroundImage", environment, logger);
        var nen = EInvoicePrintImageHelper.ResolveTransformImage(template.XSL_CONTENT, template.NEN_PATH, "nenImage", environment, logger);
        return EInvoiceXmlHtmlTransformService.Transform(
            xml,
            template.XSL_CONTENT!,
            logoImage: logo,
            backgroundImage: background,
            nenImage: nen,
            lookupCompanyCd: companyCd);
    }

    private async Task<string> ReadDefaultSampleAsync()
    {
        if (!EInvoiceFtpClient.IsConfigured())
            throw new InvalidOperationException("E-invoice FTP is not configured");
        if (!EInvoiceFtpClient.TryBuildPitXslPath(DefaultFtpFileName, out var remotePath))
            throw new InvalidOperationException("Default PIT XSL FTP path is invalid");

        string content;
        try
        {
            content = await Task.Run(() => EInvoiceFtpClient.DownloadText(remotePath));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Default PIT XSL sample not found on FTP: {remotePath}", ex);
        }

        if (!EInvoiceFtpClient.LooksLikeXmlOrXsl(content, out var reason))
            throw new InvalidOperationException(reason ?? "Default PIT XSL sample is invalid");
        return content;
    }

    private static string? RestrictPitImagePath(string imageKind, string companyCd, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return EInvoiceFtpClient.TryResolveAccessibleImagePath(imageKind, companyCd, null, path, out var remotePath)
            ? remotePath
            : null;
    }

    private static string NormalizePitImageKind(string? imageKind)
    {
        var kind = (imageKind ?? "").Trim().ToLowerInvariant();
        return kind switch
        {
            "logo" => "logo",
            "background" => "background",
            "nen" or "invoice-background" => "nen",
            _ => throw new ArgumentException("Unsupported image kind")
        };
    }
}

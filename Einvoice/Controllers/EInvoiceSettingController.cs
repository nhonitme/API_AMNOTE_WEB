using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EInvoiceSettingController : BaseApiController
    {
        private readonly IEInvoiceSettingService _service;
        private readonly IEInvoiceSellerPreviewService _previewService;
        private readonly IMailSettingService _mailSettingService;

        public EInvoiceSettingController(
            IEInvoiceSettingService service,
            IEInvoiceSellerPreviewService previewService,
            IMailSettingService mailSettingService)
        {
            _service = service;
            _previewService = previewService;
            _mailSettingService = mailSettingService;
        }

        [HttpGet("templates/{xslId:long}/designer/designs")]
        public async Task<IActionResult> GetTemplateDesignerDesigns([FromRoute] long xslId)
        {
            if (xslId <= 0) return ValidationError(await FieldRequiredAsync("XSL_ID"));
            return await FromSettingAsync(async () =>
            {
                var data = await _service.GetTemplateDesignerDesignsAsync(Common.GetCompanyCode(), xslId);
                return Success(data);
            });
        }

        [HttpPost("templates/{xslId:long}/designer/designs/{designId:long}/clone")]
        public async Task<IActionResult> CloneTemplateDesigner([FromRoute] long xslId, [FromRoute] long designId)
        {
            if (xslId <= 0 || designId <= 0) return ValidationError(await FieldRequiredAsync(xslId <= 0 ? "XSL_ID" : "DESIGN_ID"));
            return await FromSettingAsync(async () =>
            {
                var data = await _service.CloneTemplateDesignerAsync(Common.GetCompanyCode(), Common.GetUserId(), xslId, designId);
                return Created(data, "Invoice template design cloned");
            });
        }

        [HttpPut("templates/{xslId:long}/designer/draft")]
        public async Task<IActionResult> SaveTemplateDesignerDraft([FromRoute] long xslId, [FromBody] EInvoiceTemplateDesignerDraftSaveRequest? request)
        {
            if (xslId <= 0) return ValidationError(await FieldRequiredAsync("XSL_ID"));
            if (request == null || request.DESIGN_ID <= 0 || string.IsNullOrWhiteSpace(request.XSL_CONTENT))
                return ValidationError(await FieldRequiredAsync(request?.DESIGN_ID <= 0 ? "DESIGN_ID" : "XSL_CONTENT"));

            return await FromSettingAsync(async () =>
            {
                var data = await _service.SaveTemplateDesignerDraftAsync(Common.GetCompanyCode(), Common.GetUserId(), xslId, request);
                return Updated(data, "Invoice template design XSLT saved successfully");
            });
        }

        [HttpPost("templates/{xslId:long}/designer/designs/{designId:long}/publish")]
        public async Task<IActionResult> PublishTemplateDesigner([FromRoute] long xslId, [FromRoute] long designId)
        {
            if (xslId <= 0 || designId <= 0) return ValidationError(await FieldRequiredAsync(xslId <= 0 ? "XSL_ID" : "DESIGN_ID"));
            return await FromSettingAsync(async () =>
            {
                var data = await _service.PublishTemplateDesignerAsync(Common.GetCompanyCode(), Common.GetUserId(), xslId, designId);
                return Updated(data, "Invoice template content saved");
            });
        }

        [HttpPost("templates/{xslId:long}/designer/images/{imageKind}")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadTemplateDesignerImage(
            [FromRoute] long xslId,
            [FromRoute] string imageKind,
            [FromQuery] long designId,
            IFormFile? file)
        {
            if (xslId <= 0) return ValidationError(await FieldRequiredAsync("XSL_ID"));
            if (designId <= 0) return ValidationError(await FieldRequiredAsync("DESIGN_ID"));
            if (file == null || file.Length == 0) return ValidationError(await FieldRequiredAsync("file"));

            return await FromSettingAsync(async () =>
            {
                await using var input = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, HttpContext.RequestAborted);
                var data = await _service.UploadTemplateDesignerImageAsync(
                    Common.GetCompanyCode(),
                    Common.GetUserId(),
                    xslId,
                    designId,
                    imageKind,
                    file.FileName,
                    buffer.ToArray(),
                    HttpContext.RequestAborted);
                return Success(data);
            });
        }

        [HttpGet("designer/images/{imageKind}")]
        public async Task<IActionResult> ListTemplateDesignerImages([FromRoute] string imageKind)
        {
            return await FromSettingAsync(async () =>
            {
                var files = await _service.ListTemplateDesignerImagesAsync(Common.GetCompanyCode(), imageKind, HttpContext.RequestAborted);
                return Success(files);
            });
        }

        [HttpGet("designer/xsl-samples")]
        public async Task<IActionResult> ListTemplateDesignerXslSamples()
        {
            return await FromSettingAsync(async () =>
            {
                var files = await _service.ListTemplateDesignerXslSamplesAsync(HttpContext.RequestAborted);
                return Success(files);
            });
        }

        [HttpGet("designer/xsl-samples/file")]
        public async Task<IActionResult> GetTemplateDesignerXslSampleFile([FromQuery] string? fileName)
        {
            return await FromSettingAsync(async () =>
            {
                var data = await _service.GetTemplateDesignerXslSampleFileAsync(fileName, HttpContext.RequestAborted);
                return Success(data);
            });
        }

        [HttpPost("templates")]
        public async Task<IActionResult> CreateSellerXslTemplate([FromBody] EInvoiceSellerXslTemplateMetaSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            return await FromSettingAsync(async () =>
            {
                var data = await _service.CreateSellerXslTemplateAsync(
                    Common.GetCompanyCode(),
                    Common.GetUserId(),
                    request,
                    HttpContext.RequestAborted);
                return Created(data, "Invoice template created successfully");
            });
        }

        [HttpPut("templates/{xslId:long}")]
        public async Task<IActionResult> UpdateSellerXslTemplate([FromRoute] long xslId, [FromBody] EInvoiceSellerXslTemplateMetaSaveRequest? request)
        {
            if (xslId <= 0)
                return ValidationError(await FieldRequiredAsync("XSL_ID"));
            if (request == null)
                return ValidationError("Request body must be provided");

            return await FromSettingAsync(async () =>
            {
                var data = await _service.UpdateSellerXslTemplateAsync(
                    Common.GetCompanyCode(),
                    Common.GetUserId(),
                    xslId,
                    request,
                    HttpContext.RequestAborted);
                return Updated(data, "Invoice template updated successfully");
            });
        }

        [HttpPost("templates/bulk-delete")]
        public async Task<IActionResult> DeleteSellerXslTemplates([FromBody] EInvoiceSellerXslTemplateDeleteManyRequest? request)
        {
            if (request?.XslIds == null || request.XslIds.Count == 0)
                return ValidationError(await FieldRequiredAsync("XslIds"));

            return await FromSettingAsync(async () =>
            {
                var deleted = await _service.DeleteSellerXslTemplatesAsync(Common.GetCompanyCode(), Common.GetUserId(), request.XslIds);
                return Success(new { deleted }, "Invoice templates deleted successfully");
            });
        }

        [HttpGet("designer/images/{imageKind}/file")]
        public async Task<IActionResult> GetTemplateDesignerImageFile(
            [FromRoute] string imageKind,
            [FromQuery] string? fileName,
            [FromQuery] string? path)
        {
            return await FromSettingAsync(async () =>
            {
                var (bytes, contentType) = await _service.GetTemplateDesignerImageFileAsync(
                    Common.GetCompanyCode(),
                    imageKind,
                    fileName,
                    path,
                    HttpContext.RequestAborted);
                return File(bytes, contentType);
            });
        }

        [HttpPut("templates/{xslId:long}/designer/images/{imageKind}")]
        public async Task<IActionResult> SelectTemplateDesignerImage(
            [FromRoute] long xslId,
            [FromRoute] string imageKind,
            [FromQuery] long designId,
            [FromBody] EInvoiceFtpImageSelectRequest? request)
        {
            if (xslId <= 0) return ValidationError(await FieldRequiredAsync("XSL_ID"));
            if (designId <= 0) return ValidationError(await FieldRequiredAsync("DESIGN_ID"));

            return await FromSettingAsync(async () =>
            {
                var data = await _service.SelectTemplateDesignerImageAsync(
                    Common.GetCompanyCode(),
                    Common.GetUserId(),
                    xslId,
                    designId,
                    imageKind,
                    request?.FILE_NAME,
                    request?.PATH);
                return Success(data);
            });
        }

        [HttpPut("sellers/{id:long}")]
        public async Task<IActionResult> UpdateSellerInfo([FromRoute] long id, [FromBody] EInvoiceSellerInfoSaveRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));
            if (request == null)
                return ValidationError("Request body must be provided");

            var companyCd = Common.GetCompanyCode();
            if (string.IsNullOrWhiteSpace(companyCd))
                return ValidationError(await FieldRequiredAsync("COMPANY_CD"));

            return await FromSettingAsync(async () =>
            {
                var data = await _service.UpdateSellerInfoAsync(companyCd, Common.GetUserId(), id, request);
                return Updated(data, "E-invoice seller updated successfully");
            });
        }

        [HttpGet("sellers/{id:long}/preview/html")]
        public async Task<IActionResult> GetSellerPreviewHtml([FromRoute] long id, [FromQuery] long? xslId = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));

            return await FromPreviewAsync(async () =>
            {
                var html = await _previewService.GetHtmlAsync(Common.GetCompanyCode(), id, xslId, HttpContext.RequestAborted);
                return Content(html, "text/html; charset=utf-8");
            });
        }

        [HttpPost("sellers/{id:long}/decimal-preview/html")]
        public async Task<IActionResult> PostSellerDecimalPreviewHtml(
            [FromRoute] long id,
            [FromBody] EInvoiceSellerDecimalPreviewRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));

            return await FromPreviewAsync(async () =>
            {
                var html = await _previewService.GetDecimalDemoHtmlAsync(
                    Common.GetCompanyCode(),
                    id,
                    request ?? new EInvoiceSellerDecimalPreviewRequest(),
                    HttpContext.RequestAborted);
                return Content(html, "text/html; charset=utf-8");
            });
        }

        [HttpGet("sellers/{id:long}/preview/pdf")]
        public async Task<IActionResult> GetSellerPreviewPdf([FromRoute] long id, [FromQuery] long? xslId = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _previewService.ExportPdfAsync(Common.GetCompanyCode(), id, xslId, HttpContext.RequestAborted);
                return File(pdf, "application/pdf", $"EInvoiceSeller_{id}.pdf");
            });
        }

        [HttpGet("sellers/{id:long}/designer-preview/html")]
        public async Task<IActionResult> GetSellerDesignerPreviewHtml(
            [FromRoute] long id,
            [FromQuery] long? xslId = null,
            [FromQuery] long? designId = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));

            return await FromPreviewAsync(async () =>
            {
                var html = await _previewService.GetDesignerHtmlAsync(Common.GetCompanyCode(), id, xslId, designId, HttpContext.RequestAborted);
                return Content(html, "text/html; charset=utf-8");
            });
        }

        [HttpGet("sellers/{id:long}/designer-preview/pdf")]
        public async Task<IActionResult> GetSellerDesignerPreviewPdf(
            [FromRoute] long id,
            [FromQuery] long? xslId = null,
            [FromQuery] long? designId = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _previewService.ExportDesignerPdfAsync(
                    Common.GetCompanyCode(),
                    id,
                    xslId,
                    designId,
                    cancellationToken: HttpContext.RequestAborted);
                return File(pdf, "application/pdf", $"EInvoiceDesigner_{id}.pdf");
            });
        }

        public sealed class EInvoiceDesignerLivePreviewRequest
        {
            public string? XslContent { get; set; }
            public long? XslId { get; set; }
            public long? DesignId { get; set; }
            public Dictionary<string, string>? ColumnParams { get; set; }
        }

        [HttpPost("sellers/{id:long}/designer-preview/html")]
        public async Task<IActionResult> PostSellerDesignerPreviewHtml(
            [FromRoute] long id,
            [FromBody] EInvoiceDesignerLivePreviewRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));
            if (string.IsNullOrWhiteSpace(request?.XslContent))
                return ValidationError(await FieldRequiredAsync("XSL_CONTENT"));

            return await FromPreviewAsync(async () =>
            {
                var html = await _previewService.GetDesignerLiveHtmlAsync(
                    Common.GetCompanyCode(),
                    id,
                    request!.XslContent!,
                    request.XslId,
                    request.DesignId,
                    request.ColumnParams,
                    HttpContext.RequestAborted);
                return Content(html, "text/html; charset=utf-8");
            });
        }

        [HttpPost("sellers/{id:long}/designer-preview/pdf")]
        public async Task<IActionResult> PostSellerDesignerPreviewPdf(
            [FromRoute] long id,
            [FromBody] EInvoiceDesignerLivePreviewRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));

            return await FromPreviewAsync(async () =>
            {
                var pdf = await _previewService.ExportDesignerPdfAsync(
                    Common.GetCompanyCode(),
                    id,
                    request?.XslId,
                    request?.DesignId,
                    request?.XslContent,
                    request?.ColumnParams,
                    HttpContext.RequestAborted);
                return File(pdf, "application/pdf", $"EInvoiceDesigner_{id}.pdf");
            });
        }

        [HttpGet("sellers/{id:long}/preview")]
        public async Task<IActionResult> GetSellerPreview([FromRoute] long id, [FromQuery] long? xslId = null)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SELLER_ID"));

            return await FromPreviewAsync(async () =>
            {
                var data = await _previewService.GetPreviewAsync(Common.GetCompanyCode(), id, xslId);
                return Success(data);
            });
        }

        [HttpGet("decimal-settings")]
        public async Task<IActionResult> SearchDecimalSettings(
            [FromQuery] long? settingId = null,
            [FromQuery] long? xslId = null,
            [FromQuery] string? applyTarget = null,
            [FromQuery] string? fieldScope = null,
            [FromQuery] string? keyword = null,
            [FromQuery] bool includeInactive = true)
        {
            var data = await _service.SearchDecimalSettingsAsync(Common.GetCompanyCode(), new EInvoiceDecimalSettingSearchRequest
            {
                SettingId = settingId,
                XslId = xslId,
                ApplyTarget = applyTarget,
                FieldScope = fieldScope,
                Keyword = keyword,
                IncludeInactive = includeInactive
            });
            return Success(data);
        }

        [HttpGet("decimal-settings/{id:long}")]
        public async Task<IActionResult> GetDecimalSettingById([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SETTING_ID"));

            var data = await _service.GetDecimalSettingByIdAsync(Common.GetCompanyCode(), id);
            if (data == null)
                return NotFound("E-invoice decimal setting not found");

            return Success(data);
        }

        [HttpPost("decimal-settings")]
        public async Task<IActionResult> SaveDecimalSetting([FromBody] EInvoiceDecimalSettingSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.SaveDecimalSettingAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return request.SETTING_ID > 0
                ? Updated(data, "E-invoice decimal setting updated successfully")
                : Created(data, "E-invoice decimal setting created successfully");
        }

        [HttpPut("decimal-settings/{id:long}")]
        public async Task<IActionResult> UpdateDecimalSetting([FromRoute] long id, [FromBody] EInvoiceDecimalSettingSaveRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SETTING_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            request.SETTING_ID = id;
            var data = await _service.SaveDecimalSettingAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Updated(data, "E-invoice decimal setting updated successfully");
        }

        [HttpGet("user-settings")]
        public async Task<IActionResult> SearchUserSettings(
            [FromQuery] long? settingId = null,
            [FromQuery] string? userId = null,
            [FromQuery] string? keyword = null,
            [FromQuery] bool includeDeleted = false)
        {
            var data = await _service.SearchUserSettingsAsync(Common.GetCompanyCode(), new EInvoiceUserSettingSearchRequest
            {
                SettingId = settingId,
                UserId = userId,
                Keyword = keyword,
                IncludeDeleted = includeDeleted
            });
            return Success(data);
        }

        [HttpGet("user-settings/{id:long}")]
        public async Task<IActionResult> GetUserSettingById([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SETTING_ID"));

            var data = await _service.GetUserSettingByIdAsync(Common.GetCompanyCode(), id);
            if (data == null)
                return NotFound("E-invoice user setting not found");

            return Success(data);
        }

        [HttpPost("user-settings")]
        public async Task<IActionResult> SaveUserSetting([FromBody] EInvoiceUserSettingSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var data = await _service.SaveUserSettingAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return request.SETTING_ID > 0
                ? Updated(data, "E-invoice user setting updated successfully")
                : Created(data, "E-invoice user setting created successfully");
        }

        [HttpPut("user-settings/{id:long}")]
        public async Task<IActionResult> UpdateUserSetting([FromRoute] long id, [FromBody] EInvoiceUserSettingSaveRequest? request)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SETTING_ID"));

            if (request == null)
                return ValidationError("Request body must be provided");

            request.SETTING_ID = id;
            var data = await _service.SaveUserSettingAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Updated(data, "E-invoice user setting updated successfully");
        }

        [HttpGet("admin-settings")]
        public async Task<IActionResult> SearchAdminSettings(
            [FromQuery] long? settingId = null,
            [FromQuery] string? settingType = null,
            [FromQuery] string? keyword = null,
            [FromQuery] bool includeDeleted = false)
        {
            var data = await _service.SearchAdminSettingsAsync(Common.GetCompanyCode(), new EInvoiceAdminSettingSearchRequest
            {
                SettingId = settingId,
                SettingType = settingType,
                Keyword = keyword,
                IncludeDeleted = includeDeleted
            });
            return Success(data);
        }

        [HttpGet("admin-settings/{id:long}")]
        public async Task<IActionResult> GetAdminSettingById([FromRoute] long id)
        {
            if (id <= 0)
                return ValidationError(await FieldRequiredAsync("SETTING_ID"));

            var data = await _service.GetAdminSettingByIdAsync(Common.GetCompanyCode(), id);
            if (data == null)
                return NotFound("E-invoice admin setting not found");

            return Success(data);
        }

        [HttpGet("mail-settings")]
        public async Task<IActionResult> GetMailSetting()
        {
            var companyCd = Common.GetCompanyCode();
            if (string.IsNullOrWhiteSpace(companyCd))
                return ValidationError(await FieldRequiredAsync("COMPANY_CD"));

            var data = await _mailSettingService.GetEInvoiceMailSettingAsync(companyCd);
            return Success(data);
        }

        [HttpPost("mail-settings")]
        public async Task<IActionResult> SaveMailSetting([FromBody] MailSettingSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var companyCd = Common.GetCompanyCode();
            if (string.IsNullOrWhiteSpace(companyCd))
                return ValidationError(await FieldRequiredAsync("COMPANY_CD"));

            var data = await _mailSettingService.SaveEInvoiceMailSettingAsync(companyCd, Common.GetUserId(), request);
            return request.MAIL_ID > 0
                ? Updated(data, "Mail setting updated successfully")
                : Created(data, "Mail setting created successfully");
        }

        [HttpPut("mail-settings")]
        public async Task<IActionResult> UpdateMailSetting([FromBody] MailSettingSaveRequest? request)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var companyCd = Common.GetCompanyCode();
            if (string.IsNullOrWhiteSpace(companyCd))
                return ValidationError(await FieldRequiredAsync("COMPANY_CD"));

            var data = await _mailSettingService.SaveEInvoiceMailSettingAsync(companyCd, Common.GetUserId(), request);
            return Updated(data, "Mail setting updated successfully");
        }

        [HttpPost("mail-settings/test")]
        public async Task<IActionResult> SendTestMailSetting([FromBody] MailSettingTestRequest? request, CancellationToken cancellationToken)
        {
            if (request == null)
                return ValidationError("Request body must be provided");

            var companyCd = Common.GetCompanyCode();
            if (string.IsNullOrWhiteSpace(companyCd))
                return ValidationError(await FieldRequiredAsync("COMPANY_CD"));

            if (string.IsNullOrWhiteSpace(request.TO_EMAIL))
                return ValidationError(await FieldRequiredAsync("TO_EMAIL"));

            await _mailSettingService.SendTestEInvoiceMailAsync(companyCd, request, cancellationToken);
            return Success(true, "Test mail sent successfully");
        }

        private async Task<IActionResult> FromSettingAsync(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

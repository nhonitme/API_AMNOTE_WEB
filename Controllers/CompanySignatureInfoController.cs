using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class CompanySignatureInfoController : BaseApiController
    {
        private readonly ICompanySignatureInfoService _service;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<CompanySignatureInfoController> _logger;

        public CompanySignatureInfoController(
            ICompanySignatureInfoService service,
            IWebHostEnvironment environment,
            ILogger<CompanySignatureInfoController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCompanySignatureInfos(
            [FromQuery(Name = "id")] long? id = null,
            [FromQuery(Name = "signCode")] string? signCode = null,
            [FromQuery(Name = "isActive")] bool? isActive = null)
        {
            var companyCd = Common.GetCompanyCode();
            var normalizedSignCode = Common.NormalizeNullableText(signCode);
            var activeFlag = isActive.HasValue ? (isActive.Value ? "1" : "0") : null;
            var data = (await _service.GetCompanySignatureInfosAsync(companyCd, id, normalizedSignCode, activeFlag))
                .Select(MapDto)
                .ToList();

            if ((id.HasValue && id.Value > 0) || !string.IsNullOrWhiteSpace(normalizedSignCode))
            {
                if (!data.Any())
                {
                    return NotFound("Signature not found");
                }
            }

            return Success(data);
        }

        [HttpGet("check-exists")]
        [Authorize]
        public async Task<IActionResult> CheckExists(
            [FromQuery(Name = "id")] long? id = null,
            [FromQuery(Name = "signCode")] string? signCode = null)
        {
            var normalizedSignCode = Common.NormalizeNullableText(signCode);
            if (string.IsNullOrWhiteSpace(normalizedSignCode))
            {
                return Success(false);
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _service.GetCompanySignatureInfosAsync(companyCd, null, normalizedSignCode, null)).FirstOrDefault();
            var exists = existing != null && (!id.HasValue || existing.ID != id.Value);
            return Success(exists);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateCompanySignatureInfo([FromBody] CompanySignatureInfoRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var displayLabel = Common.NormalizeRequiredText(request.DISPLAY_LABEL);
            if (string.IsNullOrWhiteSpace(displayLabel))
            {
                return ValidationError("DISPLAY_LABEL is required");
            }

            var companyCd = Common.GetCompanyCode();
            var payload = BuildCreateRequest(request);
            var created = await _service.SetCompanySignatureInfoAsync(companyCd, Common.GetUserId(), payload);
            if (created == null)
            {
                return ServerError("Create failed");
            }

            return Created(MapDto(created), "Created successfully");
        }

        [HttpPut("{id:long}")]
        [Authorize]
        public async Task<IActionResult> UpdateCompanySignatureInfo([FromRoute] long id, [FromBody] CompanySignatureInfoRequest? request)
        {
            if (id <= 0)
            {
                return ValidationError("ID is required");
            }

            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _service.GetCompanySignatureInfosAsync(companyCd, id, null, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound("Signature not found");
            }

            var nextDisplayLabel = request.DISPLAY_LABEL == null ? existing.DISPLAY_LABEL : Common.NormalizeRequiredText(request.DISPLAY_LABEL);
            if (string.IsNullOrWhiteSpace(nextDisplayLabel))
            {
                return ValidationError("DISPLAY_LABEL is required");
            }

            var payload = BuildUpdateRequest(existing, request);
            var updated = await _service.SetCompanySignatureInfoAsync(companyCd, Common.GetUserId(), payload);
            if (updated == null)
            {
                return ServerError("Update failed");
            }

            return Updated(MapDto(updated), "Updated successfully");
        }

        [HttpPost("{id:long}/image")]
        [Authorize]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> UploadCompanySignatureImage(
            [FromRoute] long id,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return ValidationError("ID is required");
            }

            if (file == null || file.Length <= 0)
            {
                return ValidationError("Image file is required");
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _service.GetCompanySignatureInfosAsync(companyCd, id, null, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound("Signature not found");
            }

            try
            {
                var publicPath = await CompanySignatureImageStorage.SaveAsync(
                    _environment,
                    companyCd,
                    id,
                    file,
                    cancellationToken);

                var previousPath = existing.SIGN_IMAGE_URL;
                var payload = BuildUpdateRequest(
                    existing,
                    new CompanySignatureInfoRequest
                    {
                        DISPLAY_LABEL = existing.DISPLAY_LABEL,
                        SIGN_NAME = existing.SIGN_NAME,
                        SIGN_TITLE = existing.SIGN_TITLE,
                        SIGN_IMAGE_URL = publicPath,
                        SORT_ORDER = existing.SORT_ORDER,
                        IS_ACTIVE = existing.IS_ACTIVE,
                        ISDEL = existing.ISDEL
                    });

                var updated = await _service.SetCompanySignatureInfoAsync(companyCd, Common.GetUserId(), payload);
                if (updated == null)
                {
                    CompanySignatureImageStorage.TryDeletePublicPath(_environment, publicPath);
                    return ServerError("Update failed");
                }

                if (string.Equals(existing.COMPANY_CD, companyCd, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(previousPath, publicPath, StringComparison.OrdinalIgnoreCase))
                {
                    CompanySignatureImageStorage.TryDeleteStoredPath(_environment, previousPath);
                }

                return Updated(MapDto(updated), "Uploaded successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload company signature image failed. Id={Id}", id);
                return ServerError("Upload failed");
            }
        }

        [HttpGet("{id:long}/image-file")]
        [Authorize]
        public async Task<IActionResult> GetCompanySignatureImageFileById(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return ValidationError("ID is required");
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _service.GetCompanySignatureInfosAsync(companyCd, id, null, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound("Signature not found");
            }

            var storedPath = Common.NormalizeNullableText(existing.SIGN_IMAGE_URL);
            if (storedPath == null)
            {
                return NotFound("Signature image not found");
            }

            return await LoadSignatureImageAsync(storedPath, cancellationToken);
        }

        [HttpGet("image-file")]
        [Authorize]
        public Task<IActionResult> GetCompanySignatureImageFile(
            [FromQuery] string? path,
            CancellationToken cancellationToken)
        {
            var storedPath = Common.NormalizeNullableText(path);
            if (storedPath == null || !CompanySignatureImageStorage.IsAllowedStoredPath(storedPath))
            {
                return Task.FromResult(ValidationError("Invalid signature image path"));
            }

            var companyCd = Common.GetCompanyCode();
            if (CompanySignatureImageStorage.IsFtpRemotePath(storedPath))
            {
                var normalized = storedPath.Replace('\\', '/');
                var companySegment = $"/{companyCd}/";
                if (!normalized.Contains(companySegment, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(NotFound("Signature image not found"));
                }
            }

            return LoadSignatureImageAsync(storedPath, cancellationToken);
        }

        private async Task<IActionResult> LoadSignatureImageAsync(string storedPath, CancellationToken cancellationToken)
        {
            try
            {
                byte[]? bytes = null;
                var contentType = "application/octet-stream";
                await Task.Run(
                    () =>
                    {
                        if (CompanySignatureImageStorage.TryGetImageBytes(
                                _environment,
                                storedPath,
                                out var loaded,
                                out var type))
                        {
                            bytes = loaded;
                            contentType = type;
                        }
                    },
                    cancellationToken);

                if (bytes == null || bytes.Length == 0)
                {
                    return NotFound("Signature image not found");
                }

                Response.Headers.CacheControl = "private, max-age=300";
                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load company signature image failed. Path={Path}", storedPath);
                return ServerError("Cannot load signature image");
            }
        }

        [HttpPost("bulk-delete")]
        [Authorize]
        public async Task<IActionResult> BulkDeleteCompanySignatureInfo([FromBody] DeleteCompanySignaturesRequest? request)
        {
            if (request == null || request.SignatureIds == null || !request.SignatureIds.Any())
            {
                return ValidationError("SignatureIds is required");
            }

            var companyCd = Common.GetCompanyCode();
            var result = await _service.DeleteCompanySignatureInfoAsync(
                companyCd,
                Common.GetUserId(),
                request.SignatureIds.Distinct().ToList());

            return Success(new { deleted = result }, "Deleted successfully");
        }

        private static CompanySignatureInfoDto MapDto(CompanySignatureInfo item)
        {
            return new CompanySignatureInfoDto
            {
                ID = item.ID,
                COMPANY_CD = item.COMPANY_CD,
                SIGN_CODE = item.SIGN_CODE,
                DISPLAY_LABEL = item.DISPLAY_LABEL,
                SIGN_NAME = item.SIGN_NAME,
                SIGN_TITLE = item.SIGN_TITLE,
                SIGN_IMAGE_URL = item.SIGN_IMAGE_URL,
                SORT_ORDER = item.SORT_ORDER,
                IS_ACTIVE = item.IS_ACTIVE,
                ISDEL = item.ISDEL,
            };
        }

        private static CompanySignatureInfoRequest BuildCreateRequest(CompanySignatureInfoRequest request)
        {
            return new CompanySignatureInfoRequest
            {
                ID = 0,
                SIGN_CODE = null,
                DISPLAY_LABEL = Common.NormalizeRequiredText(request.DISPLAY_LABEL),
                SIGN_NAME = Common.NormalizeRequiredText(request.SIGN_NAME),
                SIGN_TITLE = Common.NormalizeNullableText(request.SIGN_TITLE),
                SIGN_IMAGE_URL = Common.NormalizeNullableText(request.SIGN_IMAGE_URL),
                SORT_ORDER = Common.NormalizeNullablePositiveInt(request.SORT_ORDER) ?? 0,
                IS_ACTIVE = Common.NormalizeFlagString(request.IS_ACTIVE, "1"),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0")
            };
        }

        private static CompanySignatureInfoRequest BuildUpdateRequest(CompanySignatureInfo existing, CompanySignatureInfoRequest request)
        {
            return new CompanySignatureInfoRequest
            {
                ID = existing.ID,
                SIGN_CODE = existing.SIGN_CODE,
                DISPLAY_LABEL = request.DISPLAY_LABEL == null ? existing.DISPLAY_LABEL : Common.NormalizeRequiredText(request.DISPLAY_LABEL),
                SIGN_NAME = request.SIGN_NAME == null ? existing.SIGN_NAME : Common.NormalizeRequiredText(request.SIGN_NAME),
                SIGN_TITLE = request.SIGN_TITLE == null ? existing.SIGN_TITLE : Common.NormalizeNullableText(request.SIGN_TITLE),
                SIGN_IMAGE_URL = request.SIGN_IMAGE_URL == null ? existing.SIGN_IMAGE_URL : Common.NormalizeNullableText(request.SIGN_IMAGE_URL),
                SORT_ORDER = request.SORT_ORDER ?? existing.SORT_ORDER ?? 0,
                IS_ACTIVE = Common.NormalizeFlagString(request.IS_ACTIVE ?? existing.IS_ACTIVE, "1"),
                ISDEL = Common.NormalizeFlagString(request.ISDEL ?? existing.ISDEL, "0")
            };
        }
    }
}

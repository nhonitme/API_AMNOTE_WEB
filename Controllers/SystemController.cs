using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    public class SystemController : BaseApiController
    {
        private readonly ISystemService _systemService;

        public SystemController(ISystemService systemService)
        {
            _systemService = systemService;
        }

        [HttpGet("sys-codes")]
        [Authorize]
        public async Task<IActionResult> GetSysCodes([FromQuery] string? codeType = null, [FromQuery] bool refresh = false)
        {
            var companyCd = GetCompanyCode();
            var result = await _systemService.GetSysCodesAsync(companyCd, codeType, refresh);
            return Success(new { data = result.ToList() });
        }

        [HttpPost("master-data-cache/clear")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> ClearMasterDataCache([FromQuery] string? scope = null, [FromQuery] bool global = false)
        {
            var normalizedScope = NormalizeNullableText(scope);
            if (string.IsNullOrWhiteSpace(normalizedScope))
            {
                return ValidationError("scope is required");
            }

            if (global)
            {
                var removedGlobal = await _systemService.ClearMasterDataCacheAsync(normalizedScope, null, true);

                return Success(new
                {
                    removed = removedGlobal,
                    scope = normalizedScope,
                    cacheLevel = "global"
                });
            }

            var resolvedCompanyCd = GetCompanyCode();
            var removed = await _systemService.ClearMasterDataCacheAsync(normalizedScope, resolvedCompanyCd);

            return Success(new
            {
                removed,
                scope = normalizedScope,
                companyCd = resolvedCompanyCd,
                cacheLevel = "company"
            });
        }

        [HttpGet("cache/clear-all")]
        [HttpPost("cache/clear-all")]
        [AllowAnonymous]
        public async Task<IActionResult> ClearAllCache()
        {
            var summary = await _systemService.ClearAllCacheAsync();

            return Success(new
            {
                removed = summary.Total,
                details = summary
            });
        }

        [HttpGet("DownloadTemplate")]
        [Authorize]
        public async Task<IActionResult> DownloadTemplate([FromQuery] string moduleCd, [FromQuery] string? lang = null)
        {
            if (lang == "vi") lang = "VIET";
            if (string.IsNullOrWhiteSpace(moduleCd))
            {
                return ValidationError("moduleCd is required");
            }

            lang ??= GetCurrentLanguage();

            var templateColumns = await Common.GetExcelTemplateColumnInfosAsync(moduleCd);
            if (templateColumns == null || !templateColumns.Any())
            {
                return ValidationError("No template columns found for moduleCd");
            }

            var stream = await ExcelHelper.ExportTemplateAsync2(templateColumns, lang, moduleCd);
            var fileName = $"template_{moduleCd}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpGet("user-permissions")]
        [Authorize]
        public async Task<IActionResult> GetUserPermissions([FromQuery] string? userId = null, [FromQuery] string? menuCode = null)
        {
            var companyCode = GetCompanyCode();
            userId ??= GetUserId();

            var result = await _systemService.GetUserPermissionsAsync(companyCode, userId, menuCode ?? string.Empty);
            return Success(result);
        }

        [HttpPut("user-permissions")]
        [Authorize]
        public async Task<IActionResult> UpdateUserPermissions([FromBody] SysUserPermissionRequest request, [FromQuery] string? lang = null)
        {
            lang ??= GetCurrentLanguage();

            if (request == null || string.IsNullOrWhiteSpace(request.USERID) || string.IsNullOrWhiteSpace(request.MENU_CODE))
            {
                return ValidationError((await getLanguage("USERID", lang)) + " " + (await getLanguage("REQUIRED", lang)));
            }

            var companyCd = GetCompanyCode();
            request.COMPANY_CD = companyCd;
            request.USERID_MODIFY = GetUserId();

            var result = await _systemService.SetUserPermissionAsync(companyCd, request);
            if (result > 0)
            {
                var updated = await _systemService.GetUserPermissionsAsync(companyCd, request.USERID, request.MENU_CODE);
                return Updated(updated, (await getLanguage("COMPANY_CD", lang)) + " " + (await getLanguage("UPDATE_SUCCESS", lang)));
            }

            return ServerError((await getLanguage("COMPANY_CD", lang)) + " " + (await getLanguage("UPDATE_FAILED", lang)));
        }

        [HttpGet("etc-data")]
        [Authorize]
        public async Task<IActionResult> GetEtcInfo([FromQuery] string etcType, [FromQuery] string lang, [FromQuery] string param1, [FromQuery] string param2)
        {
            var companyCd = GetCompanyCode();
            var normalizedEtcType = string.IsNullOrWhiteSpace(etcType) ? string.Empty : etcType.Trim();
            var normalizedLang = NormalizeLanguageCode(string.IsNullOrWhiteSpace(lang) ? GetCurrentLanguage() : lang);
            var normalizedParam1 = string.IsNullOrWhiteSpace(param1) ? string.Empty : param1.Trim();
            var normalizedParam2 = string.IsNullOrWhiteSpace(param2) ? string.Empty : param2.Trim();
            var result = await _systemService.GetEtcInfoAsync(companyCd, normalizedEtcType, normalizedLang, normalizedParam1, normalizedParam2);
            return Success(new { data = result.ToList() });
        }
    }
}

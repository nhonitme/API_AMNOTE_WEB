using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Authorize]
    public class ManagementInfoController : BaseApiController
    {
        private const string MenuCode = "MD_MANAGEMENT";

        private readonly IManagementInfoService _service;
        private readonly ILogger<ManagementInfoController> _logger;

        public ManagementInfoController(
            IManagementInfoService service,
            ILogger<ManagementInfoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetManagementInfo(
            [FromQuery(Name = "managementId")] long? managementId = null,
            [FromQuery(Name = "MG_ID")] long? legacyManagementId = null,
            [FromQuery(Name = "mgCd")] string? mgCd = null,
            [FromQuery(Name = "MG_CD")] string? legacyMgCd = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var targetManagementId = managementId ?? legacyManagementId;
            var targetMgCd = Common.NormalizeNullableText(mgCd) ?? Common.NormalizeNullableText(legacyMgCd);
            var data = (await _service.GetListAsync(companyCd, targetManagementId, targetMgCd)).ToList();

            if ((targetManagementId.HasValue && targetManagementId.Value > 0) || !string.IsNullOrWhiteSpace(targetMgCd))
            {
                if (!data.Any())
                    return NotFound(await BuildMessageAsync("MG_CD", "NOT_FOUND", currentLang));
            }

            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> CreateManagementInfo([FromBody] ManagementInfoRequest? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            var currentLang = ResolveLang(lang);
            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, await BuildMessageAsync("MG_CD", "CREATE_SUCCESS", currentLang));
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> UpdateManagementInfo([FromRoute] long id, [FromBody] ManagementInfoRequest? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            var currentLang = ResolveLang(lang);
            if (id <= 0)
                return ValidationError("MG_ID is required");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(entity, await BuildMessageAsync("MG_CD", "UPDATE_SUCCESS", currentLang));
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> BulkDeleteManagementInfo([FromBody] DeleteManagementInfosRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            if (request == null || request.ManagementIds == null || !request.ManagementIds.Any())
                return ValidationError("ManagementIds is required");

            var companyCd = Common.GetCompanyCode();
            var managementIds = request.ManagementIds.Distinct().ToList();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), managementIds);
            return Success(new { deleted = result }, "Deleted successfully");
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportManagementInfo(
            [FromQuery(Name = "managementId")] long? managementId = null,
            [FromQuery(Name = "MG_ID")] long? legacyManagementId = null,
            [FromQuery(Name = "mgCd")] string? mgCd = null,
            [FromQuery(Name = "MG_CD")] string? legacyMgCd = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var targetManagementId = managementId ?? legacyManagementId;
            var targetMgCd = Common.NormalizeNullableText(mgCd) ?? Common.NormalizeNullableText(legacyMgCd);
            var data = (await _service.GetListAsync(companyCd, targetManagementId, targetMgCd)).ToList();

            if (!data.Any())
                return NotFound(await BuildMessageAsync("MG_CD", "NO_DATA_TO_EXPORT", currentLang));

            const string screenCd = MenuCode;
            const string gridId = "management-info-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("ManagementInfo", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"management_info_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}

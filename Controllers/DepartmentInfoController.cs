using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class DepartmentInfoController : BaseApiController
    {
        private readonly IDepartmentInfoService _service;
        private readonly ILogger<DepartmentInfoController> _logger;

        public DepartmentInfoController(
            IDepartmentInfoService service,
            ILogger<DepartmentInfoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetDepartmentInfo(
            [FromQuery(Name = "departmentId")] long? departmentId = null,
            [FromQuery(Name = "DEPARTMENT_ID")] long? legacyDepartmentId = null,
            [FromQuery(Name = "departmentCd")] string? departmentCd = null,
            [FromQuery(Name = "DEPARTMENT_CD")] string? legacyDepartmentCd = null)
        {
            await EnsurePermissionAsync("MD_DEPARTMENT", "VIEW");
            var companyCd = Common.GetCompanyCode();
            var targetDepartmentId = departmentId ?? legacyDepartmentId;
            var targetDepartmentCd = Common.NormalizeNullableText(departmentCd) ?? Common.NormalizeNullableText(legacyDepartmentCd);
            var data = (await _service.GetListAsync(companyCd, targetDepartmentId, targetDepartmentCd)).ToList();

            if (targetDepartmentId.HasValue || !string.IsNullOrWhiteSpace(targetDepartmentCd))
            {
                if (!data.Any())
                {
                    return NotFound("Department not found");
                }
            }

            return Success(data);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateDepartmentInfo([FromBody] DepartmentInfoRequest? request)
        {
            await EnsurePermissionAsync("MD_DEPARTMENT", "ADD");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, "Created successfully");
        }

        [HttpPut("{id:long}")]
        [Authorize]
        public async Task<IActionResult> UpdateDepartmentInfo([FromRoute] long id, [FromBody] DepartmentInfoRequest? request)
        {
            await EnsurePermissionAsync("MD_DEPARTMENT", "EDIT");

            if (id <= 0)
                return ValidationError("DEPARTMENT_ID is required");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(entity, "Updated successfully");
        }

        [HttpPost("bulk-delete")]
        [Authorize]
        public async Task<IActionResult> BulkDeleteDepartmentInfo([FromBody] DeleteDepartmentInfosRequest? request)
        {
            await EnsurePermissionAsync("MD_DEPARTMENT", "DELETE");

            if (request == null || request.DepartmentIds == null || !request.DepartmentIds.Any())
                return ValidationError("DepartmentIds is required");

            var companyCd = Common.GetCompanyCode();
            var departmentIds = request.DepartmentIds.Distinct().ToList();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), departmentIds);
            return Success(new { deleted = result }, "Deleted successfully");
        }

        [HttpGet("export")]
        [Authorize]
        public async Task<IActionResult> ExportDepartmentInfo(
            [FromQuery(Name = "departmentId")] long? departmentId = null,
            [FromQuery(Name = "DEPARTMENT_ID")] long? legacyDepartmentId = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync("MD_DEPARTMENT", "EXPORT");
            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var targetDepartmentId = departmentId ?? legacyDepartmentId;
            var data = (await _service.GetListAsync(companyCd, targetDepartmentId)).ToList();
            if (!data.Any())
            {
                return NotFound((await Common.getLanguage("DEPARTMENT_CD", currentLang)) + " " + (await Common.getLanguage("NO_DATA_TO_EXPORT", currentLang)));
            }

            const string screenCd = "MD_COST_CENTER";
            const string gridId = "cost-center-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("DepartmentInfo", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);

            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"department_info_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}

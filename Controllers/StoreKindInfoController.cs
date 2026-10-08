using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Authorize]
    public class StoreKindInfoController : BaseApiController
    {
        private const string MenuCode = "MD_WAREHOUSE_TYPE";

        private readonly IStoreKindInfoService _service;

        public StoreKindInfoController(IStoreKindInfoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetStoreKindInfo([FromQuery] int? STORE_KIND_ID = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            var companyCd = Common.GetCompanyCode();
            var data = await _service.GetListAsync(companyCd, STORE_KIND_ID);
            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> CreateStoreKindInfo([FromBody] StoreKindInfoRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, "Created successfully");
        }

        [HttpPut]
        public async Task<IActionResult> UpdateStoreKindInfo([FromBody] StoreKindInfoRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (request == null || request.STORE_KIND_ID <= 0)
                return ValidationError("STORE_KIND_ID is required");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), request.STORE_KIND_ID, request);
            return Updated(entity, "Updated successfully");
        }

        [HttpPost("DeleteMany")]
        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] DeleteStoreKindRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            var storeKindIds = request?.StoreKindIds?.Distinct().ToList() ?? new List<int>();
            if (!storeKindIds.Any())
                return ValidationError("STORE_KIND_ID is required");

            var result = await _service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), storeKindIds);
            return Success(new { deleted = result }, "Deleted successfully");
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportStoreKindInfo([FromQuery] int? STORE_KIND_ID = null, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var data = (await _service.GetListAsync(companyCd, STORE_KIND_ID)).ToList();

            if (!data.Any())
                return NotFound(await BuildMessageAsync("STORE_KIND_CD", "NO_DATA_TO_EXPORT", currentLang));

            const string screenCd = MenuCode;
            const string gridId = "store-kind-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("StoreKindInfo", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"store_kind_info_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}

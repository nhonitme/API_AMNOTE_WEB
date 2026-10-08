using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class AcclistInfoController : BaseApiController
    {
        private const string MenuCode = "MD_ACCOUNT";
        private readonly IAcclistInfoService _service;
        private readonly ILogger<AcclistInfoController> _logger;

        public AcclistInfoController(
            IAcclistInfoService service,
            ILogger<AcclistInfoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAcclistInfo([FromQuery] int? ACC_CD = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var companyCd = Common.GetCompanyCode();
            var data = await _service.GetListAsync(companyCd, ACC_CD);
            return Success(data);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAcclistInfo([FromBody] AcclistInfoRequest request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, "Tạo thành công");
        }

        [HttpPut]
        [Authorize]
        public async Task<IActionResult> UpdateAcclistInfo([FromBody] AcclistInfoRequest request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (request == null)
                return ValidationError("Request body must be provided");

            if (request.ACC_ID == 0)
                return ValidationError("ACC_ID không được để trống");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), request.ACC_ID, request);
            return Updated(entity, "Cập nhật thành công");
        }

        [HttpPost]
        [Route("DeleteMany")]
        [Authorize]
        public async Task<IActionResult> DeleteMany([FromBody] DeleteAcclistInfoRequest request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            var acclistIds = request?.AcclistIds?.Distinct().ToList() ?? new List<int>();
            if (!acclistIds.Any())
                return ValidationError("ACC_ID không được để trống");

            var companyCd = Common.GetCompanyCode();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), acclistIds);
            if (result > 0)
                return Success(new { deleted = result }, "Xóa thành công");

            return NotFound("Không tìm thấy dữ liệu");
        }

        [HttpGet("export")]
        [Authorize]
        public async Task<IActionResult> ExportAcclistInfo([FromQuery] int? ACC_ID = null, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var data = (await _service.GetListAsync(companyCd, ACC_ID)).ToList();

            if (!data.Any())
            {
                return NotFound((await Common.getLanguage("ACC_CD", currentLang)) + " " + (await Common.getLanguage("NO_DATA_TO_EXPORT", currentLang)));
            }

            var templateColumns = await Common.GetExcelTemplateColumnInfosAsync("AcclistInfo");
            var columnMapping = await Common.BuildExcelColumnMappingAsync(templateColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(templateColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"acc_list_info{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}

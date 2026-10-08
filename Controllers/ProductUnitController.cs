using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductUnitController : BaseApiController
    {
        private const string MenuCode = "MD_UNIT";

        private readonly IProductUnitService _service;
        private readonly ILogger<ProductUnitController> _logger;

        public ProductUnitController(
            IProductUnitService service,
            ILogger<ProductUnitController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProductUnit(
            [FromQuery(Name = "unitId")] int? unitId = null,
            [FromQuery(Name = "UNIT_ID")] int? legacyUnitId = null,
            [FromQuery(Name = "unitCd")] string? unitCd = null,
            [FromQuery(Name = "UNIT_CD")] string? legacyUnitCd = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var targetUnitId = unitId ?? legacyUnitId;
            var targetUnitCd = Common.NormalizeNullableText(unitCd) ?? Common.NormalizeNullableText(legacyUnitCd);
            var data = (await _service.GetListAsync(companyCd, targetUnitId, targetUnitCd)).ToList();

            if ((targetUnitId.HasValue && targetUnitId.Value > 0) || !string.IsNullOrWhiteSpace(targetUnitCd))
            {
                if (!data.Any())
                {
                    return NotFound(await BuildMessageAsync("UNIT_CD", "NOT_FOUND", currentLang));
                }
            }

            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> InsertProductUnit([FromBody] ProductUnit? productUnit, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            var currentLang = ResolveLang(lang);
            if (productUnit == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), productUnit);
            return Created(entity, await BuildMessageAsync("UNIT_CD", "CREATE_SUCCESS", currentLang));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProductUnit([FromRoute] int id, [FromBody] ProductUnit? productUnit, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            var currentLang = ResolveLang(lang);
            if (id <= 0)
                return ValidationError(await BuildMessageAsync("UNIT_ID", "REQUIRED", currentLang));

            if (productUnit == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, productUnit);
            return Updated(entity, await BuildMessageAsync("UNIT_CD", "UPDATE_SUCCESS", currentLang));
        }

        [HttpPost("DeleteMany")]
        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] UnitIdsRequest? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            var currentLang = ResolveLang(lang);
            if (request == null || request.UnitIds == null || !request.UnitIds.Any())
                return ValidationError(await BuildMessageAsync("UNIT_ID", "REQUIRED", currentLang));

            var companyCd = Common.GetCompanyCode();
            var unitIds = request.UnitIds.Distinct().ToList();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), unitIds);
            return Success(new { deleted = result }, await Common.getLanguage("DELETE_SUCCESS", currentLang));
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportProductUnit([FromQuery] int? UNIT_ID = null, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var data = (await _service.GetListAsync(companyCd, UNIT_ID)).ToList();

            if (!data.Any())
            {
                return NotFound(await BuildMessageAsync("UNIT_CD", "NO_DATA_TO_EXPORT", currentLang));
            }

            const string screenCd = MenuCode;
            const string gridId = "product-unit-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("ProductUnit", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"product_unit_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}

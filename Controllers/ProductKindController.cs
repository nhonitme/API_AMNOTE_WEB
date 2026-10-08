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
    public class ProductKindController : BaseApiController
    {
        private const string MenuCode = "MD_PRODUCT_GROUP";

        private readonly IProductKindService _service;
        private readonly ILogger<ProductKindController> _logger;

        public ProductKindController(
            IProductKindService service,
            ILogger<ProductKindController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProductKind(
            [FromQuery(Name = "productKindId")] int? productKindId = null,
            [FromQuery(Name = "PRODUCT_KIND_ID")] int? legacyProductKindId = null,
            [FromQuery(Name = "productKindCd")] string? productKindCd = null,
            [FromQuery(Name = "PRODUCT_KIND_CD")] string? legacyProductKindCd = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var targetProductKindId = productKindId ?? legacyProductKindId;
            var targetProductKindCd = Common.NormalizeNullableText(productKindCd) ?? Common.NormalizeNullableText(legacyProductKindCd);
            var data = (await _service.GetListAsync(companyCd, targetProductKindId, targetProductKindCd)).ToList();

            if ((targetProductKindId.HasValue && targetProductKindId.Value > 0) || !string.IsNullOrWhiteSpace(targetProductKindCd))
            {
                if (!data.Any())
                {
                    return NotFound(await BuildMessageAsync("PRODUCT_KIND_CD", "NOT_FOUND", currentLang));
                }
            }

            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> InsertProductKind([FromBody] ProductKind? productKind, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            var currentLang = ResolveLang(lang);
            if (productKind == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), productKind);
            return Created(entity, await BuildMessageAsync("PRODUCT_KIND_CD", "CREATE_SUCCESS", currentLang));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProductKind([FromRoute] int id, [FromBody] ProductKind? productKind, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            var currentLang = ResolveLang(lang);
            if (id <= 0)
                return ValidationError(await BuildMessageAsync("PRODUCT_KIND_ID", "REQUIRED", currentLang));

            if (productKind == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, productKind);
            return Updated(entity, await BuildMessageAsync("PRODUCT_KIND_CD", "UPDATE_SUCCESS", currentLang));
        }

        [HttpPost("DeleteMany")]
        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] ProductKindIdsRequest? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            var currentLang = ResolveLang(lang);
            if (request == null || request.ProductKindIds == null || !request.ProductKindIds.Any())
                return ValidationError(await BuildMessageAsync("PRODUCT_KIND_ID", "REQUIRED", currentLang));

            var companyCd = Common.GetCompanyCode();
            var productKindIds = request.ProductKindIds.Distinct().ToList();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), productKindIds);
            return Success(new { deleted = result }, await Common.getLanguage("DELETE_SUCCESS", currentLang));
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportProductKind([FromQuery] int? PRODUCT_KIND_ID = null, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var data = (await _service.GetListAsync(companyCd, PRODUCT_KIND_ID)).ToList();

            if (!data.Any())
            {
                return NotFound(await BuildMessageAsync("PRODUCT_KIND_CD", "NO_DATA_TO_EXPORT", currentLang));
            }

            const string screenCd = MenuCode;
            const string gridId = "product-kind-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("ProductKind", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"product_kind_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}

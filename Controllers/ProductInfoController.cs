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
    public class ProductInfoController : BaseApiController
    {
        private const string MenuCode = "MD_INVENTORY";

        private readonly IProductInfoService _service;
        private readonly ILogger<ProductInfoController> _logger;

        public ProductInfoController(
            IProductInfoService service,
            ILogger<ProductInfoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetProductInfo(
            [FromQuery(Name = "productId")] int? productId = null,
            [FromQuery(Name = "PRODUCT_ID")] int? legacyProductId = null,
            [FromQuery(Name = "productCd")] string? productCd = null,
            [FromQuery(Name = "PRODUCT_CD")] string? legacyProductCd = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var targetProductId = productId ?? legacyProductId;
            var targetProductCd = Common.NormalizeNullableText(productCd) ?? Common.NormalizeNullableText(legacyProductCd);
            var rawData = await _service.GetListAsync(companyCd, targetProductId);
            var data = FilterByProductCd(rawData, targetProductCd).ToList();

            if ((targetProductId.HasValue && targetProductId.Value > 0) || !string.IsNullOrWhiteSpace(targetProductCd))
            {
                if (!data.Any())
                {
                    return NotFound(await BuildMessageAsync("PRODUCT_CD", "NOT_FOUND", currentLang));
                }
            }

            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> InsertProductInfo([FromBody] ProductInfo? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            var currentLang = ResolveLang(lang);
            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, await BuildMessageAsync("PRODUCT_CD", "CREATE_SUCCESS", currentLang));
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProductInfo([FromRoute] int id, [FromBody] ProductInfoDto? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            var currentLang = ResolveLang(lang);
            if (id <= 0)
                return ValidationError(await BuildMessageAsync("PRODUCT_ID", "REQUIRED", currentLang));

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(entity, await BuildMessageAsync("PRODUCT_CD", "UPDATE_SUCCESS", currentLang));
        }

        [HttpPost("DeleteMany")]
        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] ProductIdsRequest? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            var currentLang = ResolveLang(lang);
            if (request == null || request.ProductIds == null || !request.ProductIds.Any())
                return ValidationError(await BuildMessageAsync("PRODUCT_ID", "REQUIRED", currentLang));

            var companyCd = Common.GetCompanyCode();
            var productIds = request.ProductIds.Distinct().ToList();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), productIds);
            return Success(new { deleted = result }, await Common.getLanguage("DELETE_SUCCESS", currentLang));
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportProductInfo([FromQuery] int? productId = null, [FromQuery] string? productCd = null, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var rawExport = await _service.GetListAsync(companyCd, productId);
            var data = FilterByProductCd(rawExport, Common.NormalizeNullableText(productCd)).ToList();

            if (!data.Any())
            {
                return NotFound(await BuildMessageAsync("PRODUCT_CD", "NO_DATA_TO_EXPORT", currentLang));
            }

            const string screenCd = MenuCode;
            const string gridId = "product-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("ProductInfo", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"product_info_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static IEnumerable<ProductInfoDto> FilterByProductCd(IEnumerable<ProductInfoDto> data, string? productCd)
        {
            if (string.IsNullOrWhiteSpace(productCd))
                return data;

            return data.Where(x => string.Equals(x.PRODUCT_CD, productCd, StringComparison.OrdinalIgnoreCase));
        }
    }
}

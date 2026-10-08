using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Authorize]
    public class CustomerInfoCustomerExtController : BaseApiController
    {
        private const string MenuCode = "MD_CUSTOMER";
        private readonly ICustomerInfoService _service;
        private readonly ILogger<CustomerInfoCustomerExtController> _logger;

        public CustomerInfoCustomerExtController(
            ICustomerInfoService service,
            ILogger<CustomerInfoCustomerExtController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerInfoCustomerExt(
            [FromQuery(Name = "customerId")] long? customerId = null,
            [FromQuery(Name = "CUSTOMER_ID")] long? legacyCustomerId = null,
            [FromQuery(Name = "customerCd")] string? customerCd = null,
            [FromQuery(Name = "CUSTOMER_CD")] string? legacyCustomerCd = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            var companyCd = Common.GetCompanyCode();
            var targetCustomerId = customerId ?? legacyCustomerId;
            var targetCustomerCd = Common.NormalizeNullableText(customerCd) ?? Common.NormalizeNullableText(legacyCustomerCd);
            var data = await _service.GetListAsync(companyCd, targetCustomerId, targetCustomerCd);

            if ((targetCustomerId.HasValue && targetCustomerId.Value > 0) || !string.IsNullOrWhiteSpace(targetCustomerCd))
            {
                if (!data.Any())
                    return NotFound("Customer not found");
            }

            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomerInfoCustomerExt([FromBody] CustomerInfoCustomerExtRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, "Created successfully");
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> UpdateCustomerInfoCustomerExt([FromRoute] long id, [FromBody] CustomerInfoCustomerExtRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (id <= 0)
                return ValidationError("CUSTOMER_ID is required");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(entity, "Updated successfully");
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> BulkDeleteCustomerInfoCustomerExt([FromBody] DeleteCustomerInfosRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            if (request == null || request.CustomerIds == null || !request.CustomerIds.Any())
                return ValidationError("CustomerIds is required");

            var companyCd = Common.GetCompanyCode();
            var customerIds = request.CustomerIds.Distinct().ToList();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), customerIds);
            return Success(new { deleted = result }, "Deleted successfully");
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportCustomerInfoCustomerExt(
            [FromQuery(Name = "customerId")] long? customerId = null,
            [FromQuery(Name = "CUSTOMER_ID")] long? legacyCustomerId = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");

            var currentLang = ResolveLang(lang);
            var companyCd = Common.GetCompanyCode();
            var targetCustomerId = customerId ?? legacyCustomerId;
            var data = (await _service.GetListAsync(companyCd, targetCustomerId, null)).ToList();

            if (!data.Any())
                return NotFound(await BuildMessageAsync("CUSTOMER_CD", "NO_DATA_TO_EXPORT", currentLang));

            const string screenCd = MenuCode;
            const string gridId = "customer-ext-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("CustomerInfoCustomerExt", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"customer_info_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}


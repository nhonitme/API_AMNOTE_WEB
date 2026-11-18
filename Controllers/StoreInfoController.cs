using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Authorize]
    public class StoreInfoController : BaseApiController
    {
        private const string MenuCode = "MD_WAREHOUSE";

        private readonly IStoreInfoService _service;

        public StoreInfoController(IStoreInfoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetStoreInfo([FromQuery] int? STORE_ID = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            var companyCd = Common.GetCompanyCode();
            var data = await _service.GetListAsync(companyCd, STORE_ID);
            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> CreateStoreInfo([FromBody] StoreInfoRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, "Created successfully");
        }

        [HttpPut]
        public async Task<IActionResult> UpdateStoreInfo([FromBody] StoreInfoRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (request == null || request.STORE_ID <= 0)
                return ValidationError("STORE_ID is required");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), request.STORE_ID, request);
            return Updated(entity, "Updated successfully");
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] DeleteStoresRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            var storeIds = request?.StoreIds?.Distinct().ToList() ?? new List<int>();
            if (!storeIds.Any())
                return ValidationError("STORE_ID is required");

            var result = await _service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), storeIds);
            return Success(new { deleted = result }, "Deleted successfully");
        }

    }
}

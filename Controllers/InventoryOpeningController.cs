using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Authorize]
    public class InventoryOpeningController : BaseApiController
    {
        private const string MenuCode = "INV_OPENING";

        private readonly IInventoryOpeningService _service;

        public InventoryOpeningController(IInventoryOpeningService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] long? INPUT_ID = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            var data = await _service.GetListAsync(Common.GetCompanyCode(), INPUT_ID);
            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InventoryOpeningRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            if (request == null)
                return ValidationError("Request body must be provided");

            try
            {
                var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
                return Created(entity, "Created successfully");
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] InventoryOpeningRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (request == null || request.INPUT_ID <= 0)
                return ValidationError("INPUT_ID is required");

            try
            {
                var entity = await _service.UpdateAsync(
                    Common.GetCompanyCode(),
                    Common.GetUserId(),
                    request.INPUT_ID,
                    request);
                return Updated(entity, "Updated successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
        }

        [HttpPost("bulk-delete")]
        public async Task<IActionResult> DeleteMany([FromBody] InventoryOpeningIdsRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");

            var inputIds = request?.InputIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();
            if (inputIds.Count == 0)
                return ValidationError("INPUT_ID is required");

            var deleted = await _service.DeleteAsync(Common.GetCompanyCode(), Common.GetUserId(), inputIds);
            return Success(new { deleted }, "Deleted successfully");
        }

    }
}

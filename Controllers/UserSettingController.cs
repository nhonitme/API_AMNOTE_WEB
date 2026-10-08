using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    public class UserSettingController : BaseApiController
    {
        private readonly IUserSettingService _service;

        public UserSettingController(IUserSettingService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get([FromQuery] string? keyName = null)
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();

            if (!string.IsNullOrWhiteSpace(keyName))
            {
                var item = await _service.GetSettingAsync(companyCd, userId, keyName.Trim());
                return Success(item);
            }

            var items = await _service.GetSettingsAsync(companyCd, userId);
            return Success(items);
        }

        [HttpGet("batch")]
        [Authorize]
        public async Task<IActionResult> GetBatch([FromQuery] string? keyNames = null)
        {
            if (string.IsNullOrWhiteSpace(keyNames))
            {
                return ValidationError("keyNames is required");
            }

            var keys = keyNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToList();

            if (keys.Count == 0)
            {
                return ValidationError("keyNames is required");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var items = await _service.GetSettingsAsync(companyCd, userId, keys);
            return Success(items);
        }

        [HttpPut]
        [Authorize]
        public async Task<IActionResult> Put([FromBody] UserSettingSaveRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Request is required");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var item = await _service.SaveAsync(companyCd, userId, request);
            return Updated(item, "Saved successfully");
        }

        [HttpPut("bulk")]
        [Authorize]
        public async Task<IActionResult> PutBulk([FromBody] UserSettingBulkSaveRequest? request)
        {
            if (request?.ITEMS == null || request.ITEMS.Count == 0)
            {
                return ValidationError("ITEMS is required");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var items = await _service.SaveBulkAsync(companyCd, userId, request.ITEMS);
            return Updated(items, "Saved successfully");
        }

        [HttpDelete("{keyName}")]
        [Authorize]
        public async Task<IActionResult> Delete(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                return ValidationError("keyName is required");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var deleted = await _service.DeleteAsync(companyCd, userId, keyName);
            if (!deleted)
            {
                return NotFound("User setting not found");
            }

            return Deleted(null, "Deleted successfully");
        }

        [HttpPost("clear-cache")]
        [Authorize]
        public async Task<IActionResult> ClearCache([FromQuery] string? keyName = null)
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var removed = await _service.ClearCacheAsync(companyCd, userId, keyName);
            return Success(new
            {
                removed,
                companyCd,
                userId,
                keyName = Common.NormalizeNullableText(keyName)
            });
        }
    }
}

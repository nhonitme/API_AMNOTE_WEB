using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    public class SysDecimalSettingController : BaseApiController
    {
        private readonly ISysDecimalSettingService _service;
        private readonly ILogger<SysDecimalSettingController> _logger;

        public SysDecimalSettingController(ISysDecimalSettingService service, ILogger<SysDecimalSettingController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get(
            [FromQuery] string? settingType = null)
        {
            var resolvedCompanyCd = Common.GetCompanyCode();
            var data = await _service.GetSettingsAsync(resolvedCompanyCd, settingType);
            return Success(data);
        }

        [HttpGet("fields")]
        [Authorize]
        public async Task<IActionResult> GetFields(
            [FromQuery] string? fieldName = null,
            [FromQuery] string? settingType = null)
        {
            var resolvedCompanyCd = Common.GetCompanyCode();
            var data = await _service.GetFieldSettingsAsync(resolvedCompanyCd, fieldName, settingType);
            return Success(data);
        }

        [HttpGet("{settingType}")]
        [Authorize]
        public async Task<IActionResult> GetByType(string settingType)
        {
            var resolvedCompanyCd = Common.GetCompanyCode();
            var data = await _service.GetSettingsAsync(resolvedCompanyCd, settingType);
            return Success(data);
        }

        [HttpPut("{settingType}")]
        [Authorize]
        public async Task<IActionResult> Update(string settingType, [FromBody] SysDecimalSettingUpdateRequest? request)
        {
            if (request == null)
                return ValidationError("Request is required");

            var resolvedCompanyCd = Common.GetCompanyCode();
            var existing = await _service.GetSettingAsync(resolvedCompanyCd, settingType);
            var dto = await _service.UpsertSettingAsync(resolvedCompanyCd, settingType, request, Common.GetUserId());

            if (dto == null)
                return ServerError("Update failed");

            return existing == null
                ? Created(dto, "Created successfully")
                : Updated(dto, "Updated successfully");
        }
    }
}

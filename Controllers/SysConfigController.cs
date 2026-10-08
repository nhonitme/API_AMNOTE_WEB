using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    public class SysConfigController : BaseApiController
    {
        private readonly ISysConfigRepository _repository;
        private readonly ISysConfigService _sysConfigService;

        public SysConfigController(ISysConfigRepository repository, ISysConfigService sysConfigService)
        {
            _repository = repository;
            _sysConfigService = sysConfigService;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get([FromQuery] string? configGroup = null, [FromQuery] string? configKey = null, [FromQuery] bool activeOnly = true)
        {
            var resolvedCompanyCd = Common.GetCompanyCode();
            var items = (await _repository.GetSysConfigsAsync(resolvedCompanyCd, configGroup, configKey, activeOnly))
                .Select(MapToDto)
                .ToList();

            return Success(new { data = items });
        }

        [HttpGet("{configGroup}/{configKey}")]
        [Authorize]
        public async Task<IActionResult> GetByKey(string configGroup, string configKey, [FromQuery] bool activeOnly = true)
        {
            if (string.IsNullOrWhiteSpace(configGroup))
            {
                return ValidationError("configGroup is required");
            }

            if (string.IsNullOrWhiteSpace(configKey))
            {
                return ValidationError("configKey is required");
            }

            var resolvedCompanyCd = Common.GetCompanyCode();
            var item = await _repository.GetSysConfigAsync(resolvedCompanyCd, configGroup.Trim(), configKey.Trim(), activeOnly);
            if (item == null)
            {
                return NotFound("SysConfig not found");
            }

            return Success(MapToDto(item));
        }

        [HttpPost("clear-cache")]
        [Authorize]
        public async Task<IActionResult> ClearCache([FromQuery] string? configGroup = null, [FromQuery] string? configKey = null)
        {
            if (!string.IsNullOrWhiteSpace(configKey) && string.IsNullOrWhiteSpace(configGroup))
            {
                return ValidationError("configGroup is required when configKey is provided");
            }

            var resolvedCompanyCd = Common.GetCompanyCode();
            var normalizedGroup = Common.NormalizeNullableText(configGroup);
            var normalizedKey = Common.NormalizeNullableText(configKey);
            var removed = await _sysConfigService.ClearCacheAsync(resolvedCompanyCd, normalizedGroup, normalizedKey);
            return Success(new
            {
                removed,
                companyCd = resolvedCompanyCd,
                configGroup = normalizedGroup,
                configKey = normalizedKey
            });
        }


        private static SysConfigDto MapToDto(SysConfig item)
        {
            return new SysConfigDto
            {
                ID = item.ID,
                COMPANY_CD = item.COMPANY_CD,
                CONFIG_GROUP = item.CONFIG_GROUP,
                CONFIG_KEY = item.CONFIG_KEY,
                CONFIG_VALUE = item.CONFIG_VALUE,
                DATA_TYPE = item.DATA_TYPE,
                DESCRIPTION = item.DESCRIPTION,
                IS_ACTIVE = item.IS_ACTIVE,
            };
        }
    }
}

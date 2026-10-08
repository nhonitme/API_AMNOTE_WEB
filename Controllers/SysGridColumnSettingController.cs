using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/[controller]")]
    public class SysGridColumnSettingController : BaseApiController
    {
        private readonly ISysGridColumnSettingRepository _repository;

        public SysGridColumnSettingController(ISysGridColumnSettingRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("all")]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var companyCd = Common.GetCompanyCode();
            var userId = ResolveUserId(null);
            var bundle = await _repository.GetAllAsync(companyCd, userId);
            return Success(WithoutAuditColumns(bundle));
        }

        [HttpPut]
        [Authorize]
        public async Task<IActionResult> Put([FromBody] SysGridColumnSettingSaveRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Request is required");
            }

            var gridId = Common.NormalizeRequiredValue(request.GRID_ID, nameof(request.GRID_ID), 100);
            var userId = ResolveUserId(request.USER_ID);
            var columns = NormalizeColumns(request.COLUMNS);

            var slice = await _repository.SaveAsync(
                Common.GetCompanyCode(),
                userId,
                gridId,
                request.TEMPLATE_ID.GetValueOrDefault(),
                Common.NormalizeNullableText(request.TEMPLATE_NAME),
                request.IS_DEFAULT_TEMPLATE,
                columns,
                Common.GetUserId());

            return Updated(WithoutAuditColumns(slice), "Saved successfully");
        }

        [HttpPost("reset")]
        [Authorize]
        public async Task<IActionResult> Reset([FromBody] SysGridColumnResetRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Request is required");
            }

            var gridId = Common.NormalizeRequiredValue(request.GRID_ID, nameof(request.GRID_ID), 100);
            var slice = await _repository.ResetAsync(
                Common.GetCompanyCode(),
                ResolveUserId(request.USER_ID),
                gridId,
                Common.GetUserId());

            return Success(WithoutAuditColumns(slice), "Reset successfully");
        }

        private static SysGridColumnBundleDto WithoutAuditColumns(SysGridColumnBundleDto bundle)
        {
            return new SysGridColumnBundleDto
            {
                COLUMNS = bundle.COLUMNS.Where(column => !IsAuditField(column.FIELD_NAME)).ToList(),
                SETTINGS = bundle.SETTINGS.Where(column => !IsAuditField(column.FIELD_NAME)).ToList(),
                TEMPLATES = bundle.TEMPLATES
            };
        }

        private static bool IsAuditField(string fieldName) => fieldName.Trim().ToUpperInvariant() is
            "CREATE_AT" or "CREATE_BY" or "UPDATE_AT" or "UPDATE_BY" or "CREATEBY";

        private static string ResolveUserId(string? userId)
        {
            var resolved = string.IsNullOrWhiteSpace(userId) ? Common.GetUserId() : userId.Trim();
            if (string.IsNullOrWhiteSpace(resolved))
            {
                throw new ArgumentException("USER_ID is required");
            }

            return resolved;
        }

        private static List<SysGridColumnSettingSaveItemRequest> NormalizeColumns(
            IEnumerable<SysGridColumnSettingSaveItemRequest>? columns)
        {
            if (columns == null)
            {
                return new List<SysGridColumnSettingSaveItemRequest>();
            }

            return columns
                .Select(item =>
                {
                    var fieldName = Common.NormalizeRequiredText(item.FIELD_NAME);
                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        throw new ArgumentException("FIELD_NAME is required");
                    }

                    var fixedPosition = Common.NormalizeNullableText(item.FIXED_POSITION);
                    if (fixedPosition != null &&
                        !fixedPosition.Equals("left", StringComparison.OrdinalIgnoreCase) &&
                        !fixedPosition.Equals("right", StringComparison.OrdinalIgnoreCase) &&
                        fixedPosition.Length > 0)
                    {
                        fixedPosition = null;
                    }

                    var sortOrder = Common.NormalizeNullableText(item.SORT_ORDER)?.ToLowerInvariant();
                    if (sortOrder != null && sortOrder != "asc" && sortOrder != "desc")
                    {
                        sortOrder = null;
                    }

                    return new SysGridColumnSettingSaveItemRequest
                    {
                        FIELD_NAME = fieldName,
                        LABEL_TEXT = Truncate(Common.NormalizeNullableText(item.LABEL_TEXT), 100),
                        CAPTION = Truncate(Common.NormalizeNullableText(item.CAPTION), 255),
                        IS_VISIBLE = NormalizeOptionalFlag(item.IS_VISIBLE),
                        VISIBLE_INDEX = item.VISIBLE_INDEX,
                        COLUMN_WIDTH = item.COLUMN_WIDTH,
                        IS_FIXED = NormalizeOptionalFlag(item.IS_FIXED),
                        FIXED_POSITION = fixedPosition,
                        ALLOW_HIDING = NormalizeOptionalFlag(item.ALLOW_HIDING),
                        SORT_ORDER = sortOrder,
                        SORT_INDEX = item.SORT_INDEX
                    };
                })
                .Where(item => !IsAuditField(item.FIELD_NAME!))
                .GroupBy(item => item.FIELD_NAME!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static string? NormalizeOptionalFlag(string? value)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (normalized == null)
            {
                return null;
            }

            return normalized == "1" ? "1" : "0";
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (value == null || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }
    }
}

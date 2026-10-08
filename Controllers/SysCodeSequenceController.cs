using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class SysCodeSequenceController : BaseApiController
    {
        private const string MenuCode = "MD_COMPANY";

        private readonly ISysCodeSequenceRepository _repository;

        public SysCodeSequenceController(ISysCodeSequenceRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Get([FromQuery] string? objectType = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            var companyCd = Common.GetCompanyCode();
            var data = (await _repository.GetSequencesAsync(companyCd, objectType)).ToList();
            return Success(data);
        }

        [HttpGet("{id:long}")]
        [Authorize]
        public async Task<IActionResult> GetById([FromRoute] long id)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");
            if (id <= 0)
            {
                return ValidationError("ID is required");
            }

            var companyCd = Common.GetCompanyCode();
            var data = await _repository.GetSequenceAsync(companyCd, id);
            return data == null ? NotFound("Sequence not found") : Success(data);
        }

        [HttpGet("preview")]
        [Authorize]
        public async Task<IActionResult> Preview([FromQuery] string? objectType = null, [FromQuery] DateTime? baseDate = null)
        {
            var normalizedObjectType = Common.NormalizeNullableText(objectType)?.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedObjectType))
            {
                return ValidationError("OBJECT_TYPE is required");
            }

            var companyCd = Common.GetCompanyCode();
            var preview = await _repository.PreviewAsync(companyCd, normalizedObjectType, baseDate);
            return Success(preview!);
        }

        [HttpGet("preview/context")]
        [Authorize]
        public async Task<IActionResult> PreviewByContext(
            [FromQuery] string? menuCode = null,
            [FromQuery] string? codeField = null,
            [FromQuery] DateTime? baseDate = null)
        {
            var normalizedMenuCode = Common.NormalizeNullableText(menuCode)?.ToUpperInvariant();
            var normalizedCodeField = Common.NormalizeNullableText(codeField)?.ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedMenuCode))
            {
                return ValidationError("MENU_CODE is required");
            }

            if (string.IsNullOrWhiteSpace(normalizedCodeField))
            {
                return ValidationError("CODE_FIELD is required");
            }

            var companyCd = Common.GetCompanyCode();
            var preview = await _repository.PreviewByContextAsync(companyCd, normalizedMenuCode, normalizedCodeField, baseDate);
            return Success(preview!);
        }

        [HttpGet("previews")]
        [Authorize]
        public async Task<IActionResult> PreviewMany([FromQuery] string? objectTypes = null, [FromQuery] DateTime? baseDate = null)
        {
            var normalizedObjectTypes = ResolvePreviewObjectTypes(objectTypes);
            if (normalizedObjectTypes.Count == 0)
            {
                return ValidationError("OBJECT_TYPES is required");
            }

            var companyCd = Common.GetCompanyCode();
            var previews = await _repository.PreviewManyAsync(companyCd, normalizedObjectTypes, baseDate);
            return Success(previews);
        }

        private static List<string> ResolvePreviewObjectTypes(string? objectTypes)
        {
            if (string.IsNullOrWhiteSpace(objectTypes))
            {
                return new List<string>();
            }

            return objectTypes
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => Common.NormalizeNullableText(item)?.ToUpperInvariant())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] SysCodeSequenceRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var companyCd = Common.GetCompanyCode();
            request.ID = 0;
            var result = await _repository.UpsertSequenceAsync(companyCd, request);
            if (result <= 0)
            {
                return ServerError("Create failed");
            }

            return Created(new { affected = result }, "Created successfully");
        }

        [HttpPut("{id:long}")]
        [Authorize]
        public async Task<IActionResult> Update([FromRoute] long id, [FromBody] SysCodeSequenceRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");
            if (id <= 0)
            {
                return ValidationError("ID is required");
            }

            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var companyCd = Common.GetCompanyCode();
            request.ID = id;
            var result = await _repository.UpsertSequenceAsync(companyCd, request);
            if (result <= 0)
            {
                return ServerError("Update failed");
            }

            return Updated(new { affected = result }, "Updated successfully");
        }

        [HttpDelete("{id:long}")]
        [Authorize]
        public async Task<IActionResult> Delete([FromRoute] long id)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");
            if (id <= 0)
            {
                return ValidationError("ID is required");
            }

            var companyCd = Common.GetCompanyCode();
            var result = await _repository.DeleteSequenceAsync(companyCd, id);
            return result <= 0 ? ServerError("Delete failed") : Deleted(id, "Deleted successfully");
        }
    }
}

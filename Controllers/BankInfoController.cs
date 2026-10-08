using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Interfaces.Catalog;
using API_AMNOTE_WEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class BankInfoController : BaseApiController
    {
        private const string MenuCode = "MD_BANK";

        private readonly IBankInfoService _service;

        public BankInfoController(IBankInfoService service)
        {
            _service = service;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetBankInfo(
            [FromQuery(Name = "bankId")] long? bankId = null,
            [FromQuery(Name = "BANK_ID")] long? legacyBankId = null,
            [FromQuery(Name = "bankCd")] string? bankCd = null,
            [FromQuery(Name = "BANK_CD")] string? legacyBankCd = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var companyCd = Common.GetCompanyCode();
            var targetBankId = bankId ?? legacyBankId;
            var targetBankCd = Common.NormalizeNullableText(bankCd) ?? Common.NormalizeNullableText(legacyBankCd);
            var data = (await _service.GetListAsync(companyCd, targetBankId, targetBankCd)).ToList();

            if ((targetBankId.HasValue && targetBankId.Value > 0) || !string.IsNullOrWhiteSpace(targetBankCd))
            {
                if (!data.Any())
                {
                    return NotFound("Bank not found");
                }
            }

            return Success(data);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateBankInfo([FromBody] BankInfoRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.CreateAsync(Common.GetCompanyCode(), Common.GetUserId(), request);
            return Created(entity, "Created successfully");
        }

        [HttpPut("{id:long}")]
        [Authorize]
        public async Task<IActionResult> UpdateBankInfo([FromRoute] long id, [FromBody] BankInfoRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (id <= 0)
                return ValidationError("BANK_ID is required");

            if (request == null)
                return ValidationError("Request body must be provided");

            var entity = await _service.UpdateAsync(Common.GetCompanyCode(), Common.GetUserId(), id, request);
            return Updated(entity, "Updated successfully");
        }

        [HttpPost("bulk-delete")]
        [Authorize]
        public async Task<IActionResult> BulkDeleteBankInfo([FromBody] DeleteBankInfosRequest? request)
        {
            await EnsurePermissionAsync(MenuCode, "DELETE");
            if (request == null || request.BankIds == null || !request.BankIds.Any())
            {
                return ValidationError("BankIds is required");
            }

            var companyCd = Common.GetCompanyCode();
            var bankIds = request.BankIds.Distinct().ToList();
            var result = await _service.DeleteAsync(companyCd, Common.GetUserId(), bankIds);
            return Success(new { deleted = result }, "Deleted successfully");
        }

        [HttpGet("export")]
        [Authorize]
        public async Task<IActionResult> ExportBankInfo(
            [FromQuery(Name = "bankId")] long? bankId = null,
            [FromQuery(Name = "BANK_ID")] long? legacyBankId = null,
            [FromQuery(Name = "bankCd")] string? bankCd = null,
            [FromQuery(Name = "BANK_CD")] string? legacyBankCd = null,
            [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EXPORT");
            var currentLang = string.IsNullOrWhiteSpace(lang) ? Common.GetCurrentLanguage() : lang.Trim();
            var companyCd = Common.GetCompanyCode();
            var targetBankId = bankId ?? legacyBankId;
            var targetBankCd = Common.NormalizeNullableText(bankCd) ?? Common.NormalizeNullableText(legacyBankCd);
            var data = (await _service.GetListAsync(companyCd, targetBankId, targetBankCd)).ToList();
            if (!data.Any())
            {
                return NotFound((await Common.getLanguage("BANK_CD", currentLang)) + " " + (await Common.getLanguage("NO_DATA_TO_EXPORT", currentLang)));
            }

            const string screenCd = MenuCode;
            const string gridId = "bank-grid";

            var exportColumns = await Common.GetExcelExportColumnInfosAsync("BankInfo", screenCd, gridId);
            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);

            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"bank_info_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}

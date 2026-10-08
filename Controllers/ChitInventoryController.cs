using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static API_AMNOTE_WEB.Helpers.Common;

namespace API_AMNOTE_WEB.Controllers
{
    public class ChitInventoryController : BaseApiController
    {
        private readonly IInventoryVoucherRepository _repository;
        private readonly IInventoryVoucherWriteService _writeService;
        private readonly ILogger<ChitInventoryController> _logger;

        private static readonly Dictionary<string, string> PermissionMap = new()
        {
            ["AP:IR"] = "INV_RECEIPT",
            ["AR:IO"] = "INV_ISSUE",
            ["INV:IA"] = "INV_ADJUST"
        };

        public ChitInventoryController(
            IInventoryVoucherRepository repository,
            IInventoryVoucherWriteService writeService,
            ILogger<ChitInventoryController> logger)
        {
            _repository = repository;
            _writeService = writeService;
            _logger = logger;
        }

        [HttpGet("Get")]
        [Authorize]
        public async Task<IActionResult> Get(
            [FromQuery] string? INPUT_TYPE,
            [FromQuery] string? CHIT_TYPE,
            [FromQuery] long? CHIT_ID = null,
            [FromQuery] string? SEARCH_TEXT = null,
            [FromQuery] string? FROM_YMD = null,
            [FromQuery] string? TO_YMD = null,
            [FromQuery] int PAGE_NUMBER = 1,
            [FromQuery] int PAGE_SIZE = 20)
        {
            var inputType = NormalizeInputType(INPUT_TYPE, CHIT_TYPE);
            var chitType = NormalizeChitType(CHIT_TYPE);
            if (inputType == null || chitType == null)
            {
                return ValidationError("INPUT_TYPE or CHIT_TYPE is invalid");
            }

            if (CHIT_ID.HasValue && CHIT_ID.Value <= 0)
            {
                return ValidationError("CHIT_ID is invalid");
            }

            if (PAGE_NUMBER <= 0)
            {
                return ValidationError("PAGE_NUMBER must be greater than 0");
            }

            if (PAGE_SIZE <= 0)
            {
                return ValidationError("PAGE_SIZE must be greater than 0");
            }

            await EnsureInventoryPermissionAsync(inputType, chitType, "VIEW");

            var fromYmd = NormalizeNullableYmdText(FROM_YMD, nameof(FROM_YMD));
            var toYmd = NormalizeNullableYmdText(TO_YMD, nameof(TO_YMD));
            Common.ValidateYmdRange(fromYmd, toYmd);

            var companyCd = Common.GetCompanyCode();
            var (pageNumber, pageSize) = Common.ResolvePaging(CHIT_ID, PAGE_NUMBER, PAGE_SIZE);
            var result = await _repository.GetInventoryVouchersPagedAsync(
                companyCd,
                inputType,
                chitType,
                CHIT_ID,
                NormalizeNullableText(SEARCH_TEXT),
                fromYmd,
                toYmd,
                pageNumber,
                pageSize);

            if (CHIT_ID.HasValue && result.Items.Count == 0)
            {
                return NotFound("Inventory voucher not found");
            }

            return PagedSuccess(result.Items, pageNumber, pageSize, result.TotalRecords);
        }

        [HttpPost("Create")]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] InventoryVoucherRequest? request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationError("Model binding failed", GetModelStateErrors());
            }

            if (request == null)
            {
                return ValidationError("Request is required");
            }

            var inputType = NormalizeInputType(request.INPUT_TYPE, request.CHIT_TYPE);
            var chitType = NormalizeChitType(request.CHIT_TYPE);
            if (inputType == null || chitType == null)
            {
                return ValidationError("INPUT_TYPE or CHIT_TYPE is invalid");
            }

            await EnsureInventoryPermissionAsync(inputType, chitType, "ADD");

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var chitId = await _writeService.CreateAsync(companyCd, inputType, chitType, userId, request);
            var created = (await _repository.GetInventoryVouchersPagedAsync(companyCd, inputType, chitType, chitId, null, null, null, 1, 1)).Items.FirstOrDefault();
            if (created == null)
            {
                return ServerError("Create failed");
            }

            return Created(created, "Created successfully");
        }

        [HttpPut("Update")]
        [Authorize]
        public async Task<IActionResult> Update([FromBody] InventoryVoucherRequest? request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationError("Model binding failed", GetModelStateErrors());
            }

            if (request == null)
            {
                return ValidationError("Request is required");
            }

            if (!request.CHIT_ID.HasValue || request.CHIT_ID.Value <= 0)
            {
                return ValidationError("CHIT_ID is required");
            }

            var inputType = NormalizeInputType(request.INPUT_TYPE, request.CHIT_TYPE);
            var chitType = NormalizeChitType(request.CHIT_TYPE);
            if (inputType == null || chitType == null)
            {
                return ValidationError("INPUT_TYPE or CHIT_TYPE is invalid");
            }

            await EnsureInventoryPermissionAsync(inputType, chitType, "EDIT");

            var companyCd = Common.GetCompanyCode();
            var existing = (await _repository.GetInventoryVouchersPagedAsync(companyCd, inputType, chitType, request.CHIT_ID.Value, null, null, null, 1, 1)).Items.FirstOrDefault();
            if (existing == null)
            {
                return NotFound("Inventory voucher not found");
            }

            var chitId = await _writeService.UpdateAsync(companyCd, inputType, chitType, Common.GetUserId(), existing, request);
            var updated = (await _repository.GetInventoryVouchersPagedAsync(companyCd, inputType, chitType, chitId, null, null, null, 1, 1)).Items.FirstOrDefault();
            if (updated == null)
            {
                return ServerError("Update failed");
            }

            return Updated(updated, "Updated successfully");
        }

        [HttpDelete("Delete")]
        [Authorize]
        public async Task<IActionResult> Delete([FromQuery] string? INPUT_TYPE, [FromQuery] string? CHIT_TYPE, [FromQuery] long CHIT_ID)
        {
            var inputType = NormalizeInputType(INPUT_TYPE, CHIT_TYPE);
            var chitType = NormalizeChitType(CHIT_TYPE);
            if (inputType == null || chitType == null)
            {
                return ValidationError("INPUT_TYPE or CHIT_TYPE is invalid");
            }

            if (CHIT_ID <= 0)
            {
                return ValidationError("CHIT_ID is required");
            }

            await EnsureInventoryPermissionAsync(inputType, chitType, "DELETE");

            var companyCd = Common.GetCompanyCode();
            var existing = (await _repository.GetInventoryVouchersPagedAsync(companyCd, inputType, chitType, CHIT_ID, null, null, null, 1, 1)).Items.FirstOrDefault();
            if (existing == null)
            {
                return NotFound("Inventory voucher not found");
            }

            var result = await _repository.DeleteInventoryVoucherAsync(companyCd, inputType, chitType, CHIT_ID, Common.GetUserId());
            if (result >= 0)
            {
                return Deleted(CHIT_ID, "Deleted successfully");
            }

            return ServerError("Delete failed");
        }

        [HttpGet("ExportExcel")]
        [Authorize]
        public async Task<IActionResult> ExportExcel(
            [FromQuery] string? INPUT_TYPE,
            [FromQuery] string? CHIT_TYPE,
            [FromQuery] long? CHIT_ID = null,
            [FromQuery] string? FROM_YMD = null,
            [FromQuery] string? TO_YMD = null)
        {
            var inputType = NormalizeInputType(INPUT_TYPE, CHIT_TYPE);
            var chitType = NormalizeChitType(CHIT_TYPE);
            if (inputType == null || chitType == null)
            {
                return ValidationError("INPUT_TYPE or CHIT_TYPE is invalid");
            }

            if (CHIT_ID.HasValue && CHIT_ID.Value <= 0)
            {
                return ValidationError("CHIT_ID is invalid");
            }

            await EnsureInventoryPermissionAsync(inputType, chitType, "EXPORT");

            var fromYmd = NormalizeNullableYmdText(FROM_YMD, nameof(FROM_YMD));
            var toYmd = NormalizeNullableYmdText(TO_YMD, nameof(TO_YMD));
            Common.ValidateYmdRange(fromYmd, toYmd);

            var moduleCd = GetInventoryExcelModuleCd(inputType, chitType);
            var templateKeys = await Common.GetExcelTemplateKeysAsync(moduleCd);
            if (templateKeys.Count == 0)
            {
                return ServerError("Excel template is not configured");
            }

            var companyCd = Common.GetCompanyCode();
            var lang = Common.GetCurrentLanguage();
            var result = await _repository.GetInventoryVouchersPagedAsync(
                companyCd,
                inputType,
                chitType,
                CHIT_ID,
                null,
                fromYmd,
                toYmd,
                1,
                int.MaxValue);

            if (result.Items.Count == 0)
            {
                return NotFound((await getLanguage("CHIT_CD", lang)) + " " + (await getLanguage("NO_DATA_TO_EXPORT", lang)));
            }

            var exportKeyList = Common.NormalizeTemplateKeys(templateKeys.Keys);
            var exportRows = BuildInventoryExportRows(result.Items, exportKeyList, chitType);
            var columnMapping = await ChitNoteExcelHelper.GetColumnMappingAsync(templateKeys, lang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(templateKeys.Keys, companyCd, lang);
            var stream = await ExcelHelper.ExportToExcelAsync(exportRows, columnMapping, formatTypes: null, sysCodeDisplayMap: sysCodeDisplayMap);
            var fileName = $"{chitType.ToLowerInvariant()}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private static string GetInventoryExcelModuleCd(string inputType, string chitType)
        {
            return (inputType, chitType) switch
            {
                ("AP", "IR") => "InventoryReceiptVoucherAp",
                ("AR", "IO") => "InventoryIssueVoucherAr",
                ("INV", "IA") => "InventoryAdjustVoucherInv",
                _ => throw new ArgumentException("Inventory voucher type is not supported")
            };
        }

        private static List<IDictionary<string, object?>> BuildInventoryExportRows(
            IEnumerable<InventoryVoucherDto> headers,
            IReadOnlyList<string> templateKeys,
            string chitType)
        {
            var rows = new List<IDictionary<string, object?>>();

            foreach (var header in headers)
            {
                var details = chitType == "IR"
                    ? header.INPUTS.OrderBy(item => item.SORT ?? int.MaxValue).ThenBy(item => item.INPUT_ID ?? long.MaxValue).Cast<object>().ToList()
                    : header.OUTPUTS.OrderBy(item => item.SORT ?? int.MaxValue).ThenBy(item => item.OUTPUT_ID ?? long.MaxValue).Cast<object>().ToList();

                if (details.Count == 0)
                {
                    rows.Add(BuildInventoryExportRow(header, null, templateKeys));
                    continue;
                }

                foreach (var detail in details)
                {
                    rows.Add(BuildInventoryExportRow(header, detail, templateKeys));
                }
            }

            return rows;
        }

        private static IDictionary<string, object?> BuildInventoryExportRow(
            InventoryVoucherDto header,
            object? detail,
            IReadOnlyList<string> templateKeys)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in templateKeys)
            {
                row[key] = GetInventoryExportValue(header, detail, key);
            }

            return row;
        }

        private static object? GetInventoryExportValue(InventoryVoucherDto header, object? detail, string key)
        {
            if (Common.IsDetailTemplateKey(key))
            {
                var detailPropertyName = Common.ResolveInventoryDetailPropertyName(key, detail?.GetType(), true);
                return detailPropertyName == null ? null : Common.GetPropertyValue(detail, detailPropertyName);
            }

            var headerValue = Common.GetPropertyValue(header, key);
            if (headerValue != null)
            {
                return headerValue;
            }

            var fallbackDetailPropertyName = Common.ResolveInventoryDetailPropertyName(key, detail?.GetType(), false);
            return fallbackDetailPropertyName == null ? null : Common.GetPropertyValue(detail, fallbackDetailPropertyName);
        }

        private async Task EnsureInventoryPermissionAsync(string inputType, string chitType, string permissionKey)
        {
            if (!PermissionMap.TryGetValue($"{inputType}:{chitType}", out var permissionMenuCode))
            {
                throw new ArgumentException("Inventory voucher type is not supported");
            }

            await EnsurePermissionAsync(permissionMenuCode, permissionKey);
        }
    }
}

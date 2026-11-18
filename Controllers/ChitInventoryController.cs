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

        [HttpGet("GetBomSnapshot")]
        [Authorize]
        public async Task<IActionResult> GetBomSnapshot([FromQuery] long CHIT_ID)
        {
            if (CHIT_ID <= 0) return ValidationError("CHIT_ID is invalid");
            await EnsureInventoryPermissionAsync("AR", "IO", "VIEW");
            var companyCd = Common.GetCompanyCode();
            var voucher = await _repository.GetInventoryVouchersPagedAsync(companyCd, "AR", "IO", CHIT_ID);
            if (voucher.Items.Count == 0) return NotFound("Inventory voucher not found");
            return Success(await _repository.GetInventoryBomSnapshotAsync(companyCd, CHIT_ID));
        }

        [HttpGet("GetLatestInputStoresByProductIds")]
        [Authorize]
        public async Task<IActionResult> GetLatestInputStoresByProductIds([FromQuery] string? PRODUCT_IDS)
        {
            var productIds = Common.ParsePositiveIds(PRODUCT_IDS);
            if (productIds.Count == 0)
                return Success(Array.Empty<object>());

            await EnsureInventoryPermissionAsync("AR", "IO", "VIEW");
            var items = await _repository.GetLatestInputStoresByProductIdsAsync(Common.GetCompanyCode(), productIds);
            return Success(items);
        }

        [HttpGet("GetIssueLinksByCogsVoucher")]
        [Authorize]
        public async Task<IActionResult> GetIssueLinksByCogsVoucher([FromQuery] long CHIT_ID)
        {
            if (CHIT_ID <= 0) return ValidationError("CHIT_ID is invalid");
            await EnsureInventoryPermissionAsync("AR", "IO", "VIEW");
            return Success(await _repository.GetInventoryIssueLinksByCogsVoucherIdAsync(Common.GetCompanyCode(), CHIT_ID));
        }

        [HttpGet("GetIssueByCogsVoucher")]
        [Authorize]
        public async Task<IActionResult> GetIssueByCogsVoucher([FromQuery] long CHIT_ID, [FromQuery] long? CHITDETAIL_ID)
        {
            if (CHIT_ID <= 0 || CHITDETAIL_ID is <= 0)
            {
                return ValidationError("CHIT_ID is invalid");
            }

            await EnsureInventoryPermissionAsync("AR", "IO", "VIEW");

            var inventoryChitId = await _repository.GetInventoryIssueIdByCogsVoucherIdAsync(
                Common.GetCompanyCode(),
                CHIT_ID, CHITDETAIL_ID);

            return Success(new { INVENTORY_CHIT_ID = inventoryChitId });
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

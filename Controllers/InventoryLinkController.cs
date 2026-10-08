using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    public class InventoryLinkController : BaseApiController
    {
        private readonly IInventoryLinkRepository _repository;
        private readonly ILogger<InventoryLinkController> _logger;

        private static readonly Dictionary<string, string> PermissionMap = new()
        {
            ["AP:PO"] = "AP_PURCHASE_GOODS",
            ["AP:PD"] = "AP_PURCHASE_DISCOUNT",
            ["AP:PR"] = "AP_RETURN_GOODS",
            ["AR:SO"] = "AR_SALES",
            ["AR:SD"] = "AR_SALE_DISCOUNT",
            ["AR:SR"] = "AR_SALE_RETURN",
            ["AP:IR"] = "INV_RECEIPT",
            ["AR:IO"] = "INV_ISSUE"
        };

        public InventoryLinkController(IInventoryLinkRepository repository, ILogger<InventoryLinkController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("GetStatus")]
        [Authorize]
        public async Task<IActionResult> GetStatus([FromQuery] string? INPUT_TYPE, [FromQuery] string? CHIT_TYPE, [FromQuery] long CHIT_ID)
        {
            var inputType = Common.NormalizeInputType(INPUT_TYPE, CHIT_TYPE);
            var chitType = Common.NormalizeChitType(CHIT_TYPE);
            if (inputType == null || chitType == null)
            {
                return ValidationError("INPUT_TYPE or CHIT_TYPE is invalid");
            }

            if (CHIT_ID <= 0)
            {
                return ValidationError("CHIT_ID is required");
            }

            await EnsureLinkPermissionAsync(inputType, chitType, "VIEW");

            var status = await _repository.GetInventoryLinkStatusAsync(Common.GetCompanyCode(), chitType, CHIT_ID);
            if (status == null)
            {
                return NotFound("Voucher not found");
            }

            return Success(status);
        }

        [HttpGet("GetStatusByChitIds")]
        [Authorize]
        public async Task<IActionResult> GetStatusByChitIds([FromQuery] string? INPUT_TYPE, [FromQuery] string? CHIT_TYPE, [FromQuery] string? CHIT_IDS)
        {
            var inputType = Common.NormalizeInputType(INPUT_TYPE, CHIT_TYPE);
            var chitType = Common.NormalizeChitType(CHIT_TYPE);
            if (inputType == null || chitType == null)
            {
                return ValidationError("INPUT_TYPE or CHIT_TYPE is invalid");
            }

            await EnsureLinkPermissionAsync(inputType, chitType, "VIEW");

            var chitIds = Common.ParsePositiveIds(CHIT_IDS);
            if (chitIds.Count == 0)
            {
                return Success(new List<object>());
            }

            var statuses = await _repository.GetInventoryLinkStatusesAsync(Common.GetCompanyCode(), chitType, chitIds);
            return Success(statuses);
        }

        [HttpGet("GetInputsBySourceDetailIds")]
        [Authorize]
        public async Task<IActionResult> GetInputsBySourceDetailIds([FromQuery] string? CHITDETAIL_IDS)
        {
            var ids = Common.ParsePositiveIds(CHITDETAIL_IDS);
            if (ids.Count == 0)
            {
                return ValidationError("CHITDETAIL_IDS is invalid");
            }

            var items = await _repository.GetInventoryInputsByChitDetailIdsAsync(Common.GetCompanyCode(), ids);
            return Success(items);
        }

        [HttpGet("GetOutputsBySourceDetailIds")]
        [Authorize]
        public async Task<IActionResult> GetOutputsBySourceDetailIds([FromQuery] string? CHITDETAIL_IDS)
        {
            var ids = Common.ParsePositiveIds(CHITDETAIL_IDS);
            if (ids.Count == 0)
            {
                return ValidationError("CHITDETAIL_IDS is invalid");
            }

            var items = await _repository.GetInventoryOutputsByChitDetailIdsAsync(Common.GetCompanyCode(), ids);
            return Success(items);
        }

        [HttpGet("GetInputsByChitIds")]
        [Authorize]
        public async Task<IActionResult> GetInputsByChitIds([FromQuery] string? CHIT_IDS)
        {
            var ids = Common.ParsePositiveIds(CHIT_IDS);
            if (ids.Count == 0)
            {
                return Success(new List<object>());
            }

            var items = await _repository.GetInventoryInputsByChitIdsAsync(Common.GetCompanyCode(), ids);
            return Success(items);
        }

        [HttpGet("GetOutputsByChitIds")]
        [Authorize]
        public async Task<IActionResult> GetOutputsByChitIds([FromQuery] string? CHIT_IDS)
        {
            var ids = Common.ParsePositiveIds(CHIT_IDS);
            if (ids.Count == 0)
            {
                return Success(new List<object>());
            }

            var items = await _repository.GetInventoryOutputsByChitIdsAsync(Common.GetCompanyCode(), ids);
            return Success(items);
        }

        [HttpGet("GetSourceVoucher")]
        [Authorize]
        public async Task<IActionResult> GetSourceVoucher([FromQuery] long CHITDETAIL_ID)
        {
            if (CHITDETAIL_ID <= 0)
            {
                return ValidationError("CHITDETAIL_ID is required");
            }

            var item = await _repository.GetInventorySourceVoucherByDetailIdAsync(Common.GetCompanyCode(), CHITDETAIL_ID);
            if (item == null)
            {
                return NotFound("Source voucher not found");
            }

            return Success(item);
        }

        private async Task EnsureLinkPermissionAsync(string inputType, string chitType, string permissionKey)
        {
            if (!PermissionMap.TryGetValue($"{inputType}:{chitType}", out var permissionMenuCode))
            {
                throw new ArgumentException("Voucher type is not supported");
            }

            await EnsurePermissionAsync(permissionMenuCode, permissionKey);
        }

    }
}
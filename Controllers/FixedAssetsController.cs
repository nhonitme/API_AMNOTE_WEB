using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/fixed-assets")]
    [Authorize]
    public class FixedAssetsController : BaseApiController
    {
        private const string FaStatusCodeType = "FA_STATUS";

        private readonly IFixedAssetRepository _repository;
        private readonly IFixedAssetDepreciationService _depreciationService;
        private readonly ISystemService _systemService;
        private readonly ILogger<FixedAssetsController> _logger;

        public FixedAssetsController(
            IFixedAssetRepository repository,
            IFixedAssetDepreciationService depreciationService,
            ISystemService systemService,
            ILogger<FixedAssetsController> logger)
        {
            _repository = repository;
            _depreciationService = depreciationService;
            _systemService = systemService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] string? status, [FromQuery] string? accCd)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repository.GetListAsync(companyCd, status, accCd);
            return Success(data);
        }

        [HttpGet("{assetId:long}")]
        public async Task<IActionResult> GetById(long assetId)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repository.GetByIdAsync(companyCd, assetId);
            if (data is null)
            {
                return NotFound("Không tìm thấy tài sản.");
            }

            return Success(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FixedAssetSaveRequest? request)
        {
            if (request?.ASSET is null)
            {
                return ValidationError("Request body must be provided");
            }

            request.ASSET.COMPANY_CD = Common.GetCompanyCode();
            var assetId = await _repository.CreateAsync(request, Common.GetUserId());
            return Created(new { ASSET_ID = assetId }, "Đã tạo tài sản cố định.");
        }

        [HttpPut("{assetId:long}")]
        public async Task<IActionResult> Update(long assetId, [FromBody] FixedAssetSaveRequest? request)
        {
            if (request?.ASSET is null)
            {
                return ValidationError("Request body must be provided");
            }

            request.ASSET.COMPANY_CD = Common.GetCompanyCode();
            await _repository.UpdateAsync(assetId, request, Common.GetUserId());
            return Updated(true, "Đã cập nhật tài sản cố định.");
        }

        [HttpDelete("{assetId:long}")]
        public async Task<IActionResult> Delete(long assetId)
        {
            var companyCd = Common.GetCompanyCode();
            await _repository.DeleteAsync(companyCd, assetId, Common.GetUserId());
            return Deleted(true, "Đã xóa tài sản cố định.");
        }

        [HttpPost("depreciation/preview")]
        public IActionResult PreviewDepreciation([FromBody] FixedAssetDepreciationPreviewRequest? request)
        {
            if (request is null)
            {
                return ValidationError("Request body must be provided");
            }

            try
            {
                var result = _depreciationService.Preview(request);
                return Success(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Fixed asset depreciation preview validation failed.");
                return ValidationError(ex.Message);
            }
        }

    }
}

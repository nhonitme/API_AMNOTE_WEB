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

        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] long? assetId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? accCd = null,
            [FromQuery] string? lang = null)
        {
            var currentLang = Common.NormalizeLanguageCode(lang ?? Common.GetCurrentLanguage());
            var companyCd = Common.GetCompanyCode();
            var data = (await _repository.GetListAsync(companyCd, status, accCd)).ToList();

            if (assetId is > 0)
            {
                data = data.Where(x => x.ASSET_ID == assetId.Value).ToList();
            }

            if (data.Count == 0)
            {
                return NotFound("Không có dữ liệu để xuất Excel.");
            }

            const string screenCd = "FA_REGISTER";
            const string gridId = "fixed-asset-grid";
            var exportColumns = await Common.GetExcelExportColumnInfosAsync("FixedAssetInfo", screenCd, gridId);
            await ApplyFaStatusDisplayTextAsync(data, companyCd, currentLang);

            var columnMapping = await Common.BuildExcelColumnMappingAsync(exportColumns, currentLang);
            var sysCodeDisplayMap = await Common.BuildExcelSysCodeDisplayMapAsync(exportColumns, companyCd, currentLang);
            var formatTypes = await Common.GetExcelExportFormatTypesAsync(gridId);
            var stream = await ExcelHelper.ExportToExcelAsync(data, columnMapping, formatTypes, sysCodeDisplayMap);
            var fileName = $"fixed_asset_register_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        private async Task ApplyFaStatusDisplayTextAsync(
            IList<FixedAssetListItemDto> rows,
            string companyCd,
            string language)
        {
            if (rows.Count == 0)
            {
                return;
            }

            var codes = await _systemService.GetSysCodesAsync(companyCd, FaStatusCodeType);
            var displayByCd = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in codes ?? Enumerable.Empty<Models.SysCodeInfo>())
            {
                var codeCd = Common.NormalizeNullableText(code.CODE_CD);
                if (codeCd == null || displayByCd.ContainsKey(codeCd))
                {
                    continue;
                }

                displayByCd[codeCd] = ReportLanguageHelper.ResolveFaStatusDisplayText(
                    codeCd,
                    code.CODE_NAME,
                    language);
            }

            foreach (var row in rows)
            {
                var status = ReportLanguageHelper.NormalizeFaStatusCode(row.STATUS);
                if (status != null && displayByCd.TryGetValue(status, out var display) && !string.IsNullOrWhiteSpace(display))
                {
                    row.STATUS_TEXT = display;
                    continue;
                }

                row.STATUS_TEXT = ReportLanguageHelper.ResolveFaStatusDisplayText(status, null, language);
            }
        }
    }
}

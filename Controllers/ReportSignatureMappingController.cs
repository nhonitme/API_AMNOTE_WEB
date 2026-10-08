using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public sealed class ReportSignatureMappingController : BaseApiController
    {
        private readonly IReportSignatureMappingRepository _repository;

        public ReportSignatureMappingController(
            IReportSignatureMappingRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        [HttpGet("{reportKey}")]
        [Authorize]
        public async Task<IActionResult> GetReportSignatureMapping([FromRoute] string reportKey, [FromQuery] string? reportCode = null)
        {
            var companyCd = Common.GetCompanyCode();
            var data = await _repository.GetReportSignatureMappingAsync(companyCd, reportKey, reportCode);
            if (data == null)
            {
                return NotFound("Report signature mapping not found");
            }

            return Success(MapDto(data));
        }

        [HttpPut("{reportKey}")]
        [Authorize]
        public async Task<IActionResult> SaveReportSignatureMapping([FromRoute] string reportKey, [FromBody] SaveReportSignatureMappingRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var companyCd = Common.GetCompanyCode();
            var saved = await _repository.SaveReportSignatureMappingAsync(
                companyCd,
                reportKey,
                request.REPORT_CODE,
                request.SIGN_CODES,
                Common.GetUserId());

            return Updated(MapDto(saved), "Updated successfully");
        }

        private static ReportSignatureMappingDto MapDto(ReportSignatureMappingInfo source)
        {
            return new ReportSignatureMappingDto
            {
                MAPPING_ID = source.MAPPING_ID,
                COMPANY_CD = source.COMPANY_CD,
                REPORT_KEY = source.REPORT_KEY,
                REPORT_CODE = source.REPORT_CODE,
                REPORT_NAME = source.REPORT_NAME,
                REPORT_ID = source.REPORT_ID,
                SIGN_IDS = source.SIGN_IDS,
                SIGNATURES = source.SIGNATURES.Select(MapSignatureDto).ToList()
            };
        }

        private static ReportSignatureMappingSignatureDto MapSignatureDto(ReportSignatureMappingSignatureInfo source)
        {
            return new ReportSignatureMappingSignatureDto
            {
                ID = source.ID,
                COMPANY_CD = source.COMPANY_CD,
                SIGN_CODE = source.SIGN_CODE,
                DISPLAY_LABEL = source.DISPLAY_LABEL,
                SIGN_NAME = source.SIGN_NAME,
                SIGN_TITLE = source.SIGN_TITLE,
                SIGN_IMAGE_URL = source.SIGN_IMAGE_URL,
                SORT_ORDER = source.SORT_ORDER,
                IS_ACTIVE = source.IS_ACTIVE,
                IS_SELECTED = source.IS_SELECTED,
                SELECTED_ORDER = source.SELECTED_ORDER
            };
        }
    }
}

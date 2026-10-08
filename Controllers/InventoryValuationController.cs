using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.InventoryValuation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/inventory/valuation")]
    [Authorize]
    public class InventoryValuationController : BaseApiController
    {
        private readonly InventoryValuationJobStore _jobStore;

        public InventoryValuationController(InventoryValuationJobStore jobStore)
        {
            _jobStore = jobStore;
        }

        [HttpPost("jobs/start")]
        public async Task<IActionResult> StartJob([FromBody] InventoryValuationRequest? request)
        {
            var validationError = ValidateRequest(request, out var normalized);
            if (validationError != null)
            {
                return validationError;
            }

            var result = await _jobStore.StartAsync(
                normalized.CompanyCd,
                normalized.DatabaseName,
                normalized.FromYmd,
                normalized.ToYmd,
                normalized.MethodCode,
                normalized.ProductCds,
                normalized.StoreCds,
                Common.GetUserId());

            return Created(result, result.Message);
        }

        [HttpGet("jobs/progress")]
        public IActionResult GetJobProgress([FromQuery] string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return ValidationError("JobId is required");
            }

            var companyCd = Common.GetCompanyCode();
            var state = _jobStore.Get(jobId);
            if (state == null || !string.Equals(state.CompanyCd, companyCd, StringComparison.OrdinalIgnoreCase))
            {
                var lang = Common.GetCurrentLanguage();
                return NotFound(lang == "ENG"
                    ? "Inventory valuation job progress not found"
                    : "Không tìm thấy tiến trình tính giá xuất kho");
            }

            var progress = _jobStore.GetProgress(jobId);
            if (progress == null)
            {
                return NotFound("Inventory valuation job progress not found");
            }

            return Success(progress);
        }

        private IActionResult? ValidateRequest(InventoryValuationRequest? request, out NormalizedInventoryValuationRequest normalized)
        {
            normalized = new NormalizedInventoryValuationRequest();

            if (request == null)
            {
                return ValidationError("Request is required");
            }

            normalized.CompanyCd = Common.GetCompanyCode();
            normalized.DatabaseName = Common.GetDatabaseName();
            normalized.FromYmd = Common.NormalizeNullableYmdText(request.FROM_YMD, nameof(request.FROM_YMD)) ?? string.Empty;
            normalized.ToYmd = Common.NormalizeNullableYmdText(request.TO_YMD, nameof(request.TO_YMD)) ?? string.Empty;
            normalized.ProductCds = Common.NormalizeNullableText(request.PRODUCT_CDS);
            normalized.StoreCds = Common.NormalizeNullableText(request.STORE_CDS);
            normalized.MethodCode = InventoryValuationMethod.Normalize(request.METHOD_CODE);

            if (string.IsNullOrWhiteSpace(normalized.FromYmd) || string.IsNullOrWhiteSpace(normalized.ToYmd))
            {
                throw new ArgumentException("FROM_YMD and TO_YMD are required");
            }

            if (string.CompareOrdinal(normalized.FromYmd, normalized.ToYmd) > 0)
            {
                throw new ArgumentException("FROM_YMD must be less than or equal to TO_YMD");
            }

            return null;
        }

        private sealed class NormalizedInventoryValuationRequest
        {
            public string CompanyCd { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
            public string FromYmd { get; set; } = string.Empty;
            public string ToYmd { get; set; } = string.Empty;
            public string? ProductCds { get; set; }
            public string? StoreCds { get; set; }
            public string MethodCode { get; set; } = InventoryValuationMethod.PeriodEndAverage;
        }
    }
}

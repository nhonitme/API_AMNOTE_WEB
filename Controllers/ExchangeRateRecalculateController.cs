using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExchangeRevaluation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/exchange-rate/recalculate")]
    [Authorize]
    public class ExchangeRateRecalculateController : BaseApiController
    {
        private static readonly string[] AllowedModules = { "AR", "AP", "ALL" };
        private readonly IExchangeRevaluationRepository _repository;
        private readonly ExchangeRevaluationJobStore _jobStore;

        public ExchangeRateRecalculateController(
            IExchangeRevaluationRepository repository,
            ExchangeRevaluationJobStore jobStore)
        {
            _repository = repository;
            _jobStore = jobStore;
        }

        [HttpPost("jobs/start")]
        public async Task<IActionResult> StartJob([FromBody] ExchangeRevaluationPreviewRequest? request)
        {
            var validationError = ValidateJobRequest(request, out var normalized);
            if (validationError != null)
            {
                return validationError;
            }

            var jobRequest = new ExchangeRevaluationJobRequest
            {
                JobId = Guid.NewGuid().ToString("N"),
                CompanyCd = normalized.CompanyCd,
                DatabaseName = normalized.DatabaseName,
                UserId = Common.GetUserId(),
                ModuleCd = normalized.ModuleCd,
                RateDate = normalized.RateDate,
                FcType = normalized.FcType,
                ChitYmdFrom = normalized.ChitYmdFrom,
                ChitYmdTo = normalized.ChitYmdTo,
                CreatedAt = DateTime.Now
            };

            var result = await _jobStore.StartAsync(jobRequest);
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
                    ? "Exchange revaluation job progress not found"
                    : "Không tìm thấy tiến trình tính lại tỷ giá ngân hàng");
            }

            var progress = _jobStore.GetProgress(jobId);
            if (progress == null)
            {
                return NotFound("Exchange revaluation job progress not found");
            }

            return Success(progress);
        }

        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] ExchangeRevaluationPreviewRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Request is required");
            }

            var companyCd = Common.GetCompanyCode();
            var databaseName = Common.GetDatabaseName();
            var moduleCd = NormalizeModuleCd(request.MODULE_CD);
            var rateDate = Common.NormalizeRequiredDate(request.RATE_DATE, "RATE_DATE");
            var fcType = Common.NormalizeUpperText(request.FC_TYPE);
            var fromYmd = Common.NormalizeNullableYmdText(request.CHIT_YMD_FROM, "CHIT_YMD_FROM");
            var toYmd = Common.NormalizeNullableYmdText(request.CHIT_YMD_TO, "CHIT_YMD_TO");

            Common.ValidateYmdRange(fromYmd, toYmd);

            var rows = (await _repository.PreviewAsync(companyCd, moduleCd, rateDate, databaseName, fcType, fromYmd, toYmd)).ToList();
            return Success(new
            {
                data = rows,
                summary = Common.BuildExchangeRevaluationSummary(rows, item =>
                {
                    var obj = (object)item;
                    return obj is ExchangeRevaluationPreviewItem preview ? preview.DIFF_TYPE : obj is ExchangeRevaluationHistoryItem history ? history.DIFF_TYPE : string.Empty;
                })
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> History(
            [FromQuery] string? MODULE_CD = null,
            [FromQuery] string? RATE_DATE_FROM = null,
            [FromQuery] string? RATE_DATE_TO = null,
            [FromQuery] string? FC_TYPE = null)
        {
            var companyCd = Common.GetCompanyCode();
            var databaseName = Common.GetDatabaseName();
            var moduleCd = NormalizeModuleCd(MODULE_CD, true);
            var rateDateFrom = Common.NormalizeNullableDate(RATE_DATE_FROM, "RATE_DATE_FROM");
            var rateDateTo = Common.NormalizeNullableDate(RATE_DATE_TO, "RATE_DATE_TO");
            var fcType = Common.NormalizeUpperText(FC_TYPE);

            Common.ValidateDateRange(rateDateFrom, rateDateTo);

            var rows = (await _repository.GetHistoryAsync(companyCd, moduleCd, rateDateFrom, rateDateTo, fcType, databaseName)).ToList();
            return Success(new
            {
                data = rows,
                summary = Common.BuildExchangeRevaluationSummary(rows, item =>
                {
                    var obj = (object)item;
                    return obj is ExchangeRevaluationPreviewItem preview ? preview.DIFF_TYPE : obj is ExchangeRevaluationHistoryItem history ? history.DIFF_TYPE : string.Empty;
                })
            });
        }

        [HttpGet("currencies")]
        public async Task<IActionResult> GetCurrencies(
            [FromQuery] string? MODULE_CD = null)
        {
            var companyCd = Common.GetCompanyCode();
            var databaseName = Common.GetDatabaseName();
            var moduleCd = NormalizeModuleCd(MODULE_CD, true);
            var rows = (await _repository.GetCurrenciesAsync(companyCd, moduleCd, databaseName)).ToList();
            return Success(new { data = rows });
        }

        private IActionResult? ValidateJobRequest(
            ExchangeRevaluationPreviewRequest? request,
            out NormalizedExchangeRevaluationJobRequest normalized)
        {
            normalized = new NormalizedExchangeRevaluationJobRequest();

            if (request == null)
            {
                return ValidationError("Request is required");
            }

            normalized.CompanyCd = Common.GetCompanyCode();
            normalized.DatabaseName = Common.GetDatabaseName();
            normalized.ModuleCd = NormalizeModuleCd(request.MODULE_CD);
            normalized.RateDate = Common.NormalizeRequiredDate(request.RATE_DATE, "RATE_DATE");
            normalized.FcType = Common.NormalizeUpperText(request.FC_TYPE);
            normalized.ChitYmdFrom = Common.NormalizeNullableYmdText(request.CHIT_YMD_FROM, "CHIT_YMD_FROM");
            normalized.ChitYmdTo = Common.NormalizeNullableYmdText(request.CHIT_YMD_TO, "CHIT_YMD_TO");

            Common.ValidateYmdRange(normalized.ChitYmdFrom, normalized.ChitYmdTo);

            if (string.IsNullOrWhiteSpace(normalized.FcType))
            {
                throw new ArgumentException("FC_TYPE is required");
            }

            return null;
        }

        private static string? NormalizeModuleCd(string? moduleCd, bool allowEmpty = false)
        {
            var normalized = Common.NormalizeUpperText(moduleCd);
            if (normalized == null)
            {
                return allowEmpty ? null : "ALL";
            }

            if (!AllowedModules.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("MODULE_CD must be AR, AP or ALL");
            }

            return normalized;
        }

        private sealed class NormalizedExchangeRevaluationJobRequest
        {
            public string CompanyCd { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
            public string? ModuleCd { get; set; }
            public DateTime RateDate { get; set; }
            public string? FcType { get; set; }
            public string? ChitYmdFrom { get; set; }
            public string? ChitYmdTo { get; set; }
        }
    }
}

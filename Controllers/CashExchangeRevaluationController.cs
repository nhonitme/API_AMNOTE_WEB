using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.CashExchangeRevaluation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Route("api/cash/exchange-rate/recalculate")]
    [Authorize]
    public class CashExchangeRevaluationController : BaseApiController
    {
        private readonly ICashExchangeRevaluationRepository _repository;
        private readonly CashExchangeRevaluationJobStore _jobStore;

        public CashExchangeRevaluationController(
            ICashExchangeRevaluationRepository repository,
            CashExchangeRevaluationJobStore jobStore)
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

            var jobRequest = new CashExchangeRevaluationJobRequest
            {
                JobId = Guid.NewGuid().ToString("N"),
                CompanyCd = normalized.CompanyCd,
                DatabaseName = normalized.DatabaseName,
                UserId = Common.GetUserId(),
                RateDate = normalized.RateDate,
                FcType = normalized.FcType,
                ChitYmdFrom = normalized.ChitYmdFrom,
                ChitYmdTo = normalized.ChitYmdTo,
                RateMethod = normalized.RateMethod,
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
                    ? "Cash exchange revaluation job progress not found"
                    : "Không tìm thấy tiến trình tính lại tỷ giá xuất quỹ");
            }

            var progress = _jobStore.GetProgress(jobId);
            if (progress == null)
            {
                return NotFound("Cash exchange revaluation job progress not found");
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
            var rateDate = Common.NormalizeRequiredDate(request.RATE_DATE, "RATE_DATE");
            var fcType = Common.NormalizeUpperText(request.FC_TYPE);
            var fromYmd = Common.NormalizeNullableYmdText(request.CHIT_YMD_FROM, "CHIT_YMD_FROM");
            var toYmd = Common.NormalizeNullableYmdText(request.CHIT_YMD_TO, "CHIT_YMD_TO");
            var rateMethod = Common.NormalizeRateMethod(request.RATE_METHOD);

            Common.ValidateYmdRange(fromYmd, toYmd);

            var rows = (await _repository.PreviewAsync(companyCd, rateDate, databaseName, fcType, fromYmd, toYmd, rateMethod)).ToList();
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
            [FromQuery] string? RATE_DATE_FROM = null,
            [FromQuery] string? RATE_DATE_TO = null,
            [FromQuery] string? FC_TYPE = null)
        {
            var companyCd = Common.GetCompanyCode();
            var databaseName = Common.GetDatabaseName();
            var rateDateFrom = Common.NormalizeNullableDate(RATE_DATE_FROM, "RATE_DATE_FROM");
            var rateDateTo = Common.NormalizeNullableDate(RATE_DATE_TO, "RATE_DATE_TO");
            var fcType = Common.NormalizeUpperText(FC_TYPE);

            Common.ValidateDateRange(rateDateFrom, rateDateTo);

            var rows = (await _repository.GetHistoryAsync(companyCd, rateDateFrom, rateDateTo, fcType, databaseName)).ToList();
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
        public async Task<IActionResult> GetCurrencies()
        {
            var companyCd = Common.GetCompanyCode();
            var databaseName = Common.GetDatabaseName();
            var rows = (await _repository.GetCurrenciesAsync(companyCd, databaseName)).ToList();
            return Success(new { data = rows });
        }

        private IActionResult? ValidateJobRequest(
            ExchangeRevaluationPreviewRequest? request,
            out NormalizedCashExchangeRevaluationJobRequest normalized)
        {
            normalized = new NormalizedCashExchangeRevaluationJobRequest();

            if (request == null)
            {
                return ValidationError("Request is required");
            }

            normalized.CompanyCd = Common.GetCompanyCode();
            normalized.DatabaseName = Common.GetDatabaseName();
            normalized.RateDate = Common.NormalizeRequiredDate(request.RATE_DATE, "RATE_DATE");
            normalized.FcType = Common.NormalizeUpperText(request.FC_TYPE);
            normalized.ChitYmdFrom = Common.NormalizeNullableYmdText(request.CHIT_YMD_FROM, "CHIT_YMD_FROM");
            normalized.ChitYmdTo = Common.NormalizeNullableYmdText(request.CHIT_YMD_TO, "CHIT_YMD_TO");
            normalized.RateMethod = Common.NormalizeRateMethod(request.RATE_METHOD);

            Common.ValidateYmdRange(normalized.ChitYmdFrom, normalized.ChitYmdTo);

            if (string.IsNullOrWhiteSpace(normalized.FcType))
            {
                throw new ArgumentException("FC_TYPE is required");
            }

            return null;
        }

        private sealed class NormalizedCashExchangeRevaluationJobRequest
        {
            public string CompanyCd { get; set; } = string.Empty;
            public string DatabaseName { get; set; } = string.Empty;
            public DateTime RateDate { get; set; }
            public string? FcType { get; set; }
            public string? ChitYmdFrom { get; set; }
            public string? ChitYmdTo { get; set; }
            public string? RateMethod { get; set; }
        }
    }
}

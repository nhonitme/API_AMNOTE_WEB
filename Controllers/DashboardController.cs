using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class DashboardController : BaseApiController
    {
        private readonly IDashboardRepository _repository;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IDashboardRepository repository, ILogger<DashboardController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("kpi")]
        [Authorize]
        public async Task<IActionResult> GetKpi(
            [FromQuery] string? fromYmd = null,
            [FromQuery] string? toYmd   = null)
        {
            var companyCd = Common.GetCompanyCode();
            var today     = DateTime.Today;
            var from      = fromYmd ?? new DateTime(today.Year, today.Month, 1).ToString("yyyyMMdd");
            var to        = toYmd   ?? today.ToString("yyyyMMdd");

            var data = await _repository.GetKpiAsync(companyCd, from, to);
            return Success(data);
        }

        [HttpGet("chart")]
        [Authorize]
        public async Task<IActionResult> GetChart([FromQuery] string? year = null)
        {
            var companyCd  = Common.GetCompanyCode();
            var targetYear = year ?? DateTime.Today.Year.ToString();

            var data = await _repository.GetChartAsync(companyCd, targetYear);
            return Success(data);
        }

        [HttpGet("tasks")]
        [Authorize]
        public async Task<IActionResult> GetTasks(
            [FromQuery] string? fromYmd = null,
            [FromQuery] string? toYmd   = null)
        {
            var companyCd = Common.GetCompanyCode();
            var today     = DateTime.Today;
            var from      = fromYmd ?? new DateTime(today.Year, today.Month, 1).ToString("yyyyMMdd");
            var to        = toYmd   ?? today.ToString("yyyyMMdd");

            var data = await _repository.GetTasksAsync(companyCd, from, to);
            return Success(data);
        }

        [HttpGet("receivables")]
        [Authorize]
        public async Task<IActionResult> GetReceivables(
            [FromQuery] string? toYmd = null,
            [FromQuery] int     top   = 10)
        {
            var companyCd = Common.GetCompanyCode();
            var to        = toYmd ?? DateTime.Today.ToString("yyyyMMdd");

            var data = await _repository.GetReceivablesAsync(companyCd, to, Math.Clamp(top, 1, 50));
            return Success(data);
        }

        [HttpGet("payables")]
        [Authorize]
        public async Task<IActionResult> GetPayables(
            [FromQuery] string? toYmd = null,
            [FromQuery] int     top   = 10)
        {
            var companyCd = Common.GetCompanyCode();
            var to        = toYmd ?? DateTime.Today.ToString("yyyyMMdd");

            var data = await _repository.GetPayablesAsync(companyCd, to, Math.Clamp(top, 1, 50));
            return Success(data);
        }

        [HttpGet("tax-deadlines")]
        [Authorize]
        public async Task<IActionResult> GetTaxDeadlines([FromQuery] string? periodYm = null)
        {
            var companyCd    = Common.GetCompanyCode();
            var targetPeriod = periodYm ?? DateTime.Today.ToString("yyyyMM");

            var data = await _repository.GetTaxDeadlinesAsync(companyCd, targetPeriod);
            return Success(data);
        }

        [HttpGet("period-lock")]
        [Authorize]
        public async Task<IActionResult> GetPeriodLock([FromQuery] string? periodYm = null)
        {
            var companyCd    = Common.GetCompanyCode();
            var targetPeriod = periodYm ?? DateTime.Today.ToString("yyyyMM");

            var data = await _repository.GetPeriodLockAsync(companyCd, targetPeriod);
            return Success(data);
        }

        [HttpGet("recent-vouchers")]
        [Authorize]
        public async Task<IActionResult> GetRecentVouchers(
            [FromQuery] string? fromYmd = null,
            [FromQuery] string? toYmd   = null,
            [FromQuery] string? status  = null,
            [FromQuery] int     limit   = 20)
        {
            var companyCd = Common.GetCompanyCode();
            var today     = DateTime.Today;
            var from      = fromYmd ?? new DateTime(today.Year, today.Month, 1).ToString("yyyyMMdd");
            var to        = toYmd   ?? today.ToString("yyyyMMdd");

            var data = await _repository.GetRecentVouchersAsync(companyCd, from, to, status, Math.Clamp(limit, 1, 100));
            return Success(data);
        }
    }
}

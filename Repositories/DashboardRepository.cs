using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private const int DashboardCommandTimeoutSeconds = 20;
        private readonly DapperExecutor _db;
        private readonly ILogger<DashboardRepository> _logger;

        public DashboardRepository(DapperExecutor db, ILogger<DashboardRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<DashboardKpiDto?> GetKpiAsync(string companyCd, string fromYmd, string toYmd)
        {
            const string sql = "CALL getDashboardKpi(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD)";
            var results = await _db.QueryAsync<DashboardKpiDto>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd
            }, commandTimeout: DashboardCommandTimeoutSeconds);
            return results.FirstOrDefault();
        }

        public async Task<IEnumerable<DashboardChartItemDto>> GetChartAsync(string companyCd, string year)
        {
            const string sql = "CALL getDashboardChart(@p_COMPANY_CD, @p_YEAR)";
            return await _db.QueryAsync<DashboardChartItemDto>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_YEAR = year
            }, commandTimeout: DashboardCommandTimeoutSeconds);
        }

        public async Task<IEnumerable<DashboardTaskItemDto>> GetTasksAsync(string companyCd, string fromYmd, string toYmd)
        {
            const string sql = "CALL getDashboardTasks(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD)";
            var rows = await _db.QueryAsync<dynamic>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd
            }, commandTimeout: DashboardCommandTimeoutSeconds);

            return rows.Select(r => new DashboardTaskItemDto
            {
                Category = (string)(r.CATEGORY ?? ""),
                Label = (string)(r.LABEL ?? ""),
                Severity = (string)(r.SEVERITY ?? "info"),
                ActionUrl = (string?)(r.ACTION_URL),
                Count = (int)(r.CNT ?? 0)
            }).ToList();
        }

        public async Task<IEnumerable<DashboardReceivableDto>> GetReceivablesAsync(string companyCd, string toYmd, int top = 10)
        {
            const string sql = "CALL getDashboardReceivables(@p_COMPANY_CD, @p_TO_YMD, @p_TOP)";
            return await _db.QueryAsync<DashboardReceivableDto>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_TO_YMD = toYmd,
                p_TOP = top
            }, commandTimeout: DashboardCommandTimeoutSeconds);
        }

        public async Task<IEnumerable<DashboardPayableDto>> GetPayablesAsync(string companyCd, string toYmd, int top = 10)
        {
            const string sql = "CALL getDashboardPayables(@p_COMPANY_CD, @p_TO_YMD, @p_TOP)";
            return await _db.QueryAsync<DashboardPayableDto>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_TO_YMD = toYmd,
                p_TOP = top
            }, commandTimeout: DashboardCommandTimeoutSeconds);
        }

        public async Task<IEnumerable<DashboardTaxDeadlineDto>> GetTaxDeadlinesAsync(string companyCd, string periodYm)
        {
            const string sql = "CALL getDashboardTaxDeadlines(@p_COMPANY_CD, @p_PERIOD_YM)";
            var rows = await _db.QueryAsync<dynamic>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm
            }, commandTimeout: DashboardCommandTimeoutSeconds);
            return rows.Select(r => new DashboardTaxDeadlineDto
            {
                ReportNm = (string)(r.REPORT_NM ?? ""),
                Period = (string)(r.PERIOD ?? ""),
                DueDate = (string)(r.DUE_DATE ?? ""),
                Status = (string)(r.STATUS ?? "PENDING")
            }).ToList();
        }

        public async Task<DashboardPeriodLockDto> GetPeriodLockAsync(string companyCd, string periodYm)
        {
            const string sql = "CALL getDashboardPeriodLock(@p_COMPANY_CD, @p_PERIOD_YM)";
            using var conn = _db.GetOpenConnection(Net_DB.Net_DB_Company);
            using var multi = await Dapper.SqlMapper.QueryMultipleAsync(conn, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_PERIOD_YM = periodYm
            }, commandTimeout: DashboardCommandTimeoutSeconds);

            var headerRow = (await multi.ReadAsync<dynamic>()).FirstOrDefault();
            var stepRows = (await multi.ReadAsync<dynamic>()).ToList();

            return new DashboardPeriodLockDto
            {
                CurrentPeriod = (string)(headerRow?.CURRENT_PERIOD ?? periodYm),
                Status = (string)(headerRow?.STATUS ?? "OPEN"),
                Message = (string)(headerRow?.MESSAGE ?? ""),
                LockedBy = (string)(headerRow?.LOCKED_BY ?? ""),
                LockedAt = (string)(headerRow?.LOCKED_AT ?? ""),
                Steps = stepRows.Select(s => new DashboardPeriodLockStepDto
                {
                    StepCode = (string)(s.STEP_CODE ?? ""),
                    StepName = (string)(s.STEP_NAME ?? ""),
                    StepOrder = (int)(s.STEP_ORDER ?? 0),
                    Status = (string)(s.STATUS ?? "OPEN"),
                    Message = (string)(s.MESSAGE ?? ""),
                    StartedAt = (string)(s.STARTED_AT ?? ""),
                    FinishedAt = (string)(s.FINISHED_AT ?? "")
                }).ToList()
            };
        }

        public async Task<IEnumerable<DashboardRecentVoucherDto>> GetRecentVouchersAsync(
            string companyCd, string fromYmd, string toYmd, string? status, int limit = 20)
        {
            const string sql = "CALL getDashboardRecentVouchers(@p_COMPANY_CD, @p_FROM_YMD, @p_TO_YMD, @p_STATUS, @p_LIMIT)";
            var rows = await _db.QueryAsync<dynamic>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd,
                p_STATUS = string.IsNullOrEmpty(status) ? "ALL" : status,
                p_LIMIT = Math.Clamp(limit, 1, 100)
            }, commandTimeout: DashboardCommandTimeoutSeconds);
            return rows.Select(r => new DashboardRecentVoucherDto
            {
                ChitYmd = (string)(r.CHIT_YMD ?? ""),
                ChitNo = (string)(r.CHIT_NO ?? ""),
                ChitCd = (string)(r.CHIT_CD ?? ""),
                ChitType = (string)(r.CHIT_TYPE ?? ""),
                Amount = (decimal)(r.AMOUNT ?? 0m),
                Status = (string)(r.STATUS ?? "PENDING"),
                Description = (string)(r.DESCRIPTION ?? "")
            }).ToList();
        }
    }
}
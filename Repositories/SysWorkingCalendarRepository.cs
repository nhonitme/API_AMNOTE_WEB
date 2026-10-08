using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class SysWorkingCalendarRepository : ISysWorkingCalendarRepository
    {
        private const string GetEntriesQuery = "CALL sys_working_calendar_get(@p_FROM_DATE, @p_TO_DATE)";

        private readonly DapperExecutor _db;

        public SysWorkingCalendarRepository(DapperExecutor db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<SysWorkingCalendarEntry>> GetEntriesAsync(DateTime fromDate, DateTime toDate)
        {
            var rows = await _db.QueryAsync<SysWorkingCalendarEntry>(
                Net_DB.Net_DB_Manager,
                GetEntriesQuery,
                new
                {
                    p_FROM_DATE = Common.FormatNullableYmd(fromDate.Date),
                    p_TO_DATE = Common.FormatNullableYmd(toDate.Date)
                });

            return rows.ToList();
        }
    }
}
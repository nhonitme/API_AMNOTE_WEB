using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysWorkingCalendarRepository
    {
        Task<IReadOnlyList<SysWorkingCalendarEntry>> GetEntriesAsync(DateTime fromDate, DateTime toDate);
    }
}

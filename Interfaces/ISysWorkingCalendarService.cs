namespace API_AMNOTE_WEB.Interfaces
{
    public interface ISysWorkingCalendarService
    {
        Task<bool> IsWorkingDayAsync(DateTime date);

        Task<DateTime> GetNextWorkingDateAsync(DateTime fromDate, int numberOfWorkingDays);
    }
}

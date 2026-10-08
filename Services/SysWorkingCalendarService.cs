using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Services
{
    public class SysWorkingCalendarService : ISysWorkingCalendarService
    {
        private const int MaxLookupDays = 366;

        private readonly ISysWorkingCalendarRepository _repository;

        public SysWorkingCalendarService(ISysWorkingCalendarRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> IsWorkingDayAsync(DateTime date)
        {
            var normalizedDate = date.Date;
            var overrides = await LoadOverridesAsync(normalizedDate, normalizedDate);
            return WorkingCalendarRules.IsWorkingDay(normalizedDate, overrides);
        }

        public async Task<DateTime> GetNextWorkingDateAsync(DateTime fromDate, int numberOfWorkingDays)
        {
            if (numberOfWorkingDays <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numberOfWorkingDays), "numberOfWorkingDays must be greater than zero");
            }

            var startDate = fromDate.Date;
            var rangeEnd = startDate.AddDays(45);
            var overrides = await LoadOverridesAsync(startDate.AddDays(1), rangeEnd);

            var cursor = startDate;
            var found = 0;
            while (found < numberOfWorkingDays)
            {
                cursor = cursor.AddDays(1);
                if (cursor > startDate.AddDays(MaxLookupDays))
                {
                    throw new InvalidOperationException("Unable to resolve next working date within lookup range");
                }

                if (cursor > rangeEnd)
                {
                    var nextRangeEnd = cursor.AddDays(45);
                    var extraOverrides = await LoadOverridesAsync(rangeEnd.AddDays(1), nextRangeEnd);
                    foreach (var pair in extraOverrides)
                    {
                        overrides[pair.Key] = pair.Value;
                    }

                    rangeEnd = nextRangeEnd;
                }

                if (WorkingCalendarRules.IsWorkingDay(cursor, overrides))
                {
                    found++;
                }
            }

            return cursor;
        }

        private async Task<Dictionary<DateTime, bool>> LoadOverridesAsync(DateTime fromDate, DateTime toDate)
        {
            if (toDate < fromDate)
            {
                return new Dictionary<DateTime, bool>();
            }

            var entries = await _repository.GetEntriesAsync(fromDate, toDate);
            return entries.ToDictionary(
                entry => entry.CALENDAR_DATE.Date,
                entry => entry.IS_WORKING_DAY == 1);
        }
    }

    internal static class WorkingCalendarRules
    {
        public static bool IsWorkingDay(DateTime date, IReadOnlyDictionary<DateTime, bool> overrides)
        {
            var normalizedDate = date.Date;
            if (overrides.TryGetValue(normalizedDate, out var isWorkingDay))
            {
                return isWorkingDay;
            }

            return IsDefaultWorkingDay(normalizedDate);
        }

        public static bool IsDefaultWorkingDay(DateTime date)
        {
            return date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
        }
    }
}

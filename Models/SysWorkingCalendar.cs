namespace API_AMNOTE_WEB.Models
{
    public class SysWorkingCalendarEntry
    {
        public DateTime CALENDAR_DATE { get; set; }
        public int IS_WORKING_DAY { get; set; }
        public string NOTE { get; set; } = string.Empty;
    }
}

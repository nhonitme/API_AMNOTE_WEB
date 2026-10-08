namespace API_AMNOTE_WEB.Services.BackgroundJobs.Shared;

public static class BackgroundJobProgressHelper
{
    public static int CalculatePercent(
        int processed,
        int total,
        int currentPercent = 0,
        int minPercent = 10,
        int maxPercent = 99)
    {
        if (total <= 0)
        {
            return Math.Max(currentPercent, minPercent);
        }

        var range = maxPercent - minPercent;
        var calculated = minPercent + (int)Math.Round(processed * (decimal)range / total, MidpointRounding.AwayFromZero);
        return Math.Min(maxPercent, Math.Max(currentPercent, calculated));
    }
}

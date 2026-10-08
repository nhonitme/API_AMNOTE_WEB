using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.PeriodLock;

public static class PeriodLockProgressUnit
{
    public static int GetStepUnitCount(string stepCode)
    {
        if (string.Equals(stepCode, PeriodLockStepCode.FaPrepaidLock, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(stepCode, PeriodLockStepCode.CogsSummary, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (string.Equals(stepCode, PeriodLockStepCode.ProfitLossReport, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 1;
    }

    public static int GetTotalUnitCount(IEnumerable<string>? stepCodes)
    {
        if (stepCodes == null)
        {
            return 1;
        }

        var count = stepCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Sum(GetStepUnitCount);

        return Math.Max(count, 1);
    }
}

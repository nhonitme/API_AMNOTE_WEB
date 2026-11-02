using System.Globalization;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Validation thuần (không DB) cho G-K 2C-01 — dùng chung Service và unit test.
/// </summary>
public static class ProductionCostValidation
{
    private static readonly Regex PeriodYmRegex = new(@"^\d{6}$", RegexOptions.Compiled);

    public static bool IsValidPeriodYm(string? periodYm)
    {
        if (string.IsNullOrWhiteSpace(periodYm) || !PeriodYmRegex.IsMatch(periodYm))
            return false;

        if (!DateTime.TryParseExact(periodYm + "01", "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return false;

        return true;
    }

    public static bool IsValidOptionalRunId(long? runId) =>
        !runId.HasValue || runId.Value > 0;

    public static bool IsValidOptionalRunVersion(int? runVersion) =>
        !runVersion.HasValue || runVersion.Value > 0;

    public static string? ValidateQuery(string? periodYm, long? runId, int? runVersion)
    {
        if (!IsValidPeriodYm(periodYm))
            return "PeriodYm must be yyyyMM.";
        if (!IsValidOptionalRunId(runId))
            return "RunId must be a positive number when provided.";
        if (!IsValidOptionalRunVersion(runVersion))
            return "RunVersion must be a positive number when provided.";
        return null;
    }

    public static int ResolveDetailVersion(int? requestedVersion, int runHeaderVersion) =>
        requestedVersion is > 0 ? requestedVersion.Value : runHeaderVersion;
}

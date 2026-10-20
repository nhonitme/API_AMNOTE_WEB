using System.Globalization;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Helpers;

public static class DailySession
{
    public const string DeadlineClaim = "session_expires_at";
    public const string RefreshPrefix = "daily1.";

    public static DateTimeOffset NextMidnight(DateTimeOffset now) =>
        new DateTimeOffset(now.ToOffset(TimeSpan.FromHours(7)).Date.AddDays(1), TimeSpan.FromHours(7)).ToUniversalTime();

    public static DateTimeOffset? Deadline(ClaimsPrincipal? principal) =>
        DateTimeOffset.TryParse(principal?.FindFirst(DeadlineClaim)?.Value, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var deadline) ? deadline : null;

    public static bool IsValid(ClaimsPrincipal? principal) => IsValid(principal, DateTimeOffset.UtcNow);

    public static bool IsValid(ClaimsPrincipal? principal, DateTimeOffset now) =>
        Deadline(principal) is { } deadline && deadline > now && deadline <= NextMidnight(now);
}

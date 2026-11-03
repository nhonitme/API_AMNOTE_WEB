using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Bảo toàn snapshot theo RUN_VERSION — không DELETE/overwrite phiên đã xác nhận.
/// </summary>
public static class RunVersionRules
{
    public static void AssertWritableVersion(
        string companyCd,
        long runId,
        int requestedVersion,
        int headerVersion,
        string? runStatus)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || runId <= 0 || requestedVersion <= 0)
            throw new ArgumentException("COMPANY_CD / RUN_ID / RUN_VERSION không hợp lệ.");

        if (requestedVersion != headerVersion)
        {
            throw new InvalidOperationException(
                "Chỉ được ghi dữ liệu tính lại vào RUN_VERSION hiện hành trên header.");
        }

        if (string.Equals(runStatus, GkRunStatus.Superseded, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Không ghi trên phiên SUPERSEDED.");

        if (string.Equals(runStatus, GkRunStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Không overwrite snapshot CONFIRMED. REOPEN để tạo RUN_VERSION mới trước.");
        }
    }

    public static bool AllowsRecalcWrite(string? runStatus) =>
        string.Equals(runStatus, GkRunStatus.Draft, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, GkRunStatus.Previewed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, GkRunStatus.AllocConfirmed, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(runStatus);

    /// <summary>Sau REOPEN: version cũ giữ nguyên dữ liệu; version mới = old+1.</summary>
    public static int NextWorkingVersion(int preservedConfirmedVersion)
    {
        if (preservedConfirmedVersion <= 0)
            throw new ArgumentException("RUN_VERSION bảo toàn phải > 0.");
        return preservedConfirmedVersion + 1;
    }

    public static void AssertConfirmedSnapshotIntact(
        bool wouldDeleteOldVersionRows,
        bool wouldOverwriteConfirmedUniqueKey)
    {
        if (wouldDeleteOldVersionRows || wouldOverwriteConfirmedUniqueKey)
        {
            throw new InvalidOperationException(
                "Cấm DELETE/overwrite snapshot lịch sử đã xác nhận (RUN_ID+RUN_VERSION cũ).");
        }
    }
}

using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Bảo toàn kết quả WIP CONFIRMED khi tính lại — chỉ ghi version DRAFT/PREVIEW/REOPEN hợp lệ.
/// </summary>
public static class WipRecalcGuard
{
    public static bool AllowsWipRecalc(string? runStatus, string? costCalcStepStatus = null)
    {
        _ = costCalcStepStatus;
        // CONFIRMED toàn kỳ: không xóa version đã khóa; chỉ tính version mới sau REOPEN.
        if (string.Equals(runStatus, GkRunStatus.Confirmed, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(runStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(runStatus, GkRunStatus.Draft, StringComparison.OrdinalIgnoreCase)
               || string.Equals(runStatus, GkRunStatus.Previewed, StringComparison.OrdinalIgnoreCase)
               || string.Equals(runStatus, GkRunStatus.AllocConfirmed, StringComparison.OrdinalIgnoreCase)
               || string.IsNullOrWhiteSpace(runStatus);
    }

    public static void EnsureCanOverwriteWip(
        string companyCd,
        long runId,
        int runVersion,
        int headerRunVersion,
        string? runStatus,
        bool targetingConfirmedVersion)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || runId <= 0 || runVersion <= 0)
            throw new ArgumentException("COMPANY_CD / RUN_ID / RUN_VERSION không hợp lệ.");

        if (runVersion != headerRunVersion)
            throw new InvalidOperationException(
                "Chỉ được tính WIP đúng RUN_VERSION hiện tại trên header RUN.");

        if (targetingConfirmedVersion)
            throw new InvalidOperationException(
                "Không được ghi đè bản WIP CONFIRMED. Tính trên version DRAFT mới hoặc REOPEN trước.");

        if (!AllowsWipRecalc(runStatus))
            throw new InvalidOperationException(
                "Trạng thái RUN không cho phép tính lại WIP (cần DRAFT/PREVIEWED/ALLOC_CONFIRMED/REOPENED).");
    }

    /// <summary>
    /// Version CONFIRMED cũ giữ nguyên khi preview version mới — không DELETE theo RUN_ID không kèm VERSION.
    /// </summary>
    public static bool WouldDeleteConfirmedVersion(int deleteRunVersion, int confirmedRunVersion) =>
        confirmedRunVersion > 0 && deleteRunVersion == confirmedRunVersion;

    /// <summary>Preview chỉ ghi version hiện tại — tuyệt đối không đụng snapshot CONFIRMED.</summary>
    public static void EnsurePreviewPreservesConfirmedSnapshot(
        int previewRunVersion,
        int? confirmedRunVersion,
        bool wouldDeleteWithoutVersionFilter)
    {
        if (confirmedRunVersion is > 0 && previewRunVersion == confirmedRunVersion.Value)
        {
            throw new InvalidOperationException(
                "Preview không được ghi đè snapshot WIP cùng RUN_VERSION đã CONFIRMED. " +
                "Tăng version hoặc REOPEN trước.");
        }

        if (wouldDeleteWithoutVersionFilter)
        {
            throw new InvalidOperationException(
                "Preview cấm DELETE theo RUN_ID không kèm RUN_VERSION — sẽ mất snapshot CONFIRMED cũ.");
        }
    }
}

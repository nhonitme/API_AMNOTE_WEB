using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Bảo toàn snapshot theo RUN_VERSION — không DELETE/overwrite phiên đã xác nhận.
/// Đồng bộ chặn B2 với <see cref="GkRunStatus.AllowsPoolRecalc"/>.
/// </summary>
public static class RunVersionRules
{
    public enum ReopenVersionMode
    {
        /// <summary>Lần đầu rời snapshot khóa (CONFIRMED / ALLOC_CONFIRMED) → tạo version mới.</summary>
        CreateNewWorkingVersion,
        /// <summary>Đã REOPENED — mở thêm bước / cascade trong cùng phiên → giữ version.</summary>
        KeepCurrentWorkingVersion,
        /// <summary>Retry cùng OperationId → không đổi version, không ghi history trùng.</summary>
        IdempotentRetry
    }

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

    /// <summary>
    /// Cho phép ghi recalc tổng quát (B3/B4/B5…) khi DRAFT/PREVIEWED/REOPENED/ALLOC_CONFIRMED.
    /// B2 pool phải qua <see cref="AssertCanRecalcStep"/>.
    /// </summary>
    public static bool AllowsRecalcWrite(string? runStatus) =>
        string.Equals(runStatus, GkRunStatus.Draft, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, GkRunStatus.Previewed, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase)
        || string.Equals(runStatus, GkRunStatus.AllocConfirmed, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(runStatus);

    /// <summary>
    /// B2 (COST_ALLOCATION) bắt buộc <see cref="GkRunStatus.AllowsPoolRecalc"/>;
    /// B3+ vẫn chạy khi ALLOC_CONFIRMED nếu tiền đề bước hợp lệ.
    /// </summary>
    public static void AssertCanRecalcStep(string stepCode, string? runStatus)
    {
        var step = GkStepOrder.Normalize(stepCode);
        if (string.Equals(step, ProductionCostStepCode.CostAllocation, StringComparison.OrdinalIgnoreCase))
        {
            if (!GkRunStatus.AllowsPoolRecalc(runStatus))
            {
                throw new InvalidOperationException(
                    "RUN.STATUS=ALLOC_CONFIRMED/CONFIRMED — không tính lại pool B2 trực tiếp. " +
                    "Phải REOPEN hợp lệ trước khi thay đổi phân bổ.");
            }
        }
        else if (!AllowsRecalcWrite(runStatus))
        {
            throw new InvalidOperationException(
                $"RUN.STATUS={runStatus} không cho tính lại {step}.");
        }
    }

    public static ReopenVersionMode ResolveReopenMode(string? runStatus, bool isIdempotentRetry)
    {
        if (isIdempotentRetry)
            return ReopenVersionMode.IdempotentRetry;

        if (string.Equals(runStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase))
            return ReopenVersionMode.KeepCurrentWorkingVersion;

        if (string.Equals(runStatus, GkRunStatus.Confirmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(runStatus, GkRunStatus.AllocConfirmed, StringComparison.OrdinalIgnoreCase))
            return ReopenVersionMode.CreateNewWorkingVersion;

        // DRAFT/PREVIEWED: chưa có snapshot khóa — giữ version hiện tại, không SUPERSEDED
        return ReopenVersionMode.KeepCurrentWorkingVersion;
    }

    /// <summary>Chỉ đánh SUPERSEDED khi tạo version mới từ snapshot đã khóa.</summary>
    public static bool ShouldMarkPriorVersionSuperseded(ReopenVersionMode mode) =>
        mode == ReopenVersionMode.CreateNewWorkingVersion;

    public static int ResolveWorkingVersion(int currentVersion, ReopenVersionMode mode) =>
        mode switch
        {
            ReopenVersionMode.CreateNewWorkingVersion => NextWorkingVersion(currentVersion),
            ReopenVersionMode.KeepCurrentWorkingVersion => currentVersion,
            ReopenVersionMode.IdempotentRetry => currentVersion,
            _ => currentVersion
        };

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

using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Quy tắc mở lại bước G-K: lý do bắt buộc, cascade ngược B6→target, bảo toàn RUN_VERSION cũ.
/// </summary>
public static class ReopenRules
{
    public static ReopenPlan Build(ReopenDryRunPayload dry, string companyCd)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new ArgumentException("COMPANY_CD bắt buộc.");

        if (string.IsNullOrWhiteSpace(dry.Reason) || dry.Reason.Trim().Length < 3)
            throw new ArgumentException("Bắt buộc nhập lý do mở lại (tối thiểu 3 ký tự).");

        if (!dry.HasPermission)
            throw new InvalidOperationException("Người dùng không có quyền mở lại kỳ/bước G-K.");

        if (dry.RunId <= 0 || dry.CurrentRunVersion <= 0)
            throw new ArgumentException("RUN_ID / RUN_VERSION hiện hành không hợp lệ.");

        if (string.Equals(dry.RunStatus, GkRunStatus.Superseded, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Không thao tác trên phiên SUPERSEDED — dùng RUN_VERSION hiện hành.");
        }

        if (dry.RequestedRunVersion is > 0 && dry.RequestedRunVersion != dry.CurrentRunVersion)
        {
            throw new InvalidOperationException(
                $"RUN_VERSION request ({dry.RequestedRunVersion}) ≠ header hiện hành ({dry.CurrentRunVersion}).");
        }

        var target = GkStepOrder.Normalize(dry.TargetStepCode);
        var statuses = NormalizeStatuses(dry.StepStatuses);

        // Chặn mở lại khi còn bước sau DONE mà chưa nằm trong plan cascade
        var cascade = BuildCascadeReverse(target, statuses);
        AssertNoSkippedDownstream(target, statuses, cascade);

        var newVersion = dry.CurrentRunVersion + 1;
        var historyKey = HistoryPlan.StableKey(
            companyCd, dry.RunId, dry.CurrentRunVersion, target, HistoryActionCode.Reopen);

        var idem = HistoryPlan.PlanWrite(
            historyKey,
            alreadyWrittenSameKey: dry.ExistingHistoryStableKey is not null
                && string.Equals(dry.ExistingHistoryStableKey, historyKey, StringComparison.Ordinal));

        return new ReopenPlan
        {
            CompanyCd = companyCd.Trim(),
            RunId = dry.RunId,
            PreservedRunVersion = dry.CurrentRunVersion,
            NewWorkingRunVersion = newVersion,
            TargetStepCode = target,
            CascadeReverse = cascade,
            PriorRunStatus = dry.RunStatus ?? string.Empty,
            NewRunStatus = GkRunStatus.Reopened,
            MarkPriorVersionSuperseded = true,
            PreserveConfirmedSnapshots = true,
            WouldDeleteConfirmedData = false,
            Reason = dry.Reason.Trim(),
            History = new HistoryEntryPlan
            {
                StableKey = historyKey,
                ActionCode = HistoryActionCode.Reopen,
                StepCode = target,
                StepIndex = GkStepOrder.IndexOf(target) + 1,
                RunId = dry.RunId,
                RunVersionBefore = dry.CurrentRunVersion,
                RunVersionAfter = newVersion,
                StatusBefore = dry.RunStatus,
                StatusAfter = GkRunStatus.Reopened,
                Reason = dry.Reason.Trim(),
                IdempotencyAction = idem.Action.ToString(),
                WritesHistory = idem.WritesHistory
            },
            Message = idem.WritesHistory
                ? $"Plan REOPEN {target}: cascade {string.Join(" → ", cascade)}; " +
                  $"bảo toàn v{dry.CurrentRunVersion}; làm việc v{newVersion}."
                : "REOPEN idempotent — cùng STABLE_KEY đã ghi history, không ghi trùng."
        };
    }

    /// <summary>
    /// Cascade mở lại: các bước sau đang DONE/PREVIEWED theo thứ tự ngược, rồi đến target.
    /// </summary>
    public static IReadOnlyList<string> BuildCascadeReverse(
        string targetStepCode,
        IReadOnlyDictionary<string, string> stepStatuses)
    {
        var target = GkStepOrder.Normalize(targetStepCode);
        var list = new List<string>();
        foreach (var step in GkStepOrder.DownstreamReverse(target))
        {
            if (stepStatuses.TryGetValue(step, out var st) && GkStepOrder.IsDone(st))
                list.Add(step);
        }

        list.Add(target);
        return list;
    }

    /// <summary>Không được bỏ qua bước sau còn khóa khi mở bước trước.</summary>
    public static void AssertCanReopenTarget(
        string targetStepCode,
        IReadOnlyDictionary<string, string> stepStatuses)
    {
        var target = GkStepOrder.Normalize(targetStepCode);
        foreach (var down in GkStepOrder.Downstream(target))
        {
            if (stepStatuses.TryGetValue(down, out var st) &&
                string.Equals(st, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Không mở lại {target} khi bước sau {down} còn DONE. " +
                    $"Mở lại theo thứ tự ngược: {string.Join(" → ", GkStepOrder.DownstreamReverse(target))} → {target}.");
            }
        }
    }

    private static void AssertNoSkippedDownstream(
        string target,
        IReadOnlyDictionary<string, string> statuses,
        IReadOnlyList<string> cascade)
    {
        var cascadeSet = cascade.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var down in GkStepOrder.Downstream(target))
        {
            if (!statuses.TryGetValue(down, out var st))
                continue;
            if (string.Equals(st, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase)
                && !cascadeSet.Contains(down))
            {
                throw new InvalidOperationException(
                    $"Cascade REOPEN thiếu bước phụ thuộc {down} (còn DONE).");
            }
        }
    }

    private static Dictionary<string, string> NormalizeStatuses(
        IReadOnlyDictionary<string, string>? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raw == null)
            return map;
        foreach (var kv in raw)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
                continue;
            map[GkStepOrder.Normalize(kv.Key)] = (kv.Value ?? string.Empty).Trim();
        }

        return map;
    }
}

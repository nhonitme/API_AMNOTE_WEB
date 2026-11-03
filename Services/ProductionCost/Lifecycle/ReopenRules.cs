using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Quy tắc mở lại bước G-K: lý do bắt buộc, cascade ngược B6→target, bảo toàn RUN_VERSION cũ.
/// Không tăng version lặp khi đã REOPENED; SUPERSEDED chỉ khi tạo version mới hợp lệ.
/// </summary>
public static class ReopenRules
{
    public static ReopenPlan Build(ReopenDryRunPayload dry, string companyCd, string? userId = null)
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

        var operationId = HistoryPlan.RequireOperationId(dry.OperationId);
        var target = GkStepOrder.Normalize(dry.TargetStepCode);
        var statuses = NormalizeStatuses(dry.StepStatuses);
        var cascade = BuildCascadeReverse(target, statuses);
        AssertNoSkippedDownstream(target, statuses, cascade);

        // Confirm DB: chỉ tin PersistedHistoryFromDb / OperationIds từ DB — không tin client hint.
        var historyKey = HistoryPlan.StableKey(
            companyCd, dry.RunId, dry.CurrentRunVersion, target, HistoryActionCode.Reopen, operationId);
        var persisted = HistoryPlan.NormalizePersisted(
            dry.PersistedHistoryFromDb, dry.PersistedOperationIdsFromDb);
        var idem = HistoryPlan.PlanWrite(historyKey, operationId, persisted);
        var isRetry = idem.Action == HistoryPlan.WriteAction.NoopAlreadyWritten;
        var mode = RunVersionRules.ResolveReopenMode(dry.RunStatus, isRetry);

        var workingVersion = RunVersionRules.ResolveWorkingVersion(dry.CurrentRunVersion, mode);
        var markSuperseded = RunVersionRules.ShouldMarkPriorVersionSuperseded(mode);
        var preserved = mode == RunVersionRules.ReopenVersionMode.CreateNewWorkingVersion
            ? dry.CurrentRunVersion
            : dry.CurrentRunVersion;

        var actionAt = dry.ActionAtUtc ?? DateTime.UtcNow;

        return new ReopenPlan
        {
            CompanyCd = companyCd.Trim(),
            RunId = dry.RunId,
            PreservedRunVersion = preserved,
            NewWorkingRunVersion = workingVersion,
            VersionMode = mode.ToString(),
            TargetStepCode = target,
            CascadeReverse = cascade,
            PriorRunStatus = dry.RunStatus ?? string.Empty,
            NewRunStatus = GkRunStatus.Reopened,
            MarkPriorVersionSuperseded = markSuperseded,
            PreserveConfirmedSnapshots = true,
            WouldDeleteConfirmedData = false,
            Reason = dry.Reason.Trim(),
            History = new HistoryEntryPlan
            {
                StableKey = historyKey,
                OperationId = operationId,
                ActionCode = HistoryActionCode.Reopen,
                StepCode = target,
                StepIndex = GkStepOrder.IndexOf(target) + 1,
                RunId = dry.RunId,
                RunVersionBefore = dry.CurrentRunVersion,
                RunVersionAfter = workingVersion,
                StatusBefore = dry.RunStatus,
                StatusAfter = GkRunStatus.Reopened,
                Reason = dry.Reason.Trim(),
                UserId = userId,
                ActionAtUtc = actionAt,
                IdempotencyAction = idem.Action.ToString(),
                WritesHistory = idem.WritesHistory,
                ClientHistoryHintIsNotAuthoritative = true
            },
            Message = BuildMessage(mode, target, cascade, dry.CurrentRunVersion, workingVersion, idem.WritesHistory)
        };
    }

    private static string BuildMessage(
        RunVersionRules.ReopenVersionMode mode,
        string target,
        IReadOnlyList<string> cascade,
        int current,
        int working,
        bool writesHistory)
    {
        if (!writesHistory)
            return "REOPEN idempotent — cùng OperationId đã persist; không tăng version / không ghi history trùng.";

        return mode switch
        {
            RunVersionRules.ReopenVersionMode.CreateNewWorkingVersion =>
                $"REOPEN lần đầu {target}: cascade {string.Join(" → ", cascade)}; " +
                $"bảo toàn snapshot v{current}; làm việc v{working}; đánh SUPERSEDED version cũ.",
            RunVersionRules.ReopenVersionMode.KeepCurrentWorkingVersion =>
                $"REOPEN trong phiên REOPENED/DRAFT — giữ v{working}; cascade {string.Join(" → ", cascade)}; " +
                "không tăng RUN_VERSION; không đánh SUPERSEDED lại.",
            _ => $"REOPEN {target} v{working}."
        };
    }

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

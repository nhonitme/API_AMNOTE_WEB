using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Lập kế hoạch Confirm REOPEN — pure, không DB.
/// Version +1 lần đầu từ CONFIRMED/ALLOC_CONFIRMED; giữ version khi đã REOPENED;
/// retry OperationId → Noop; không DELETE snapshot cũ.
/// </summary>
public static class ReopenConfirmPlanner
{
    public sealed class Plan
    {
        public ReopenPlan Reopen { get; init; } = new();
        public string ContentFingerprint { get; init; } = string.Empty;
        public bool IsNoop { get; init; }
        public bool WouldPersist { get; init; }
        public bool WouldBumpVersion { get; init; }
        public bool WouldMarkPriorSuperseded { get; init; }
        public bool WouldWriteHistory { get; init; }
        public bool WouldDeleteConfirmedSnapshot { get; init; }
        public string? IdempotencyAction { get; init; }
        public string Message { get; init; } = string.Empty;
        /// <summary>Retry sau khi version đã đổi — nhận từ history, không REOPEN lần hai.</summary>
        public bool RecognizedCompletedReopenFromHistory { get; init; }
    }

    public static string BuildContentFingerprint(
        string companyCd,
        long runId,
        int currentRunVersion,
        string targetStepCode,
        string reason,
        string priorStatus,
        IReadOnlyList<string> cascadeReverse)
    {
        var inv = CultureInfo.InvariantCulture;
        var target = GkStepOrder.Normalize(targetStepCode);
        var cascade = string.Join(">", cascadeReverse.Select(GkStepOrder.Normalize));
        var sb = new StringBuilder();
        sb.Append("REOPEN|c").Append(companyCd.Trim())
            .Append("|r").Append(runId.ToString(inv))
            .Append("|v").Append(currentRunVersion.ToString(inv))
            .Append("|t").Append(target)
            .Append("|st").Append((priorStatus ?? string.Empty).Trim().ToUpperInvariant())
            .Append("|cas").Append(cascade)
            .Append("|why").Append((reason ?? string.Empty).Trim());

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))
            .ToLowerInvariant();
    }

    public static Plan BuildFromDb(
        string companyCd,
        string? userId,
        long runId,
        int lockedRunVersion,
        string? lockedRunStatus,
        string targetStepCode,
        string reason,
        bool hasPermission,
        string operationId,
        IReadOnlyDictionary<string, string> stepStatusesFromDb,
        PersistedHistoryRef? persistedHistoryFromDb,
        string? clientFingerprint = null,
        DateTime? actionAtUtc = null)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, lockedRunVersion);

        // 1) Retry sau bump version: cùng OpId đã REOPEN thành công → Noop từ history.
        var completed = ReopenIdempotency.TryRecognizeCompleted(
            companyCd, runId, lockedRunVersion, lockedRunStatus, operationId, persistedHistoryFromDb);
        if (completed != null)
            return completed;

        var dry = new ReopenDryRunPayload
        {
            RunId = runId,
            CurrentRunVersion = lockedRunVersion,
            RequestedRunVersion = lockedRunVersion,
            RunStatus = lockedRunStatus,
            TargetStepCode = targetStepCode,
            Reason = reason,
            HasPermission = hasPermission,
            OperationId = operationId,
            ActionAtUtc = actionAtUtc ?? DateTime.UtcNow,
            StepStatuses = stepStatusesFromDb,
            PersistedHistoryFromDb = persistedHistoryFromDb == null
                ? null
                : new[] { persistedHistoryFromDb }
        };

        var reopen = ReopenRules.Build(dry, companyCd, userId);
        RunVersionRules.AssertConfirmedSnapshotIntact(
            reopen.WouldDeleteConfirmedData,
            wouldOverwriteConfirmedUniqueKey: false);

        var fp = string.IsNullOrWhiteSpace(clientFingerprint)
            ? BuildContentFingerprint(
                companyCd, runId, lockedRunVersion, targetStepCode, reason,
                lockedRunStatus ?? string.Empty, reopen.CascadeReverse)
            : HistoryPlan.NormalizeContentFingerprint(clientFingerprint, required: true)!;

        // Confirm: luôn đối chiếu fingerprint từ DB — Conflict nếu khác / thiếu khi require.
        var idem = HistoryPlan.PlanWrite(
            reopen.History!.StableKey,
            operationId,
            persistedHistoryFromDb == null
                ? Array.Empty<PersistedHistoryRef>()
                : new[] { persistedHistoryFromDb },
            fp,
            requireContentFingerprint: true);
        reopen.History.IdempotencyAction = idem.Action.ToString();
        reopen.History.WritesHistory = idem.WritesHistory;

        var isNoop = !idem.WritesHistory
            || string.Equals(
                reopen.VersionMode,
                RunVersionRules.ReopenVersionMode.IdempotentRetry.ToString(),
                StringComparison.OrdinalIgnoreCase);

        if (isNoop)
        {
            return new Plan
            {
                Reopen = reopen,
                ContentFingerprint = fp,
                IsNoop = true,
                WouldPersist = false,
                WouldBumpVersion = false,
                WouldMarkPriorSuperseded = false,
                WouldWriteHistory = false,
                WouldDeleteConfirmedSnapshot = false,
                IdempotencyAction = HistoryPlan.WriteAction.NoopAlreadyWritten.ToString(),
                Message = "REOPEN Noop — cùng OperationId đã persist (kiểm tra DB)."
            };
        }

        return new Plan
        {
            Reopen = reopen,
            ContentFingerprint = fp,
            IsNoop = false,
            WouldPersist = true,
            WouldBumpVersion = reopen.MarkPriorVersionSuperseded
                || reopen.NewWorkingRunVersion != reopen.PreservedRunVersion,
            WouldMarkPriorSuperseded = reopen.MarkPriorVersionSuperseded,
            WouldWriteHistory = true,
            WouldDeleteConfirmedSnapshot = false,
            IdempotencyAction = HistoryPlan.WriteAction.InsertOnce.ToString(),
            Message = reopen.Message ?? "REOPEN plan ready."
        };
    }
}

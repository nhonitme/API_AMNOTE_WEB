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

    /// <summary>
    /// Fingerprint deterministic từ thông tin bất biến của request REOPEN.
    /// COMPANY_CD + RUN_ID + original RUN_VERSION + TargetStepCode + Reason.
    /// Không gồm STATUS/cascade (phụ thuộc DB tại thời điểm persist).
    /// </summary>
    public static string BuildContentFingerprint(
        string companyCd,
        long runId,
        int originalRunVersion,
        string targetStepCode,
        string reason)
    {
        var inv = CultureInfo.InvariantCulture;
        var target = GkStepOrder.Normalize(targetStepCode);
        var sb = new StringBuilder();
        sb.Append("REOPEN|c").Append(companyCd.Trim())
            .Append("|r").Append(runId.ToString(inv))
            .Append("|v").Append(originalRunVersion.ToString(inv))
            .Append("|t").Append(target)
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
        DateTime? actionAtUtc = null,
        int? requestOriginalRunVersion = null)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, lockedRunVersion);

        var originalVersion = requestOriginalRunVersion is > 0
            ? requestOriginalRunVersion.Value
            : lockedRunVersion;
        ProductionCostWriteContext.EnsureRunIdentity(runId, originalVersion);

        var fp = string.IsNullOrWhiteSpace(clientFingerprint)
            ? BuildContentFingerprint(companyCd, runId, originalVersion, targetStepCode, reason)
            : HistoryPlan.NormalizeContentFingerprint(clientFingerprint, required: true)!;

        // 1) Retry sau bump version: cùng OpId + cùng fingerprint đã REOPEN thành công → Noop từ history.
        //    Cho phép request original RUN_VERSION ≠ header khi đã chứng minh khớp history.
        var completed = ReopenIdempotency.TryRecognizeCompleted(
            companyCd,
            runId,
            lockedRunVersion,
            lockedRunStatus,
            operationId,
            persistedHistoryFromDb,
            fp,
            originalVersion,
            targetStepCode,
            reason);
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

        // Confirm: luôn đối chiếu fingerprint từ DB — Conflict nếu khác / thiếu khi require.
        // StableKey gắn locked version (version đang mở lại); FP gắn original request version.
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

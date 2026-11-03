using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Nhận diện retry REOPEN khi RUN_VERSION đã đổi sau lần persist thành công —
/// không thực hiện REOPEN lần hai; không Conflict chỉ vì StableKey gắn version cũ.
/// </summary>
public static class ReopenIdempotency
{
    public static ReopenConfirmPlanner.Plan? TryRecognizeCompleted(
        string companyCd,
        long runId,
        int lockedRunVersion,
        string? lockedRunStatus,
        string operationId,
        PersistedHistoryRef? persisted)
    {
        if (persisted == null || string.IsNullOrWhiteSpace(persisted.OperationId))
            return null;

        var op = HistoryPlan.RequireOperationId(operationId);
        if (!string.Equals(persisted.OperationId.Trim(), op, StringComparison.OrdinalIgnoreCase))
            return null;

        // Chỉ nhận diện khi history ghi rõ REOPEN cùng RUN.
        if (!string.IsNullOrWhiteSpace(persisted.ActionCode)
            && !string.Equals(persisted.ActionCode, HistoryActionCode.Reopen, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OperationId '{op}' đã dùng cho ACTION={persisted.ActionCode} — Conflict với REOPEN.");
        }

        if (persisted.RunId is > 0 && persisted.RunId.Value != runId)
        {
            throw new InvalidOperationException(
                $"OperationId '{op}' đã dùng cho RUN_ID={persisted.RunId} — Conflict.");
        }

        // StableKey REOPEN gắn version tại thời điểm mở lại (trước bump).
        // Sau bump: header version mới + STATUS=REOPENED → coi là hoàn tất.
        var looksLikeCompletedReopen =
            string.Equals(lockedRunStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(persisted.StableKey)
            && persisted.StableKey!.IndexOf(":REOPEN:", StringComparison.OrdinalIgnoreCase) >= 0
            && persisted.StableKey.Contains($":{runId}:", StringComparison.Ordinal);

        // Fallback: ActionCode=REOPEN + RunVersionAfter khớp header (khi SELECT đủ cột).
        if (!looksLikeCompletedReopen
            && string.Equals(persisted.ActionCode, HistoryActionCode.Reopen, StringComparison.OrdinalIgnoreCase)
            && persisted.RunVersion is > 0
            && persisted.RunVersion.Value == lockedRunVersion
            && string.Equals(lockedRunStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase))
        {
            looksLikeCompletedReopen = true;
        }

        if (!looksLikeCompletedReopen)
            return null;

        // Payload Conflict: cùng OpId nhưng fingerprint khác (nếu cả hai có).
        // Soft: thiếu fingerprint một phía vẫn Noop (REOPEN không bắt buộc FP trên hàng cũ).

        var reopen = new ReopenPlan
        {
            CompanyCd = companyCd.Trim(),
            RunId = runId,
            PreservedRunVersion = lockedRunVersion,
            NewWorkingRunVersion = lockedRunVersion,
            VersionMode = RunVersionRules.ReopenVersionMode.IdempotentRetry.ToString(),
            TargetStepCode = string.IsNullOrWhiteSpace(persisted.StepCode)
                ? string.Empty
                : persisted.StepCode!,
            CascadeReverse = Array.Empty<string>(),
            PriorRunStatus = lockedRunStatus ?? string.Empty,
            NewRunStatus = GkRunStatus.Reopened,
            MarkPriorVersionSuperseded = false,
            PreserveConfirmedSnapshots = true,
            WouldDeleteConfirmedData = false,
            Reason = "(idempotent retry — recognized from history)",
            History = new HistoryEntryPlan
            {
                StableKey = persisted.StableKey ?? string.Empty,
                OperationId = op,
                ActionCode = HistoryActionCode.Reopen,
                StepCode = persisted.StepCode ?? string.Empty,
                RunId = runId,
                RunVersionBefore = lockedRunVersion,
                RunVersionAfter = lockedRunVersion,
                StatusBefore = lockedRunStatus,
                StatusAfter = GkRunStatus.Reopened,
                IdempotencyAction = HistoryPlan.WriteAction.NoopAlreadyWritten.ToString(),
                WritesHistory = false,
                ClientHistoryHintIsNotAuthoritative = true
            },
            Message =
                "REOPEN đã hoàn tất trước đó (nhận từ history sau khi RUN_VERSION đổi) — Noop, không REOPEN lần hai."
        };

        return new ReopenConfirmPlanner.Plan
        {
            Reopen = reopen,
            ContentFingerprint = persisted.ContentFingerprint ?? string.Empty,
            IsNoop = true,
            WouldPersist = false,
            WouldBumpVersion = false,
            WouldMarkPriorSuperseded = false,
            WouldWriteHistory = false,
            WouldDeleteConfirmedSnapshot = false,
            IdempotencyAction = HistoryPlan.WriteAction.NoopAlreadyWritten.ToString(),
            RecognizedCompletedReopenFromHistory = true,
            Message = reopen.Message
        };
    }
}

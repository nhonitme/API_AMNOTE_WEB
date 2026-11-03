using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Nhận diện retry REOPEN khi RUN_VERSION đã đổi sau lần persist thành công —
/// không thực hiện REOPEN lần hai; không Conflict chỉ vì StableKey gắn version cũ.
/// Chỉ Noop khi fingerprint request khớp history authoritative (DB).
/// </summary>
public static class ReopenIdempotency
{
    /// <summary>
    /// Nhận diện REOPEN đã hoàn tất theo COMPANY_CD + OperationId (history DB).
    /// <paramref name="requestFingerprint"/> phải xây từ COMPANY_CD, RUN_ID,
    /// original RUN_VERSION, TargetStepCode, Reason — không tin client history.
    /// </summary>
    public static ReopenConfirmPlanner.Plan? TryRecognizeCompleted(
        string companyCd,
        long runId,
        int lockedRunVersion,
        string? lockedRunStatus,
        string operationId,
        PersistedHistoryRef? persisted,
        string requestFingerprint,
        int requestOriginalRunVersion,
        string targetStepCode,
        string reason)
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
        // Sau bump: header version mới + STATUS=REOPENED → coi là hoàn tất về mặt lifecycle.
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

        // Strict fingerprint — history REOPEN mới phải có FP; thiếu ≠ khớp → Conflict.
        var reqFp = HistoryPlan.NormalizeContentFingerprint(requestFingerprint, required: true)!;
        if (string.IsNullOrWhiteSpace(persisted.ContentFingerprint))
        {
            throw new InvalidOperationException(
                $"OperationId '{op}' đã REOPEN nhưng DB thiếu CONTENT_FINGERPRINT — Conflict " +
                "(không Noop khi thiếu fingerprint đối với history REOPEN mới).");
        }

        var gotFp = HistoryPlan.NormalizeContentFingerprint(persisted.ContentFingerprint, required: true)!;
        if (!string.Equals(reqFp, gotFp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OperationId '{op}' đã REOPEN nhưng request khác nội dung " +
                $"(TargetStepCode/Reason/original RUN_VERSION) — Conflict CONTENT_FINGERPRINT.");
        }

        // Đối chiếu bất biến từ request vs history step (khi DB có StepCode).
        var target = GkStepOrder.Normalize(targetStepCode);
        if (!string.IsNullOrWhiteSpace(persisted.StepCode)
            && !string.Equals(GkStepOrder.Normalize(persisted.StepCode), target, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OperationId '{op}' đã REOPEN TargetStep={persisted.StepCode} — Conflict với request {target}.");
        }

        _ = reason; // fingerprint đã bao Reason; không tin Reason từ client history.
        _ = requestOriginalRunVersion; // fingerprint đã bao original RUN_VERSION.

        var reopen = new ReopenPlan
        {
            CompanyCd = companyCd.Trim(),
            RunId = runId,
            PreservedRunVersion = lockedRunVersion,
            NewWorkingRunVersion = lockedRunVersion,
            VersionMode = RunVersionRules.ReopenVersionMode.IdempotentRetry.ToString(),
            TargetStepCode = target,
            CascadeReverse = Array.Empty<string>(),
            PriorRunStatus = lockedRunStatus ?? string.Empty,
            NewRunStatus = GkRunStatus.Reopened,
            MarkPriorVersionSuperseded = false,
            PreserveConfirmedSnapshots = true,
            WouldDeleteConfirmedData = false,
            Reason = reason.Trim(),
            History = new HistoryEntryPlan
            {
                StableKey = persisted.StableKey ?? string.Empty,
                OperationId = op,
                ActionCode = HistoryActionCode.Reopen,
                StepCode = target,
                RunId = runId,
                RunVersionBefore = lockedRunVersion,
                RunVersionAfter = lockedRunVersion,
                StatusBefore = lockedRunStatus,
                StatusAfter = GkRunStatus.Reopened,
                Reason = reason.Trim(),
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
            ContentFingerprint = gotFp,
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

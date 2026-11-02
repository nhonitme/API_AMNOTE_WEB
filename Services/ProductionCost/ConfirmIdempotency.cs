using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Lập kế hoạch Confirm theo STABLE_KEY — không tạo trùng chứng từ khi gửi lặp.
/// Atomicity: caller C# sở hữu TRANSACTION; SP không START/COMMIT.
/// </summary>
public static class ConfirmIdempotency
{
    public enum VoucherAction
    {
        InsertOnce,
        UpdateExisting,
        /// <summary>Đã CONFIRM cùng RUN_VERSION — không ghi voucher/history thêm.</summary>
        NoopAlreadyConfirmed
    }

    public sealed class VoucherPlan
    {
        public VoucherAction Action { get; init; }
        public string StableKey { get; init; } = string.Empty;
        public long? ExistingChitId { get; init; }
        public bool CreatesNewVoucher => Action == VoucherAction.InsertOnce;
        public bool WritesHistory => Action is VoucherAction.InsertOnce or VoucherAction.UpdateExisting;
    }

    public static VoucherPlan Plan(string stableKey, long? existingChitId)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("STABLE_KEY is required.");

        if (existingChitId is > 0)
        {
            return new VoucherPlan
            {
                Action = VoucherAction.UpdateExisting,
                StableKey = stableKey,
                ExistingChitId = existingChitId
            };
        }

        return new VoucherPlan
        {
            Action = VoucherAction.InsertOnce,
            StableKey = stableKey,
            ExistingChitId = null
        };
    }

    /// <summary>
    /// Confirm đồng thời / lặp: nếu step đã DONE + RUN đã khóa phân bổ + đúng version → NOOP.
    /// </summary>
    public static VoucherPlan PlanConfirm(
        string stableKey,
        long? existingChitId,
        string? runStatus,
        string? allocationStepStatus,
        int requestedVersion,
        int lockedVersion)
    {
        if (IsDuplicateConfirm(allocationStepStatus, requestedVersion, lockedVersion)
            && GkRunStatus.IsAllocationLocked(runStatus))
        {
            return new VoucherPlan
            {
                Action = VoucherAction.NoopAlreadyConfirmed,
                StableKey = stableKey,
                ExistingChitId = existingChitId
            };
        }

        return Plan(stableKey, existingChitId);
    }

    /// <summary>
    /// Preview không ghi đè phiên đã khóa phân bổ (ALLOC_CONFIRMED / CONFIRMED).
    /// </summary>
    public static void AssertPreviewDoesNotOverwriteConfirmed(string? runStatus, bool wouldPersist)
    {
        if (!wouldPersist)
            return;

        if (GkRunStatus.IsAllocationLocked(runStatus))
            throw new InvalidOperationException(
                "Preview không ghi đè phiên đã xác nhận phân bổ. Mở lại (REOPEN) trước khi tính thử ghi DB.");
    }

    /// <summary>
    /// Confirm lặp cùng RUN_VERSION đã DONE: idempotent — không tạo kết quả mới.
    /// </summary>
    public static bool IsDuplicateConfirm(string? stepStatus, int requestedVersion, int confirmedVersion) =>
        string.Equals(stepStatus, "DONE", StringComparison.OrdinalIgnoreCase)
        && requestedVersion == confirmedVersion;

    /// <summary>
    /// Mô phỏng atomic confirm: nếu bất kỳ bước nào fail → không ghi history thành công,
    /// số liệu/status giữ nguyên (caller rollback TX).
    /// </summary>
    public static bool ShouldWriteSuccessHistory(bool voucherSucceeded, bool allocationStepSucceeded) =>
        voucherSucceeded && allocationStepSucceeded;
}

namespace API_AMNOTE_WEB.Services.ProductionCost;

/// <summary>
/// Lập kế hoạch Confirm theo STABLE_KEY — không tạo trùng chứng từ khi gửi lặp.
/// </summary>
public static class ConfirmIdempotency
{
    public enum VoucherAction
    {
        InsertOnce,
        UpdateExisting
    }

    public sealed class VoucherPlan
    {
        public VoucherAction Action { get; init; }
        public string StableKey { get; init; } = string.Empty;
        public long? ExistingChitId { get; init; }
        public bool CreatesNewVoucher => Action == VoucherAction.InsertOnce;
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
    /// Preview không được ghi đè phiên CONFIRMED — chỉ được tính trên version DRAFT/hiện tại.
    /// </summary>
    public static void AssertPreviewDoesNotOverwriteConfirmed(string? runStatus, bool wouldPersist)
    {
        if (!wouldPersist)
            return;

        if (string.Equals(runStatus, "CONFIRMED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Preview không ghi đè phiên CONFIRMED. Mở lại (REOPEN) trước khi tính thử ghi DB.");
    }

    /// <summary>
    /// Confirm lặp cùng RUN_VERSION đã DONE: idempotent — không tạo kết quả mới.
    /// </summary>
    public static bool IsDuplicateConfirm(string? stepStatus, int requestedVersion, int confirmedVersion) =>
        string.Equals(stepStatus, "DONE", StringComparison.OrdinalIgnoreCase)
        && requestedVersion == confirmedVersion;
}

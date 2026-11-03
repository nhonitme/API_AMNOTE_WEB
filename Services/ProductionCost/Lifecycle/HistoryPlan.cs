namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

public static class HistoryActionCode
{
    public const string Reopen = "REOPEN";
    public const string Preview = "PREVIEW";
    public const string Recalculate = "RECALCULATE";
    public const string Confirm = "CONFIRM";
    public const string Compare = "COMPARE";
    public const string Error = "ERROR";
}

public static class HistoryPlan
{
    public enum WriteAction
    {
        InsertOnce,
        NoopAlreadyWritten
    }

    public sealed class WritePlan
    {
        public WriteAction Action { get; init; }
        public string StableKey { get; init; } = string.Empty;
        public string OperationId { get; init; } = string.Empty;
        public bool WritesHistory => Action == WriteAction.InsertOnce;
    }

    /// <summary>
    /// Khóa history theo OperationId — hai RECALCULATE hợp lệ cùng VERSION được phân biệt.
    /// </summary>
    public static string StableKey(
        string companyCd,
        long runId,
        int runVersion,
        string stepCode,
        string actionCode,
        string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("OperationId bắt buộc cho history idempotency.");

        return $"PC_HIST:{companyCd}:{runId}:v{runVersion}:{stepCode}:{actionCode}:op:{operationId.Trim()}";
    }

    public static string RequireOperationId(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Trim().Length < 8)
            throw new ArgumentException("OperationId bắt buộc (tối thiểu 8 ký tự) cho mỗi thao tác lifecycle.");
        return operationId.Trim();
    }

    /// <summary>
    /// Retry cùng OperationId đã persist (từ DB) → Noop.
    /// Client tự khai "đã có history" không đủ — Confirm phải đối chiếu OperationId trong DB.
    /// </summary>
    public static WritePlan PlanWrite(
        string stableKey,
        string operationId,
        IReadOnlyCollection<string>? persistedOperationIdsFromDb)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("History STABLE_KEY bắt buộc.");

        var op = RequireOperationId(operationId);
        var known = persistedOperationIdsFromDb ?? Array.Empty<string>();
        var already = known.Any(id =>
            string.Equals((id ?? string.Empty).Trim(), op, StringComparison.OrdinalIgnoreCase));

        if (already)
        {
            return new WritePlan
            {
                Action = WriteAction.NoopAlreadyWritten,
                StableKey = stableKey,
                OperationId = op
            };
        }

        return new WritePlan
        {
            Action = WriteAction.InsertOnce,
            StableKey = stableKey,
            OperationId = op
        };
    }
}

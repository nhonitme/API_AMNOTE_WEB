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
        public bool WritesHistory => Action == WriteAction.InsertOnce;
    }

    public static string StableKey(
        string companyCd,
        long runId,
        int runVersion,
        string stepCode,
        string actionCode) =>
        $"PC_HIST:{companyCd}:{runId}:v{runVersion}:{stepCode}:{actionCode}";

    /// <summary>Retry cùng STABLE_KEY → không ghi trùng history.</summary>
    public static WritePlan PlanWrite(string stableKey, bool alreadyWrittenSameKey)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
            throw new ArgumentException("History STABLE_KEY bắt buộc.");

        if (alreadyWrittenSameKey)
        {
            return new WritePlan
            {
                Action = WriteAction.NoopAlreadyWritten,
                StableKey = stableKey
            };
        }

        return new WritePlan
        {
            Action = WriteAction.InsertOnce,
            StableKey = stableKey
        };
    }
}

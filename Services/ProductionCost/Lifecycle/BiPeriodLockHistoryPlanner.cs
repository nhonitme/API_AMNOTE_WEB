using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Lập kế hoạch ghi history EXECUTE cho bước mirror PeriodLock B-I (B1 / B6).
/// Không chạy lại nghiệp vụ khóa sổ — chỉ xác nhận B-I DONE và ghi nhật ký idempotent.
/// </summary>
public static class BiPeriodLockHistoryPlanner
{
    public sealed class Plan
    {
        public string CompanyCd { get; init; } = string.Empty;
        public string PeriodYm { get; init; } = string.Empty;
        public long RunId { get; init; }
        public int RunVersion { get; init; }
        public string OperationId { get; init; } = string.Empty;
        public string StepCode { get; init; } = string.Empty;
        public int StepIndex { get; init; }
        public string HistoryStableKey { get; init; } = string.Empty;
        public string ContentFingerprint { get; init; } = string.Empty;
        public bool IsNoop { get; init; }
        public bool WouldWriteHistory { get; init; }
        public string? IdempotencyAction { get; init; }
        public string Message { get; init; } = string.Empty;
        public HistoryEntryPlan? History { get; init; }
    }

    public static (int StepIndex, string StepCode, string BiStepCode) ResolveStep(string stepCode)
    {
        var code = (stepCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code == ProductionCostStepCode.FaPrepaidLock
            || code == ProductionCostBiStepCode.FaPrepaidLock)
        {
            return (1, ProductionCostStepCode.FaPrepaidLock, ProductionCostBiStepCode.FaPrepaidLock);
        }

        if (code == ProductionCostStepCode.ProfitLossReport
            || code == ProductionCostBiStepCode.ProfitLossReport)
        {
            return (6, ProductionCostStepCode.ProfitLossReport, ProductionCostBiStepCode.ProfitLossReport);
        }

        throw new ArgumentException(
            "Chỉ hỗ trợ ghi history PeriodLock cho FA_PREPAID_LOCK hoặc PROFIT_LOSS_REPORT.");
    }

    public static string BuildContentFingerprint(
        string periodYm,
        int runVersion,
        string biStatus,
        DateTime? biFinishedAt,
        string? biUpdatedBy)
    {
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");
        if (runVersion <= 0)
            throw new ArgumentException("RUN_VERSION phải > 0.");

        var inv = CultureInfo.InvariantCulture;
        var finished = biFinishedAt?.ToUniversalTime().ToString("o", inv) ?? "null";
        var by = string.IsNullOrWhiteSpace(biUpdatedBy) ? "-" : biUpdatedBy.Trim();
        var canonical =
            $"BI_LOCK|p{periodYm.Trim()}|v{runVersion.ToString(inv)}|st={(biStatus ?? string.Empty).Trim().ToUpperInvariant()}|fin={finished}|by={by}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }

    public static Plan Build(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        string stepCode,
        string operationId,
        string biStatus,
        DateTime? biFinishedAt,
        string? biUpdatedBy,
        bool alreadyHasExecuteHistory,
        PersistedHistoryRef? persistedHistoryFromDb,
        string? userId)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        var (stepIndex, normalizedStep, _) = ResolveStep(stepCode);
        if (!string.Equals(biStatus?.Trim(), "DONE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(biStatus?.Trim(), ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"B-I {normalizedStep} chưa DONE — không ghi history EXECUTE.");
        }

        var op = HistoryPlan.RequireOperationId(operationId);
        var statusForFp = string.IsNullOrWhiteSpace(biStatus) ? "DONE" : biStatus.Trim();
        var fp = BuildContentFingerprint(periodYm, runVersion, statusForFp, biFinishedAt, biUpdatedBy);
        var historyKey = HistoryPlan.StableKey(
            companyCd, runId, runVersion, normalizedStep, HistoryActionCode.Execute, op);

        var persisted = persistedHistoryFromDb == null
            ? Array.Empty<PersistedHistoryRef>()
            : new[] { persistedHistoryFromDb };

        var historyPlan = HistoryPlan.PlanWrite(
            historyKey, op, persisted, fp, requireContentFingerprint: true);

        if (historyPlan.Action == HistoryPlan.WriteAction.NoopAlreadyWritten)
        {
            return new Plan
            {
                CompanyCd = companyCd.Trim(),
                PeriodYm = periodYm.Trim(),
                RunId = runId,
                RunVersion = runVersion,
                OperationId = op,
                StepCode = normalizedStep,
                StepIndex = stepIndex,
                HistoryStableKey = historyKey,
                ContentFingerprint = fp,
                IsNoop = true,
                WouldWriteHistory = false,
                IdempotencyAction = historyPlan.Action.ToString(),
                Message = $"History EXECUTE {normalizedStep} idempotent — cùng OperationId + fingerprint.",
                History = null
            };
        }

        // Đã có EXECUTE hiện hành (sau REOPEN gần nhất trên cùng RUN_VERSION) → không ghi trùng
        // khi PeriodLock trả DONE vì bước đã khóa từ trước (no-op). Caller phải truyền cờ
        // đã loại EXECUTE bị REOPEN «che» — xem HasStepExecuteHistoryAsync.
        if (alreadyHasExecuteHistory)
        {
            return new Plan
            {
                CompanyCd = companyCd.Trim(),
                PeriodYm = periodYm.Trim(),
                RunId = runId,
                RunVersion = runVersion,
                OperationId = op,
                StepCode = normalizedStep,
                StepIndex = stepIndex,
                HistoryStableKey = historyKey,
                ContentFingerprint = fp,
                IsNoop = true,
                WouldWriteHistory = false,
                IdempotencyAction = HistoryPlan.WriteAction.NoopAlreadyWritten.ToString(),
                Message =
                    $"B-I {normalizedStep} đã DONE và đã có history EXECUTE cho RUN_VERSION={runVersion} — không ghi trùng.",
                History = null
            };
        }

        var historyEntry = new HistoryEntryPlan
        {
            StableKey = historyKey,
            OperationId = op,
            ActionCode = HistoryActionCode.Execute,
            StepCode = normalizedStep,
            StepIndex = stepIndex,
            RunId = runId,
            RunVersionBefore = runVersion,
            RunVersionAfter = runVersion,
            StatusBefore = null,
            StatusAfter = ProductionCostStepStatus.Done,
            Reason = null,
            UserId = userId,
            ActionAtUtc = DateTime.UtcNow,
            IdempotencyAction = historyPlan.Action.ToString(),
            WritesHistory = true,
            ClientHistoryHintIsNotAuthoritative = true
        };

        return new Plan
        {
            CompanyCd = companyCd.Trim(),
            PeriodYm = periodYm.Trim(),
            RunId = runId,
            RunVersion = runVersion,
            OperationId = op,
            StepCode = normalizedStep,
            StepIndex = stepIndex,
            HistoryStableKey = historyKey,
            ContentFingerprint = fp,
            IsNoop = false,
            WouldWriteHistory = true,
            IdempotencyAction = historyPlan.Action.ToString(),
            Message = $"Ghi history EXECUTE {normalizedStep} sau khi B-I xác nhận DONE.",
            History = historyEntry
        };
    }
}

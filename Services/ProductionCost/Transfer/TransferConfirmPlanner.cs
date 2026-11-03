using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

namespace API_AMNOTE_WEB.Services.ProductionCost.Transfer;

/// <summary>
/// Lập kế hoạch Confirm B3 EXP_TO_154_ONLY — pure, không DB.
/// Chỉ N154/C621–627; không 154→155/632; không CONFIRMED toàn kỳ.
/// </summary>
public static class TransferConfirmPlanner
{
    public const string StepCode = ProductionCostStepCode.ExpTo154Only;
    public const int StepIndex = 3;
    public const string BiStepCode = ProductionCostBiStepCode.CogsSummary;
    public const string CogsRule = ProductionCostCogsRule.ExpTo154Only;

    public sealed class Plan
    {
        public string CompanyCd { get; init; } = string.Empty;
        public string PeriodYm { get; init; } = string.Empty;
        public long RunId { get; init; }
        public int RunVersion { get; init; }
        public string OperationId { get; init; } = string.Empty;
        public string HistoryStableKey { get; init; } = string.Empty;
        public string VoucherStableKey { get; init; } = string.Empty;
        public string ContentFingerprint { get; init; } = string.Empty;
        public string? PriorRunStatus { get; init; }
        public string ExpectedRunStatusAfter { get; init; } = GkRunStatus.AllocConfirmed;
        public bool IsNoop { get; init; }
        public bool WouldCallPeriodLockCogs { get; init; }
        public bool WouldMarkStepDone { get; init; }
        public bool WouldWriteHistory { get; init; }
        public bool WouldCreateDownstream155Or632 { get; init; } // luôn false
        public bool WouldPromoteFullPeriodConfirmed { get; init; } // luôn false
        public string? IdempotencyAction { get; init; }
        public string Message { get; init; } = string.Empty;
        public HistoryEntryPlan? History { get; init; }
        public decimal ConfirmedAllocTotal { get; init; }
        public IReadOnlyList<(string Debit, string Credit)> AllowedSampleLines { get; init; } =
            Array.Empty<(string, string)>();
    }

    /// <summary>
    /// Fingerprint canonical — dùng engine/rule <b>yêu cầu</b> (GK + EXP_TO_154_ONLY),
    /// không phụ thuộc COST_ENGINE trước/sau claim (tránh Noop lệch sau Confirm).
    /// </summary>
    public static string BuildContentFingerprint(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        decimal confirmedAllocTotal)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("B3|p").Append(periodYm.Trim())
            .Append("|c").Append(companyCd.Trim())
            .Append("|r").Append(runId.ToString(inv))
            .Append("|v").Append(runVersion.ToString(inv))
            .Append("|rule=").Append(CogsRule)
            .Append("|eng=").Append(ProductionCostEngine.Gk)
            .Append("|alloc=").Append(confirmedAllocTotal.ToString("0.00", inv))
            .Append("|down=0|full=0");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))
            .ToLowerInvariant();
    }

    public static Plan Build(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        string? runStatus,
        IReadOnlyDictionary<string, string> stepStatuses,
        string operationId,
        string? costEngine,
        string? periodCogsRule,
        string? biCogsStepStatus,
        decimal confirmedAllocTotal,
        PersistedHistoryRef? persistedHistoryFromDb,
        string? userId = null,
        string? clientFingerprint = null)
    {
        TransferConfirmAtomicity.EnsureSharedSessionAvailable();
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        ExpTo154Rules.AssertRuleCode(CogsRule);
        ExpTo154Rules.AssertNoDownstreamTransfer(CogsRule);

        stepStatuses.TryGetValue(ProductionCostStepCode.CostAllocation, out var allocStep);
        ExpTo154Rules.AssertAllocationConfirmed(allocStep, confirmedAllocTotal, runStatus);

        CostEngineExclusive.AssertTransferAllowed(
            costEngine,
            periodCogsRule,
            CogsRule,
            biCogsStepStatus);

        // Không hạ / không nâng CONFIRMED toàn kỳ ở B3.
        var expectedStatus = string.Equals(runStatus, GkRunStatus.Confirmed, StringComparison.OrdinalIgnoreCase)
            ? GkRunStatus.Confirmed
            : GkRunStatus.AllocConfirmed;
        if (GkRunStatus.IsFullPeriodConfirmed(expectedStatus)
            && !GkRunStatus.IsFullPeriodConfirmed(runStatus))
        {
            throw new InvalidOperationException("B3 không được chuyển RUN thành CONFIRMED toàn kỳ.");
        }

        var op = HistoryPlan.RequireOperationId(operationId);
        var voucherKey = ExpTo154Rules.StableKey(companyCd, periodYm);
        var historyKey = HistoryPlan.StableKey(
            companyCd, runId, runVersion, StepCode, HistoryActionCode.Confirm, op);
        var fp = string.IsNullOrWhiteSpace(clientFingerprint)
            ? BuildContentFingerprint(companyCd, periodYm, runId, runVersion, confirmedAllocTotal)
            : HistoryPlan.NormalizeContentFingerprint(clientFingerprint, required: true)!;

        var persisted = persistedHistoryFromDb == null
            ? Array.Empty<PersistedHistoryRef>()
            : new[] { persistedHistoryFromDb };

        var historyPlan = HistoryPlan.PlanWrite(
            historyKey, op, persisted, fp, requireContentFingerprint: true);

        var sampleLines = new (string Debit, string Credit)[]
        {
            ("154", "621"),
            ("154", "622"),
            ("154", "623"),
            ("154", "627")
        };
        ExpTo154Rules.AssertLinesAllowed(sampleLines);
        // Cấm tuyệt đối downstream trong plan.
        AssertNoForbiddenDownstreamSamples();

        if (historyPlan.Action == HistoryPlan.WriteAction.NoopAlreadyWritten)
        {
            return new Plan
            {
                CompanyCd = companyCd.Trim(),
                PeriodYm = periodYm.Trim(),
                RunId = runId,
                RunVersion = runVersion,
                OperationId = op,
                HistoryStableKey = historyKey,
                VoucherStableKey = voucherKey,
                ContentFingerprint = fp,
                PriorRunStatus = runStatus,
                ExpectedRunStatusAfter = expectedStatus,
                IsNoop = true,
                WouldCallPeriodLockCogs = false,
                WouldMarkStepDone = false,
                WouldWriteHistory = false,
                WouldCreateDownstream155Or632 = false,
                WouldPromoteFullPeriodConfirmed = false,
                IdempotencyAction = historyPlan.Action.ToString(),
                Message = "Confirm B3 idempotent — cùng OperationId + fingerprint; không ghi lại.",
                History = null,
                ConfirmedAllocTotal = confirmedAllocTotal,
                AllowedSampleLines = sampleLines
            };
        }

        stepStatuses.TryGetValue(StepCode, out var b3Status);
        if (string.Equals(b3Status, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(historyPlan.Action.ToString(), nameof(HistoryPlan.WriteAction.NoopAlreadyWritten),
                StringComparison.Ordinal))
        {
            // Step DONE nhưng OpId mới → vẫn cho phép nếu chưa có history conflict (retry khác OpId hiếm).
            // Không tạo CT trùng: PeriodLock create_voucher idempotent theo kỳ + voucher_ref STABLE_KEY.
        }

        var historyEntry = new HistoryEntryPlan
        {
            StableKey = historyKey,
            OperationId = op,
            ActionCode = HistoryActionCode.Confirm,
            StepCode = StepCode,
            StepIndex = StepIndex,
            RunId = runId,
            RunVersionBefore = runVersion,
            RunVersionAfter = runVersion,
            StatusBefore = runStatus,
            StatusAfter = expectedStatus,
            Reason = $"EXP_TO_154_ONLY; alloc={confirmedAllocTotal.ToString("0.00", CultureInfo.InvariantCulture)}",
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
            HistoryStableKey = historyKey,
            VoucherStableKey = voucherKey,
            ContentFingerprint = fp,
            PriorRunStatus = runStatus,
            ExpectedRunStatusAfter = expectedStatus,
            IsNoop = false,
            WouldCallPeriodLockCogs = true,
            WouldMarkStepDone = true,
            WouldWriteHistory = true,
            WouldCreateDownstream155Or632 = false,
            WouldPromoteFullPeriodConfirmed = false,
            IdempotencyAction = historyPlan.Action.ToString(),
            Message = "Plan Confirm B3 — PeriodLock EXP_TO_154_ONLY trên cùng TX + STEP DONE + history.",
            History = historyEntry,
            ConfirmedAllocTotal = confirmedAllocTotal,
            AllowedSampleLines = sampleLines
        };
    }

    public static bool ShouldCommitHistory(bool transferSucceeded, bool stepMarked) =>
        ConfirmIdempotency.ShouldWriteSuccessHistory(transferSucceeded, stepMarked);

    private static void AssertNoForbiddenDownstreamSamples()
    {
        var forbidden = new (string Debit, string Credit)[]
        {
            ("155", "154"),
            ("632", "154"),
            ("632", "155"),
            ("154", "155"),
            ("154", "632")
        };
        foreach (var (d, c) in forbidden)
        {
            if (!ExpTo154Rules.IsForbiddenDownstreamLine(d, c))
                throw new InvalidOperationException($"Ma trận cấm B3 thiếu {d}/{c}.");
            if (ExpTo154Rules.IsValidExpLine(d, c))
                throw new InvalidOperationException($"Bút toán cấm {d}/{c} không được coi hợp lệ EXP.");
        }
    }
}

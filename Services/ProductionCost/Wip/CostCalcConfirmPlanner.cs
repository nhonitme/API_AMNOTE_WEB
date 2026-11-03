using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

namespace API_AMNOTE_WEB.Services.ProductionCost.Wip;

/// <summary>
/// Lập kế hoạch Confirm B4 COST_CALC — pure, không DB.
/// Persist source/wip/receipt; COST_CALC=DONE only; không apply kho / 632; không CONFIRMED toàn kỳ.
/// </summary>
public static class CostCalcConfirmPlanner
{
    public const string StepCode = ProductionCostStepCode.CostCalc;
    public const int StepIndex = 4;

    public sealed class Plan
    {
        public string CompanyCd { get; init; } = string.Empty;
        public string PeriodYm { get; init; } = string.Empty;
        public long RunId { get; init; }
        public int RunVersion { get; init; }
        public string OperationId { get; init; } = string.Empty;
        public string HistoryStableKey { get; init; } = string.Empty;
        public string ContentFingerprint { get; init; } = string.Empty;
        public string? PriorRunStatus { get; init; }
        public string ExpectedRunStatusAfter { get; init; } = GkRunStatus.AllocConfirmed;
        public bool IsNoop { get; init; }
        public bool WouldPersistSourceWipReceipt { get; init; }
        public bool WouldMarkStepDone { get; init; }
        public bool WouldWriteHistory { get; init; }
        public bool WouldApplyInventoryPrice { get; init; } // luôn false ở B4
        public bool WouldPost632 { get; init; } // luôn false ở B4
        public bool WouldPromoteFullPeriodConfirmed { get; init; } // luôn false
        public string? IdempotencyAction { get; init; }
        public string Message { get; init; } = string.Empty;
        public HistoryEntryPlan? History { get; init; }
        public string WipMethod { get; init; } = string.Empty;
        public string IncompleteMode { get; init; } = string.Empty;
        public decimal TotalCostPriceSum { get; init; }
        public decimal ReceiptNewAmountSum { get; init; }
        public int WipLineCount { get; init; }
        public int ReceiptLineCount { get; init; }
        public int SourceLineCount { get; init; }
    }

    public static string BuildContentFingerprint(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        string wipMethod,
        string incompleteMode,
        IReadOnlyList<WipCalcResult> wip,
        IReadOnlyList<ReceiptPricePrepLine> receipts,
        IReadOnlyList<CostSourceLine> sources)
    {
        ArgumentNullException.ThrowIfNull(wip);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(sources);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("B4|p").Append(periodYm.Trim())
            .Append("|c").Append(companyCd.Trim())
            .Append("|r").Append(runId.ToString(inv))
            .Append("|v").Append(runVersion.ToString(inv))
            .Append("|m=").Append((wipMethod ?? string.Empty).Trim())
            .Append("|mode=").Append((incompleteMode ?? string.Empty).Trim())
            .Append("|wip=").Append(wip.Count.ToString(inv))
            .Append("|rec=").Append(receipts.Count.ToString(inv))
            .Append("|src=").Append(sources.Count.ToString(inv))
            .Append("|inv=0|632=0|full=0");

        foreach (var line in sources
                     .OrderBy(x => x.ProductId)
                     .ThenBy(x => x.DepartmentId)
                     .ThenBy(x => x.SourceKind, StringComparer.Ordinal))
        {
            sb.Append("|S:")
                .Append(line.ProductId.ToString(inv))
                .Append(':').Append(line.DepartmentId.ToString(inv))
                .Append(':').Append((line.SourceKind ?? string.Empty).Trim())
                .Append('=')
                .Append(line.Amount.ToString("0.00", inv));
        }

        foreach (var row in wip
                     .OrderBy(x => x.ProductId)
                     .ThenBy(x => x.DepartmentId))
        {
            sb.Append("|W:")
                .Append(row.ProductId.ToString(inv))
                .Append(':').Append(row.DepartmentId.ToString(inv))
                .Append('=')
                .Append(row.TotalCostPrice.ToString("0.00", inv))
                .Append('/')
                .Append(row.UnitCost.ToString("0.######", inv))
                .Append('/')
                .Append(row.Quantity.ToString("0.######", inv))
                .Append('/')
                .Append(row.ClosingIncompleteAmount154.ToString("0.00", inv))
                .Append('/')
                .Append(row.Amount154Direct.ToString("0.00", inv));
        }

        foreach (var r in receipts.OrderBy(x => x.InputId))
        {
            sb.Append("|R:")
                .Append(r.InputId.ToString(inv))
                .Append(':').Append(r.ProductId.ToString(inv))
                .Append(':').Append(r.DepartmentId.ToString(inv))
                .Append('=')
                .Append(r.Qty.ToString("0.######", inv))
                .Append('/')
                .Append(r.NewAmount.ToString("0.00", inv))
                .Append('/')
                .Append(r.NewUnitPrice.ToString("0.######", inv))
                .Append("/applied=").Append(r.IsApplied ? "1" : "0");
        }

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
        WipRunConfig config,
        IReadOnlyList<WipCalcResult> wip,
        IReadOnlyList<ReceiptPricePrepLine> receipts,
        IReadOnlyList<CostSourceLine> sources,
        PersistedHistoryRef? persistedHistoryFromDb,
        string? userId = null,
        string? clientFingerprint = null,
        bool hasB4ConfirmHistory = false)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(wip);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(sources);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        AssertPrerequisites(stepStatuses, runStatus);
        CostSourceRules.AssertNoTransferInIncurred(sources);

        if (wip.Any(i => i.HasErrors))
        {
            var first = wip.First(i => i.HasErrors).Errors[0];
            throw new InvalidOperationException(first);
        }

        if (receipts.Any(r => r.IsApplied))
        {
            throw new InvalidOperationException(
                "B4 không được đánh IsApplied=true — cập nhật giá kho chỉ ở B5.");
        }

        // Confirm: thiếu / lệch receipt → abort (ReadyToApply).
        ReceiptReconcile.AssertMatches(wip, receipts, ReceiptReconcileMode.ReadyToApply);

        WipRecalcGuard.EnsureCanOverwriteWip(
            companyCd, runId, runVersion, runVersion, runStatus, targetingConfirmedVersion: false);

        var expectedStatus = GkRunStatus.AfterCostCalcConfirm(runStatus);
        if (GkRunStatus.IsFullPeriodConfirmed(expectedStatus)
            && !GkRunStatus.IsFullPeriodConfirmed(runStatus))
        {
            throw new InvalidOperationException("B4 không được chuyển RUN thành CONFIRMED toàn kỳ.");
        }

        var op = HistoryPlan.RequireOperationId(operationId);
        var historyKey = HistoryPlan.StableKey(
            companyCd, runId, runVersion, StepCode, HistoryActionCode.Confirm, op);
        var totalCost = AllocationMath.RoundDigit(wip.Sum(x => x.TotalCostPrice), 0);
        var receiptSum = AllocationMath.RoundDigit(receipts.Sum(x => x.NewAmount), 0);
        var fp = string.IsNullOrWhiteSpace(clientFingerprint)
            ? BuildContentFingerprint(
                companyCd, periodYm, runId, runVersion,
                config.WipMethod, config.IncompleteMode, wip, receipts, sources)
            : HistoryPlan.NormalizeContentFingerprint(clientFingerprint, required: true)!;

        var persisted = persistedHistoryFromDb == null
            ? Array.Empty<PersistedHistoryRef>()
            : new[] { persistedHistoryFromDb };

        var historyPlan = HistoryPlan.PlanWrite(
            historyKey, op, persisted, fp, requireContentFingerprint: true);

        // 1) Retry cùng OperationId + fingerprint → Noop.
        if (historyPlan.Action == HistoryPlan.WriteAction.NoopAlreadyWritten)
        {
            return NoopPlan(
                companyCd, periodYm, runId, runVersion, op, historyKey, fp,
                runStatus, expectedStatus, config, totalCost, receiptSum,
                wip.Count, receipts.Count, sources.Count,
                nameof(HistoryPlan.WriteAction.NoopAlreadyWritten),
                "Confirm B4 idempotent — cùng OperationId + fingerprint; không ghi lại.");
        }

        stepStatuses.TryGetValue(StepCode, out var b4Status);
        var b4Done = string.Equals(b4Status, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase);

        // 2) B4 đã DONE + OperationId mới → không tính lại / không overwrite snapshot.
        if (b4Done)
        {
            var consistent =
                hasB4ConfirmHistory
                && runVersion > 0
                && (string.Equals(runStatus, GkRunStatus.AllocConfirmed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runStatus, GkRunStatus.Confirmed, StringComparison.OrdinalIgnoreCase));

            if (consistent)
            {
                return NoopPlan(
                    companyCd, periodYm, runId, runVersion, op, historyKey, fp,
                    runStatus, expectedStatus, config, totalCost, receiptSum,
                    wip.Count, receipts.Count, sources.Count,
                    nameof(ConfirmIdempotency.VoucherAction.NoopAlreadyConfirmed),
                    "Confirm B4 đã DONE — OperationId mới không tính lại; " +
                    "bảo toàn source/wip/receipt RUN_VERSION; cần REOPEN hợp lệ để Confirm lại.");
            }

            throw new InvalidOperationException(
                "STEP COST_CALC đã DONE nhưng trạng thái thiếu/mâu thuẫn (history/RUN) — " +
                "không ghi đè snapshot. Cần quy trình REOPEN hợp lệ trước khi Confirm mới.");
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
            Reason =
                $"COST_CALC; method={config.WipMethod}; mode={config.IncompleteMode}; " +
                $"wip={wip.Count}; receipt={receipts.Count}; total={totalCost.ToString("0.00", CultureInfo.InvariantCulture)}",
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
            ContentFingerprint = fp,
            PriorRunStatus = runStatus,
            ExpectedRunStatusAfter = expectedStatus,
            IsNoop = false,
            WouldPersistSourceWipReceipt = true,
            WouldMarkStepDone = true,
            WouldWriteHistory = true,
            WouldApplyInventoryPrice = false,
            WouldPost632 = false,
            WouldPromoteFullPeriodConfirmed = false,
            IdempotencyAction = historyPlan.Action.ToString(),
            Message =
                "Plan Confirm B4 — persist source/wip/receipt + COST_CALC=DONE; " +
                "không apply kho; không post 632; không CONFIRMED toàn kỳ.",
            History = historyEntry,
            WipMethod = config.WipMethod,
            IncompleteMode = config.IncompleteMode,
            TotalCostPriceSum = totalCost,
            ReceiptNewAmountSum = receiptSum,
            WipLineCount = wip.Count,
            ReceiptLineCount = receipts.Count,
            SourceLineCount = sources.Count
        };
    }

    public static bool ShouldCommitHistory(bool persistSucceeded, bool stepMarked) =>
        ConfirmIdempotency.ShouldWriteSuccessHistory(persistSucceeded, stepMarked);

    public static void AssertPrerequisites(
        IReadOnlyDictionary<string, string> stepStatuses,
        string? runStatus)
    {
        ArgumentNullException.ThrowIfNull(stepStatuses);

        stepStatuses.TryGetValue(ProductionCostStepCode.CostAllocation, out var b2);
        if (!string.Equals(b2, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "B2 COST_ALLOCATION phải DONE trước Confirm B4.");
        }

        stepStatuses.TryGetValue(ProductionCostStepCode.ExpTo154Only, out var b3);
        if (!string.Equals(b3, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "B3 EXP_TO_154_ONLY phải DONE trước Confirm B4.");
        }

        if (!WipRecalcGuard.AllowsWipRecalc(runStatus))
        {
            throw new InvalidOperationException(
                "Trạng thái RUN không cho phép Confirm B4 (cần ALLOC_CONFIRMED/REOPENED/DRAFT/PREVIEWED).");
        }
    }

    private static Plan NoopPlan(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        string op,
        string historyKey,
        string fp,
        string? runStatus,
        string expectedStatus,
        WipRunConfig config,
        decimal totalCost,
        decimal receiptSum,
        int wipCount,
        int receiptCount,
        int sourceCount,
        string idempotencyAction,
        string message) =>
        new()
        {
            CompanyCd = companyCd.Trim(),
            PeriodYm = periodYm.Trim(),
            RunId = runId,
            RunVersion = runVersion,
            OperationId = op,
            HistoryStableKey = historyKey,
            ContentFingerprint = fp,
            PriorRunStatus = runStatus,
            ExpectedRunStatusAfter = expectedStatus,
            IsNoop = true,
            WouldPersistSourceWipReceipt = false,
            WouldMarkStepDone = false,
            WouldWriteHistory = false,
            WouldApplyInventoryPrice = false,
            WouldPost632 = false,
            WouldPromoteFullPeriodConfirmed = false,
            IdempotencyAction = idempotencyAction,
            Message = message,
            History = null,
            WipMethod = config.WipMethod,
            IncompleteMode = config.IncompleteMode,
            TotalCostPriceSum = totalCost,
            ReceiptNewAmountSum = receiptSum,
            WipLineCount = wipCount,
            ReceiptLineCount = receiptCount,
            SourceLineCount = sourceCount
        };
}

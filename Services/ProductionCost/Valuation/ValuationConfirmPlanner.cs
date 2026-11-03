using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;
using API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;
using API_AMNOTE_WEB.Services.ProductionCost.Wip;

namespace API_AMNOTE_WEB.Services.ProductionCost.Valuation;

/// <summary>
/// Lập kế hoạch Confirm B5 VALUATION — pure, không DB.
/// Apply giá INPUT_ID + tính xuất + UPSERT VALUATION_COGS; VALUATION=DONE; không CONFIRMED toàn kỳ.
/// </summary>
public static class ValuationConfirmPlanner
{
    public const string StepCode = ProductionCostStepCode.Valuation;
    public const int StepIndex = 5;
    public const string CogsRefRole = "VALUATION_COGS";

    public sealed class Plan
    {
        public string CompanyCd { get; init; } = string.Empty;
        public string PeriodYm { get; init; } = string.Empty;
        public long RunId { get; init; }
        public int RunVersion { get; init; }
        public string OperationId { get; init; } = string.Empty;
        public string HistoryStableKey { get; init; } = string.Empty;
        public string CogsStableKey { get; init; } = string.Empty;
        public string ContentFingerprint { get; init; } = string.Empty;
        public string? PriorRunStatus { get; init; }
        public string ExpectedRunStatusAfter { get; init; } = GkRunStatus.AllocConfirmed;
        public bool IsNoop { get; init; }
        public bool WouldApplyInventoryPrice { get; init; }
        public bool WouldRecalcOutPrice { get; init; }
        public bool WouldPost632 { get; init; }
        public bool WouldMarkReceiptsApplied { get; init; }
        public bool WouldMarkStepDone { get; init; }
        public bool WouldWriteHistory { get; init; }
        public bool WouldPromoteFullPeriodConfirmed { get; init; }
        public bool ExistingValuationCogsVoucher { get; init; }
        public string? IdempotencyAction { get; init; }
        public string Message { get; init; } = string.Empty;
        public HistoryEntryPlan? History { get; init; }
        public string ValuationMethod { get; init; } = string.Empty;
        public ValuationTransactionPlan? TransactionPlan { get; init; }
        public decimal CogsDeltaSum { get; init; }
        public int ReceiptLineCount { get; init; }
    }

    public static string BuildContentFingerprint(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        string valuationMethod,
        IReadOnlyList<ReceiptPricePrepLine> receipts,
        IReadOnlyList<Cogs632PreviewLine> cogs)
    {
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(cogs);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        // Không nhúng exist632 — trạng thái voucher_ref đổi sau Confirm đầu → sẽ phá idempotent retry.
        sb.Append("B5|p").Append(periodYm.Trim())
            .Append("|c").Append(companyCd.Trim())
            .Append("|r").Append(runId.ToString(inv))
            .Append("|v").Append(runVersion.ToString(inv))
            .Append("|m=").Append((valuationMethod ?? string.Empty).Trim())
            .Append("|rec=").Append(receipts.Count.ToString(inv))
            .Append("|cogs=").Append(cogs.Count.ToString(inv))
            .Append("|inv=1|632=1|full=0");

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
                .Append(r.NewUnitPrice.ToString("0.######", inv));
        }

        foreach (var g in cogs
                     .OrderBy(x => x.ProductCd, StringComparer.Ordinal)
                     .ThenBy(x => x.StoreCd, StringComparer.Ordinal)
                     .ThenBy(x => x.ChitType, StringComparer.Ordinal))
        {
            sb.Append("|G:")
                .Append(g.ProductCd ?? string.Empty)
                .Append('|').Append(g.StoreCd ?? string.Empty)
                .Append('|').Append(g.ChitType ?? string.Empty)
                .Append('=')
                .Append(g.CogsAmountBefore.ToString("0.00", inv))
                .Append("->")
                .Append(g.CogsAmountAfter.ToString("0.00", inv))
                .Append('/').Append(g.CogsDelta.ToString("0.00", inv));
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
        string valuationMethod,
        IReadOnlyList<WipCalcResult> confirmedWip,
        IReadOnlyList<ReceiptPricePrepLine> receipts,
        IReadOnlyList<ValuationAverageResult> beforeGroups,
        IReadOnlyList<ValuationAverageResult> afterGroups,
        IReadOnlyList<Cogs632PreviewLine> cogs,
        ValuationTransactionPlan transactionPlan,
        PersistedHistoryRef? persistedHistoryFromDb,
        string? userId = null,
        string? clientFingerprint = null,
        bool hasB5ConfirmHistory = false)
    {
        ProductionCostWriteContext.EnsureCompany(companyCd);
        ProductionCostWriteContext.EnsureRunIdentity(runId, runVersion);
        ArgumentNullException.ThrowIfNull(stepStatuses);
        ArgumentNullException.ThrowIfNull(confirmedWip);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(beforeGroups);
        ArgumentNullException.ThrowIfNull(afterGroups);
        ArgumentNullException.ThrowIfNull(cogs);
        ArgumentNullException.ThrowIfNull(transactionPlan);
        if (string.IsNullOrWhiteSpace(periodYm) || periodYm.Trim().Length != 6)
            throw new ArgumentException("PERIOD_YM phải 6 ký tự.");
        if (string.IsNullOrWhiteSpace(valuationMethod))
            throw new ArgumentException("ValuationMethod bắt buộc.");

        stepStatuses.TryGetValue(ProductionCostStepCode.CostAllocation, out var b2);
        stepStatuses.TryGetValue(ProductionCostStepCode.ExpTo154Only, out var b3);
        stepStatuses.TryGetValue(ProductionCostStepCode.CostCalc, out var b4);

        ValuationPrerequisite.AssertReady(
            companyCd, periodYm, runId, runVersion, runStatus, b2, b3, b4);

        if (confirmedWip.Count == 0)
            throw new InvalidOperationException("Confirm B5 yêu cầu WIP đã COST_CALC.");

        if (receipts.Count == 0)
            throw new InvalidOperationException("Confirm B5 yêu cầu receipt đã chuẩn bị ở B4.");

        ReceiptReconcile.AssertMatches(confirmedWip, receipts, ReceiptReconcileMode.ReadyToApply);

        if (beforeGroups.Count == 0 || afterGroups.Count == 0)
        {
            throw new InvalidOperationException(
                "Confirm B5 yêu cầu kết quả InventoryOutPriceEngine trước/sau định giá (authoritative).");
        }

        if (cogs.Count == 0)
            throw new InvalidOperationException("Confirm B5 yêu cầu preview N632/C155 từ kết quả valuation.");

        if (!transactionPlan.WouldApplyInventory || !transactionPlan.WouldPostCogs632)
        {
            throw new InvalidOperationException(
                "Confirm B5 yêu cầu TransactionPlan WouldApplyInventory + WouldPostCogs632 = true.");
        }

        if (!string.Equals(
                transactionPlan.StableKey,
                ValuationApplyPlan.StableKey(companyCd, periodYm),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"STABLE_KEY plan ({transactionPlan.StableKey}) ≠ VALUATION_COGS:{companyCd}:{periodYm}.");
        }

        var expectedStatus = ValuationPrerequisite.AfterValuationConfirm(runStatus);
        if (GkRunStatus.IsFullPeriodConfirmed(expectedStatus)
            && !GkRunStatus.IsFullPeriodConfirmed(runStatus))
        {
            throw new InvalidOperationException("B5 không được chuyển RUN thành CONFIRMED toàn kỳ.");
        }

        var op = HistoryPlan.RequireOperationId(operationId);
        var historyKey = HistoryPlan.StableKey(
            companyCd, runId, runVersion, StepCode, HistoryActionCode.Confirm, op);
        var cogsKey = ValuationApplyPlan.StableKey(companyCd, periodYm);
        var existingCogs = transactionPlan.ExistingValuationCogsVoucher;
        var cogsDelta = AllocationMath.RoundDigit(cogs.Sum(x => x.CogsDelta), 0);
        var fp = string.IsNullOrWhiteSpace(clientFingerprint)
            ? BuildContentFingerprint(
                companyCd, periodYm, runId, runVersion, valuationMethod, receipts, cogs)
            : HistoryPlan.NormalizeContentFingerprint(clientFingerprint, required: true)!;

        var persisted = persistedHistoryFromDb == null
            ? Array.Empty<PersistedHistoryRef>()
            : new[] { persistedHistoryFromDb };

        var historyPlan = HistoryPlan.PlanWrite(
            historyKey, op, persisted, fp, requireContentFingerprint: true);

        if (historyPlan.Action == HistoryPlan.WriteAction.NoopAlreadyWritten)
        {
            return NoopPlan(
                companyCd, periodYm, runId, runVersion, op, historyKey, cogsKey, fp,
                runStatus, expectedStatus, valuationMethod, transactionPlan, cogsDelta, receipts.Count,
                nameof(HistoryPlan.WriteAction.NoopAlreadyWritten),
                "Confirm B5 idempotent — cùng OperationId + fingerprint; không ghi lại.");
        }

        stepStatuses.TryGetValue(StepCode, out var b5Status);
        var b5Done = string.Equals(b5Status, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase);

        if (b5Done)
        {
            var consistent =
                hasB5ConfirmHistory
                && runVersion > 0
                && (string.Equals(runStatus, GkRunStatus.AllocConfirmed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runStatus, GkRunStatus.Reopened, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(runStatus, GkRunStatus.Confirmed, StringComparison.OrdinalIgnoreCase));

            if (consistent)
            {
                return NoopPlan(
                    companyCd, periodYm, runId, runVersion, op, historyKey, cogsKey, fp,
                    runStatus, expectedStatus, valuationMethod, transactionPlan, cogsDelta, receipts.Count,
                    nameof(ConfirmIdempotency.VoucherAction.NoopAlreadyConfirmed),
                    "Confirm B5 đã DONE — OperationId mới không tính lại / không post lại; " +
                    "bảo toàn snapshot RUN_VERSION; cần REOPEN hợp lệ trước khi Confirm lại.");
            }

            throw new InvalidOperationException(
                "STEP VALUATION đã DONE nhưng trạng thái thiếu/mâu thuẫn (history/RUN) — " +
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
                $"VALUATION; method={valuationMethod}; receipt={receipts.Count}; " +
                $"cogsDelta={cogsDelta.ToString("0.00", CultureInfo.InvariantCulture)}; " +
                $"exist632={(existingCogs ? "1" : "0")}",
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
            CogsStableKey = cogsKey,
            ContentFingerprint = fp,
            PriorRunStatus = runStatus,
            ExpectedRunStatusAfter = expectedStatus,
            IsNoop = false,
            WouldApplyInventoryPrice = true,
            WouldRecalcOutPrice = true,
            WouldPost632 = true,
            WouldMarkReceiptsApplied = true,
            WouldMarkStepDone = true,
            WouldWriteHistory = true,
            WouldPromoteFullPeriodConfirmed = false,
            ExistingValuationCogsVoucher = existingCogs,
            IdempotencyAction = historyPlan.Action.ToString(),
            Message =
                "Plan Confirm B5 — apply INPUT_ID + tính xuất + UPSERT VALUATION_COGS + VALUATION=DONE; " +
                "không CONFIRMED toàn kỳ.",
            History = historyEntry,
            ValuationMethod = valuationMethod.Trim(),
            TransactionPlan = transactionPlan,
            CogsDeltaSum = cogsDelta,
            ReceiptLineCount = receipts.Count
        };
    }

    public static bool ShouldCommitHistory(bool persistSucceeded, bool stepMarked) =>
        ConfirmIdempotency.ShouldWriteSuccessHistory(persistSucceeded, stepMarked);

    private static Plan NoopPlan(
        string companyCd,
        string periodYm,
        long runId,
        int runVersion,
        string op,
        string historyKey,
        string cogsKey,
        string fp,
        string? runStatus,
        string expectedStatus,
        string valuationMethod,
        ValuationTransactionPlan transactionPlan,
        decimal cogsDelta,
        int receiptCount,
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
            CogsStableKey = cogsKey,
            ContentFingerprint = fp,
            PriorRunStatus = runStatus,
            ExpectedRunStatusAfter = expectedStatus,
            IsNoop = true,
            WouldApplyInventoryPrice = false,
            WouldRecalcOutPrice = false,
            WouldPost632 = false,
            WouldMarkReceiptsApplied = false,
            WouldMarkStepDone = false,
            WouldWriteHistory = false,
            WouldPromoteFullPeriodConfirmed = false,
            ExistingValuationCogsVoucher = transactionPlan.ExistingValuationCogsVoucher,
            IdempotencyAction = idempotencyAction,
            Message = message,
            History = null,
            ValuationMethod = valuationMethod.Trim(),
            TransactionPlan = transactionPlan,
            CogsDeltaSum = cogsDelta,
            ReceiptLineCount = receiptCount
        };
}

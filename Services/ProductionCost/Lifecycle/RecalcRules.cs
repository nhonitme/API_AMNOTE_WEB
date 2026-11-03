using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.ProductionCost.Allocation;

namespace API_AMNOTE_WEB.Services.ProductionCost.Lifecycle;

/// <summary>
/// Tính lại bước bị ảnh hưởng — tái sử dụng engine B2/B3/B4/B5; B5 = InventoryOutPriceEngine.
/// B2 đồng bộ <see cref="GkRunStatus.AllowsPoolRecalc"/>.
/// </summary>
public static class RecalcRules
{
    public static RecalcPlan Build(RecalcDryRunPayload dry, string companyCd, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(companyCd))
            throw new ArgumentException("COMPANY_CD bắt buộc.");

        RunVersionRules.AssertWritableVersion(
            companyCd, dry.RunId, dry.WorkingRunVersion, dry.HeaderRunVersion, dry.RunStatus);

        var fromStep = GkStepOrder.Normalize(dry.FromStepCode);
        RunVersionRules.AssertCanRecalcStep(fromStep, dry.RunStatus);

        var statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in dry.StepStatuses ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(kv.Key))
                statuses[GkStepOrder.Normalize(kv.Key)] = (kv.Value ?? string.Empty).Trim();
        }

        foreach (var down in GkStepOrder.Downstream(fromStep))
        {
            if (statuses.TryGetValue(down, out var st) &&
                string.Equals(st, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Không tính lại {fromStep} khi bước sau {down} còn DONE. REOPEN cascade trước.");
            }
        }

        foreach (var up in GkStepOrder.UpstreamInclusive(fromStep).Where(s =>
                     !string.Equals(s, fromStep, StringComparison.OrdinalIgnoreCase)))
        {
            if (!statuses.TryGetValue(up, out var st) ||
                !string.Equals(st, ProductionCostStepStatus.Done, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Không tính lại {fromStep}: bước trước {up} chưa DONE.");
            }
        }

        var affected = GkStepOrder.Forward
            .Skip(GkStepOrder.IndexOf(fromStep))
            .Where(s =>
            {
                if (!statuses.TryGetValue(s, out var st))
                    return string.Equals(s, fromStep, StringComparison.OrdinalIgnoreCase);
                return string.Equals(s, fromStep, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(st, ProductionCostStepStatus.Reopened, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(st, ProductionCostStepStatus.NotStarted, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(st, ProductionCostStepStatus.Error, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        if (affected.Count == 0)
            affected.Add(fromStep);

        // Mọi bước B2 trong danh sách affected cũng phải qua guard pool
        foreach (var step in affected)
            RunVersionRules.AssertCanRecalcStep(step, dry.RunStatus);

        var operationId = HistoryPlan.RequireOperationId(dry.OperationId);
        var engines = affected.Select(DescribeEngine).ToList();
        var historyKey = HistoryPlan.StableKey(
            companyCd, dry.RunId, dry.WorkingRunVersion, fromStep,
            HistoryActionCode.Recalculate, operationId);
        var persisted = HistoryPlan.NormalizePersisted(
            dry.PersistedHistoryFromDb, dry.PersistedOperationIdsFromDb);
        var idem = HistoryPlan.PlanWrite(historyKey, operationId, persisted);
        var actionAt = dry.ActionAtUtc ?? DateTime.UtcNow;

        return new RecalcPlan
        {
            CompanyCd = companyCd.Trim(),
            RunId = dry.RunId,
            WorkingRunVersion = dry.WorkingRunVersion,
            FromStepCode = fromStep,
            AffectedStepsForward = affected,
            EngineBindings = engines,
            PreviewOnly = true,
            WouldTouchAccountingOrInventory = false,
            UsesSharedInventoryOutPriceEngine = affected.Any(s =>
                string.Equals(s, ProductionCostStepCode.Valuation, StringComparison.OrdinalIgnoreCase)),
            History = new HistoryEntryPlan
            {
                StableKey = historyKey,
                OperationId = operationId,
                ActionCode = HistoryActionCode.Recalculate,
                StepCode = fromStep,
                StepIndex = GkStepOrder.IndexOf(fromStep) + 1,
                RunId = dry.RunId,
                RunVersionBefore = dry.WorkingRunVersion,
                RunVersionAfter = dry.WorkingRunVersion,
                StatusBefore = dry.RunStatus,
                StatusAfter = GkRunStatus.Previewed,
                Reason = dry.Reason,
                UserId = userId,
                ActionAtUtc = actionAt,
                IdempotencyAction = idem.Action.ToString(),
                WritesHistory = idem.WritesHistory,
                ClientHistoryHintIsNotAuthoritative = true
            },
            Message = idem.WritesHistory
                ? $"Plan RECALC từ {fromStep}: [{string.Join(", ", affected)}]. " +
                  "Preview không đổi chứng từ KT / kho. B5 dùng InventoryOutPriceEngine chung."
                : "RECALCULATE idempotent — cùng OperationId đã persist; không ghi history trùng."
        };
    }

    private static RecalcEngineBinding DescribeEngine(string stepCode) => stepCode switch
    {
        ProductionCostStepCode.CostAllocation => new RecalcEngineBinding
        {
            StepCode = stepCode,
            EngineName = "AllocationEngine",
            Notes = "B2 — chỉ khi AllowsPoolRecalc; ALLOC_CONFIRMED phải REOPEN trước"
        },
        ProductionCostStepCode.ExpTo154Only => new RecalcEngineBinding
        {
            StepCode = stepCode,
            EngineName = "ExpTo154Rules",
            Notes = "B3 — vẫn chạy khi ALLOC_CONFIRMED nếu tiền đề hợp lệ"
        },
        ProductionCostStepCode.CostCalc => new RecalcEngineBinding
        {
            StepCode = stepCode,
            EngineName = "WipCostEngine",
            Notes = "B4 — WipCostEngine + ReceiptPricePrep"
        },
        ProductionCostStepCode.Valuation => new RecalcEngineBinding
        {
            StepCode = stepCode,
            EngineName = "InventoryOutPriceEngine",
            Notes = "B5 — adapter ValuationAverageCost; 4 PP PERIOD_END_AVG/MOVING_AVG/FIFO/SPECIFIC"
        },
        ProductionCostStepCode.FaPrepaidLock => new RecalcEngineBinding
        {
            StepCode = stepCode,
            EngineName = "PeriodLock.FA_PREPAID_LOCK",
            Notes = "B1 — mirror B-I; không đổi nghiệp vụ B-I"
        },
        ProductionCostStepCode.ProfitLossReport => new RecalcEngineBinding
        {
            StepCode = stepCode,
            EngineName = "PeriodLock.PROFIT_LOSS_REPORT",
            Notes = "B6 — mirror B-I; skeleton"
        },
        _ => new RecalcEngineBinding { StepCode = stepCode, EngineName = "Unknown", Notes = "" }
    };
}

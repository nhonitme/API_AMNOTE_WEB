namespace API_AMNOTE_WEB.Models;

public static class SessionComparePresence
{
    public const string Both = "BOTH";
    public const string ConfirmedOnly = "CONFIRMED_ONLY";
    public const string PreviewOnly = "PREVIEW_ONLY";
}

public sealed class ReopenDryRunPayload
{
    public long RunId { get; set; }
    public int CurrentRunVersion { get; set; }
    public int? RequestedRunVersion { get; set; }
    public string? RunStatus { get; set; }
    public string TargetStepCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    /// <summary>DryRun: client mô phỏng quyền; Confirm phải đọc quyền thật từ DB.</summary>
    public bool HasPermission { get; set; } = true;
    /// <summary>Idempotency — retry cùng OperationId → Noop.</summary>
    public string OperationId { get; set; } = string.Empty;
    public DateTime? ActionAtUtc { get; set; }
    public IReadOnlyDictionary<string, string> StepStatuses { get; set; }
        = new Dictionary<string, string>();
    /// <summary>
    /// OperationId đã persist (legacy dry-run). Confirm phải dùng <see cref="PersistedHistoryFromDb"/>.
    /// </summary>
    public IReadOnlyList<string>? PersistedOperationIdsFromDb { get; set; }
    /// <summary>Hàng history từ DB — OperationId + StableKey (+ fingerprint). Không tin client hint.</summary>
    public IReadOnlyList<PersistedHistoryRef>? PersistedHistoryFromDb { get; set; }
}

/// <summary>Tham chiếu history đã persist — nguồn tin cậy idempotency (COMPANY_CD scope).</summary>
public sealed class PersistedHistoryRef
{
    public string OperationId { get; set; } = string.Empty;
    /// <summary>Null = chỉ biết OpId (legacy dry-run) → Noop khi trùng OpId.</summary>
    public string? StableKey { get; set; }
    /// <summary>Tuỳ chọn — hash nội dung nghiệp vụ; khác → Conflict dù cùng StableKey.</summary>
    public string? ContentFingerprint { get; set; }
}

public sealed class HistoryEntryPlan
{
    public string StableKey { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string StepCode { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public long RunId { get; set; }
    public int? RunVersionBefore { get; set; }
    public int? RunVersionAfter { get; set; }
    public string? StatusBefore { get; set; }
    public string? StatusAfter { get; set; }
    public string? Reason { get; set; }
    public string? UserId { get; set; }
    public DateTime? ActionAtUtc { get; set; }
    public string? IdempotencyAction { get; set; }
    public bool WritesHistory { get; set; }
    /// <summary>Confirm phải đọc OperationId từ DB — không tin client tự khai.</summary>
    public bool ClientHistoryHintIsNotAuthoritative { get; set; } = true;
}

public sealed class ReopenPlan
{
    public string CompanyCd { get; set; } = string.Empty;
    public long RunId { get; set; }
    public int PreservedRunVersion { get; set; }
    public int NewWorkingRunVersion { get; set; }
    /// <summary>CreateNewWorkingVersion | KeepCurrentWorkingVersion | IdempotentRetry</summary>
    public string VersionMode { get; set; } = string.Empty;
    public string TargetStepCode { get; set; } = string.Empty;
    public IReadOnlyList<string> CascadeReverse { get; set; } = Array.Empty<string>();
    public string PriorRunStatus { get; set; } = string.Empty;
    public string NewRunStatus { get; set; } = string.Empty;
    public bool MarkPriorVersionSuperseded { get; set; }
    public bool PreserveConfirmedSnapshots { get; set; } = true;
    public bool WouldDeleteConfirmedData { get; set; }
    public string Reason { get; set; } = string.Empty;
    public HistoryEntryPlan? History { get; set; }
    public string? Message { get; set; }
}

public sealed class RecalcDryRunPayload
{
    public long RunId { get; set; }
    public int WorkingRunVersion { get; set; }
    public int HeaderRunVersion { get; set; }
    public string? RunStatus { get; set; }
    public string FromStepCode { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public DateTime? ActionAtUtc { get; set; }
    public IReadOnlyDictionary<string, string> StepStatuses { get; set; }
        = new Dictionary<string, string>();
    public IReadOnlyList<string>? PersistedOperationIdsFromDb { get; set; }
    public IReadOnlyList<PersistedHistoryRef>? PersistedHistoryFromDb { get; set; }
}

public sealed class RecalcEngineBinding
{
    public string StepCode { get; set; } = string.Empty;
    public string EngineName { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class RecalcPlan
{
    public string CompanyCd { get; set; } = string.Empty;
    public long RunId { get; set; }
    public int WorkingRunVersion { get; set; }
    public string FromStepCode { get; set; } = string.Empty;
    public IReadOnlyList<string> AffectedStepsForward { get; set; } = Array.Empty<string>();
    public IReadOnlyList<RecalcEngineBinding> EngineBindings { get; set; } = Array.Empty<RecalcEngineBinding>();
    public bool PreviewOnly { get; set; } = true;
    public bool WouldTouchAccountingOrInventory { get; set; }
    public bool UsesSharedInventoryOutPriceEngine { get; set; }
    public HistoryEntryPlan? History { get; set; }
    public string? Message { get; set; }
}

public sealed class SessionCompareDryRunPayload
{
    public int ConfirmedRunVersion { get; set; }
    public int PreviewRunVersion { get; set; }
    public IReadOnlyList<WipCalcResult>? ConfirmedWip { get; set; }
    public IReadOnlyList<WipCalcResult>? PreviewWip { get; set; }
    public IReadOnlyList<ReceiptPricePrepLine>? ConfirmedReceipts { get; set; }
    public IReadOnlyList<ReceiptPricePrepLine>? PreviewReceipts { get; set; }
    public IReadOnlyList<ValuationAverageResult>? ConfirmedValuationGroups { get; set; }
    public IReadOnlyList<ValuationAverageResult>? PreviewValuationGroups { get; set; }
}

public sealed class SessionCompareLine
{
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Presence { get; set; } = SessionComparePresence.Both;
    public long ProductId { get; set; }
    public string? ProductCd { get; set; }
    public decimal ConfirmedValue { get; set; }
    public decimal PreviewValue { get; set; }
    public decimal Delta => PreviewValue - ConfirmedValue;
}

public sealed class SessionCompareResult
{
    public int ConfirmedRunVersion { get; set; }
    public int PreviewRunVersion { get; set; }
    public IReadOnlyList<CostCompareLine> WipLines { get; set; } = Array.Empty<CostCompareLine>();
    public IReadOnlyList<SessionCompareLine> ReceiptLines { get; set; } = Array.Empty<SessionCompareLine>();
    public IReadOnlyList<SessionCompareLine> ValuationLines { get; set; } = Array.Empty<SessionCompareLine>();
    public bool HasDifferences { get; set; }
    public string? Message { get; set; }
}

public sealed class ProductionCostLifecycleWriteResponse
{
    public bool WriteEnabled { get; set; }
    public bool Persisted { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public string Action { get; set; } = string.Empty;
    public ReopenPlan? Reopen { get; set; }
    public RecalcPlan? Recalc { get; set; }
    public SessionCompareResult? Compare { get; set; }
    public bool StepStatusesAreClientSimulated { get; set; } = true;
    public string? Message { get; set; }
}

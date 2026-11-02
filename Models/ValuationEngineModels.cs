namespace API_AMNOTE_WEB.Models;

/// <summary>Phương pháp bình quân tái sử dụng cho Preview Bước 5 (không đụng B-I).</summary>
public static class GkValuationMethod
{
    public const string PeriodEndAverage = "PERIOD_END_AVG";
    public const string MovingAverage = "MOVING_AVG";
}

public sealed class ValuationMovementInput
{
    public long ProductId { get; set; }
    public long? DepartmentId { get; set; }
    public long? InputId { get; set; }
    public long? InventoryId { get; set; }
    public string FlowType { get; set; } = string.Empty; // OPEN | INPUT | OUTPUT
    public decimal Quantity { get; set; }
    public decimal AmountCc { get; set; }
    /// <summary>True nếu dòng thuộc CT EXP_TO_154_ONLY — cấm đưa vào tính 632.</summary>
    public bool IsExpTo154Only { get; set; }
    public string? DebitAccountCd { get; set; }
    public string? CreditAccountCd { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
}

public sealed class ValuationAverageResult
{
    public string MethodCode { get; set; } = string.Empty;
    public decimal InputQuantity { get; set; }
    public decimal InputAmount { get; set; }
    public decimal OutputQuantity { get; set; }
    public decimal OutputAmount { get; set; }
    public decimal EndingQuantity { get; set; }
    public decimal EndingAmount { get; set; }
    public decimal EndingAverageUnitCost { get; set; }
}

public sealed class Cogs632PreviewLine
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public decimal OutputQuantity { get; set; }
    public decimal UnitCostBefore { get; set; }
    public decimal UnitCostAfter { get; set; }
    public decimal CogsAmountBefore { get; set; }
    public decimal CogsAmountAfter { get; set; }
    public decimal CogsDelta { get; set; }
    public string DebitAccountHint { get; set; } = "632";
    public string CreditAccountHint { get; set; } = "155";
}

public sealed class ValuationPriceApplyPlanLine
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public long InputId { get; set; }
    public long? InventoryId { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal OldUnitPrice { get; set; }
    public decimal NewUnitPrice { get; set; }
    public decimal OldAmount { get; set; }
    public decimal NewAmount { get; set; }
    public decimal DeltaAmount { get; set; }
    public bool IsApplied { get; set; }
}

public sealed class ValuationTransactionPlan
{
    public string StableKey { get; set; } = string.Empty;
    public string IdempotencyAction { get; set; } = string.Empty;
    public bool AtomicInAndOutRequired { get; set; } = true;
    public bool WouldApplyInventory { get; set; }
    public bool WouldPostCogs632 { get; set; }
    public IReadOnlyList<ValuationPriceApplyPlanLine> PriceLines { get; set; }
        = Array.Empty<ValuationPriceApplyPlanLine>();
    public IReadOnlyList<ValuationPriceApplyPlanLine> RollbackRestoreLines { get; set; }
        = Array.Empty<ValuationPriceApplyPlanLine>();
    public string? Message { get; set; }
}

/// <summary>Dry-run payload Preview Bước 5 VALUATION.</summary>
public sealed class ValuationDryRunPayload
{
    public string ValuationMethod { get; set; } = GkValuationMethod.PeriodEndAverage;
    /// <summary>WIP đã COST_CALC (xác nhận hợp lệ) — nguồn UNIT_COST.</summary>
    public IReadOnlyList<WipCalcResult> ConfirmedWip { get; set; } = Array.Empty<WipCalcResult>();
    /// <summary>Phiếu nhập cần cập nhật giá (đã có Old* từ kho / chứng từ).</summary>
    public IReadOnlyList<ReceiptPricePrepInput> Receipts { get; set; } = Array.Empty<ReceiptPricePrepInput>();
    /// <summary>Chuyển động kho để Preview bình quân / 632 (thuần).</summary>
    public IReadOnlyList<ValuationMovementInput> Movements { get; set; } = Array.Empty<ValuationMovementInput>();
    public string? AllocationStepStatus { get; set; }
    public string? ExpTo154StepStatus { get; set; }
    public string? CostCalcStepStatus { get; set; }
    public string? RunStatus { get; set; }
    public long? ExistingValuationChitId { get; set; }
}

public sealed class ProductionCostValuationWriteResponse
{
    public bool WriteEnabled { get; set; }
    public bool Persisted { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public string StepCode { get; set; } = ProductionCostStepCode.Valuation;
    public string ValuationMethod { get; set; } = string.Empty;
    public IReadOnlyList<ReceiptPricePrepLine> ReceiptPreps { get; set; } = Array.Empty<ReceiptPricePrepLine>();
    public IReadOnlyList<string> ReconcileWarnings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ReconcileErrors { get; set; } = Array.Empty<string>();
    public bool ReadyToApply { get; set; }
    public ValuationAverageResult? AverageBefore { get; set; }
    public ValuationAverageResult? AverageAfter { get; set; }
    public IReadOnlyList<Cogs632PreviewLine> Cogs632Preview { get; set; } = Array.Empty<Cogs632PreviewLine>();
    public ValuationTransactionPlan? TransactionPlan { get; set; }
    public bool InventoryPriceApplied { get; set; }
    public bool CogsVoucherPosted { get; set; }
    public string? Message { get; set; }
}

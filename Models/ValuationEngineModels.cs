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
    public string? ProductCd { get; set; }
    public string? ProductName { get; set; }
    public long? DepartmentId { get; set; }
    public long? StoreId { get; set; }
    public string? StoreCd { get; set; }
    public string? StoreName { get; set; }
    /// <summary>Loại chứng từ kho — khớp nhóm InventoryValuation (CHIT_TYPE).</summary>
    public string? ChitType { get; set; }
    public long? InputId { get; set; }
    public long? InventoryId { get; set; }
    public long? ChitdetailId { get; set; }
    /// <summary>Ngày giao dịch yyyyMMdd — bắt buộc cho MOVING_AVG.</summary>
    public string? TxnYmd { get; set; }
    public string FlowType { get; set; } = string.Empty; // OPEN | INPUT | OUTPUT
    public decimal Quantity { get; set; }
    public decimal AmountCc { get; set; }
    /// <summary>True nếu dòng thuộc CT EXP_TO_154_ONLY — cấm đưa vào tính 632.</summary>
    public bool IsExpTo154Only { get; set; }
    public string? DebitAccountCd { get; set; }
    public string? CreditAccountCd { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
}

public sealed class ValuationGroupKey : IEquatable<ValuationGroupKey>
{
    public string CompanyCd { get; init; } = string.Empty;
    public long ProductId { get; init; }
    public string ProductCd { get; init; } = string.Empty;
    public long StoreId { get; init; }
    public string StoreCd { get; init; } = string.Empty;
    public string ChitType { get; init; } = string.Empty;

    public override string ToString() =>
        $"{CompanyCd}|P{ProductId}:{ProductCd}|S{StoreId}:{StoreCd}|{ChitType}";

    public bool Equals(ValuationGroupKey? other)
    {
        if (other is null) return false;
        return ProductId == other.ProductId
               && StoreId == other.StoreId
               && string.Equals(CompanyCd, other.CompanyCd, StringComparison.OrdinalIgnoreCase)
               && string.Equals(ProductCd, other.ProductCd, StringComparison.OrdinalIgnoreCase)
               && string.Equals(StoreCd, other.StoreCd, StringComparison.OrdinalIgnoreCase)
               && string.Equals(ChitType, other.ChitType, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as ValuationGroupKey);

    public override int GetHashCode() =>
        HashCode.Combine(
            ProductId,
            StoreId,
            CompanyCd?.ToUpperInvariant(),
            ProductCd?.ToUpperInvariant(),
            StoreCd?.ToUpperInvariant(),
            ChitType?.ToUpperInvariant());
}

public sealed class ValuationAverageResult
{
    public string MethodCode { get; set; } = string.Empty;
    public ValuationGroupKey Group { get; set; } = new();
    public long ProductId { get; set; }
    public string? ProductCd { get; set; }
    public long StoreId { get; set; }
    public string? StoreCd { get; set; }
    public string? ChitType { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
    public decimal InputQuantity { get; set; }
    public decimal InputAmount { get; set; }
    public decimal OutputQuantity { get; set; }
    public decimal OutputAmount { get; set; }
    /// <summary>Đơn giá xuất bình quân thực tế = OutputAmount / OutputQuantity (0 nếu không xuất).</summary>
    public decimal OutputAverageUnitCost { get; set; }
    public decimal EndingQuantity { get; set; }
    public decimal EndingAmount { get; set; }
    /// <summary>Đơn giá bình quân tồn cuối — khác OutputAverageUnitCost ở MOVING_AVG.</summary>
    public decimal EndingAverageUnitCost { get; set; }
    public bool IsValid { get; set; } = true;
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}

public sealed class Cogs632PreviewLine
{
    public long ProductId { get; set; }
    public string? ProductCd { get; set; }
    public long DepartmentId { get; set; }
    public long StoreId { get; set; }
    public string? StoreCd { get; set; }
    public string? ChitType { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
    public decimal OutputQuantity { get; set; }
    /// <summary>Đơn giá xuất bình quân trước/sau — dùng cho COGS, không dùng tồn cuối thay thế.</summary>
    public decimal OutputUnitCostBefore { get; set; }
    public decimal OutputUnitCostAfter { get; set; }
    public decimal EndingUnitCostBefore { get; set; }
    public decimal EndingUnitCostAfter { get; set; }
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
    /// <summary>Đã có CHIT VALUATION_COGS theo STABLE_KEY+COMPANY — Confirm phải UPDATE.</summary>
    public bool ExistingValuationCogsVoucher { get; set; }
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
    /// <summary>
    /// Trạng thái bước do client gửi trong DryRun — chỉ mô phỏng Preview.
    /// Confirm thật bắt buộc đọc lại từ DB.
    /// </summary>
    public string? AllocationStepStatus { get; set; }
    public string? ExpTo154StepStatus { get; set; }
    public string? CostCalcStepStatus { get; set; }
    public string? RunStatus { get; set; }
    public long? ExistingValuationChitId { get; set; }
    /// <summary>COMPANY_CD của chứng từ VALUATION_COGS đã có (nếu khác → lỗi).</summary>
    public string? ExistingValuationChitCompanyCd { get; set; }
    public string? ExistingValuationStableKey { get; set; }
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
    /// <summary>Kết quả bình quân từng nhóm (PRODUCT+STORE+CHIT_TYPE) — trước cập nhật giá.</summary>
    public IReadOnlyList<ValuationAverageResult> AverageBeforeGroups { get; set; } = Array.Empty<ValuationAverageResult>();
    public IReadOnlyList<ValuationAverageResult> AverageAfterGroups { get; set; } = Array.Empty<ValuationAverageResult>();
    public IReadOnlyList<Cogs632PreviewLine> Cogs632Preview { get; set; } = Array.Empty<Cogs632PreviewLine>();
    public ValuationTransactionPlan? TransactionPlan { get; set; }
    public bool InventoryPriceApplied { get; set; }
    public bool CogsVoucherPosted { get; set; }
    /// <summary>True khi B2/B3/B4 trong DryRun chỉ là mô phỏng client, chưa xác thực DB.</summary>
    public bool StepStatusesAreClientSimulated { get; set; } = true;
    public string? Message { get; set; }
}

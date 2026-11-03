namespace API_AMNOTE_WEB.Models;

/// <summary>Phương pháp tính dở dang chuẩn (CONG_THUC_TINH_DO_DANG).</summary>
public static class ProductionCostWipMethod
{
    public const string PercentCompleted = "PERCENT_COMPLETED";
    public const string ClosingWipQuantity = "CLOSING_WIP_QUANTITY";
}

/// <summary>Chế độ ngoại lệ theo tài liệu AMnote / WinForms.</summary>
public static class ProductionCostIncompleteMode
{
    public const string Standard = "STANDARD";
    public const string SlTpSx = "incomplete_cost_SL_TP_SX";
    public const string Id2720 = "incomplete_ValueChange_Calculated_ID2720";
}

/// <summary>SOURCE_KIND trên production_cost_source — không cộng XFER vào incurred.</summary>
public static class ProductionCostSourceKind
{
    public const string Src621 = "SRC_621";
    public const string Src6212 = "SRC_6212";
    public const string Src622Alloc = "SRC_622_ALLOC";
    public const string Src627Alloc = "SRC_627_ALLOC";
    public const string Src154Direct = "SRC_154_DIRECT";
    /// <summary>Bút toán EXP_TO_154_ONLY — cấm cộng vào TOTAL_AMOUNT_INCURRED.</summary>
    public const string XferExpTo154 = "XFER_EXP_TO_154";
}

public sealed class IncompleteCostConfig
{
    public string Mode { get; set; } = ProductionCostIncompleteMode.Standard;
    /// <summary>INCOMPLETE_Percent (p): mặc định 0.8; preset 0.7 / 0.5.</summary>
    public decimal IncompletePercent { get; set; } = 0.8m;
    public int SpecialRoundDigit { get; set; }
    public bool IsRoundInventory { get; set; } = true;
    public bool EnableProductReduce { get; set; }
    public bool ShowAllWhenQtyZero { get; set; }
}

public sealed class WipCalcInput
{
    public long ProductId { get; set; }
    public string? ProductCd { get; set; }
    public long DepartmentId { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal IncompleteQtyOpening { get; set; }
    public decimal BeforeAmountResult621 { get; set; }
    public decimal BeforeAmountResult62N { get; set; }
    public decimal Amount621 { get; set; }
    public decimal Amount6212 { get; set; }
    public decimal Amount622 { get; set; }
    public decimal Amount627 { get; set; }
    /// <summary>Phát sinh thẳng 154 — cộng đúng 1 lần; không gồm CT EXP.</summary>
    public decimal Amount154Direct { get; set; }
    public decimal Quantity { get; set; }
    public decimal PercentCompleted { get; set; }
    public decimal ClosingIncompleteQty { get; set; }
    /// <summary>User điền tay CLOSING khi phương pháp % — null = tự tính.</summary>
    public decimal? ClosingAmountOverride { get; set; }
    public decimal ProductReduce { get; set; }
    public decimal PreviousClosing154 { get; set; }
}

public sealed class WipCalcResult
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string? ProductCd { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal BeforeAmount154 { get; set; }
    public decimal Amount621 { get; set; }
    public decimal Amount6212 { get; set; }
    public decimal Amount622 { get; set; }
    public decimal Amount627 { get; set; }
    public decimal Amount154Direct { get; set; }
    public decimal TotalAmountIncurred { get; set; }
    public decimal Quantity { get; set; }
    public decimal PercentCompleted { get; set; }
    public decimal ClosingIncompleteQty { get; set; }
    public decimal IncompleteQtyOpening { get; set; }
    public decimal BeforeAmountResult621 { get; set; }
    public decimal BeforeAmountResult62N { get; set; }
    public decimal AmountResult621 { get; set; }
    public decimal AmountResult62N { get; set; }
    public decimal ClosingIncompleteAmount154 { get; set; }
    public decimal ProductReduce { get; set; }
    public decimal TotalCostPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal QHt { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
    public bool HasErrors => Errors.Count > 0;
}

public sealed class CostSourceLine
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public sealed class ReceiptPricePrepInput
{
    public long ProductId { get; set; }
    /// <summary>Bắt buộc khi cùng PRODUCT có nhiều phòng ban / nhiều UNIT_COST.</summary>
    public long DepartmentId { get; set; }
    public long InputId { get; set; }
    public long? InventoryId { get; set; }
    public string? ProductCd { get; set; }
    public string? InventoryNo { get; set; }
    public decimal Qty { get; set; }
    public decimal OldUnitPrice { get; set; }
    public decimal OldAmount { get; set; }
}

public sealed class ReceiptPricePrepLine
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public long InputId { get; set; }
    public long? InventoryId { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
    public string? ProductCd { get; set; }
    public string? InventoryNo { get; set; }
    public decimal Qty { get; set; }
    public decimal OldUnitPrice { get; set; }
    public decimal NewUnitPrice { get; set; }
    public decimal OldAmount { get; set; }
    public decimal NewAmount { get; set; }
    public decimal DeltaAmount { get; set; }
    /// <summary>Luôn false ở 2C-03 — chưa cập nhật kho thật.</summary>
    public bool IsApplied { get; set; }
}

public sealed class CostCompareLine
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string? ProductCd { get; set; }
    public string Field { get; set; } = string.Empty;
    /// <summary>BOTH | CONFIRMED_ONLY | PREVIEW_ONLY</summary>
    public string Presence { get; set; } = "BOTH";
    public decimal ConfirmedValue { get; set; }
    public decimal PreviewValue { get; set; }
    public decimal Delta => PreviewValue - ConfirmedValue;
}

/// <summary>Dry-run payload Preview tính giá thành (2C-03).</summary>
public sealed class WipDryRunPayload
{
    public string WipMethod { get; set; } = ProductionCostWipMethod.PercentCompleted;
    public IncompleteCostConfig Config { get; set; } = new();
    public IReadOnlyList<WipCalcInput> Products { get; set; } = Array.Empty<WipCalcInput>();
    /// <summary>Nguồn chi phí (đã loại XFER) — tùy chọn; nếu trống dùng Amount* trên Products.</summary>
    public IReadOnlyList<CostSourceLine> Sources { get; set; } = Array.Empty<CostSourceLine>();
    /// <summary>Phiếu nhập TP để chuẩn bị cập nhật giá theo INPUT_ID (không apply).</summary>
    public IReadOnlyList<ReceiptPricePrepInput> Receipts { get; set; } = Array.Empty<ReceiptPricePrepInput>();
    /// <summary>Kết quả CONFIRMED phiên trước — để so sánh; không bị ghi đè.</summary>
    public IReadOnlyList<WipCalcResult>? ConfirmedBaseline { get; set; }
}

public sealed class ProductionCostWipWriteResponse
{
    public bool WriteEnabled { get; set; }
    public bool Persisted { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public string WipMethod { get; set; } = string.Empty;
    public string IncompleteMode { get; set; } = string.Empty;
    public IReadOnlyList<WipCalcResult> Items { get; set; } = Array.Empty<WipCalcResult>();
    public IReadOnlyList<CostCompareLine> Compare { get; set; } = Array.Empty<CostCompareLine>();
    public IReadOnlyList<ReceiptPricePrepLine> ReceiptPreps { get; set; } = Array.Empty<ReceiptPricePrepLine>();
    public decimal TotalCostPriceSum { get; set; }
    /// <summary>B4 luôn false — apply giá kho chỉ ở B5.</summary>
    public bool InventoryPriceApplied { get; set; }
    /// <summary>B4 luôn false — post 632 chỉ ở B5.</summary>
    public bool Cogs632Posted { get; set; }
    public string? IdempotencyAction { get; set; }
    public string? Message { get; set; }
}

namespace API_AMNOTE_WEB.Models;

/// <summary>
/// DTO / hằng số G-K Giá thành giản đơn (All-in-One) — giai đoạn 2C-01 đọc.
/// PascalCase theo quy ước PeriodLock API.
/// </summary>

/// <summary>Mã bước G-K lưu trong <c>production_cost_step.STEP_CODE</c> (DDL 2A).</summary>
public static class ProductionCostStepCode
{
    public const string FaPrepaidLock = "FA_PREPAID_LOCK";
    public const string CostAllocation = "COST_ALLOCATION";
    /// <summary>Bước 3 G-K — STEP_CODE cố định; khác BiStepCode COGS_SUMMARY và CogsRule.</summary>
    public const string ExpTo154Only = "EXP_TO_154_ONLY";
    public const string CostCalc = "COST_CALC";
    public const string Valuation = "VALUATION";
    public const string ProfitLossReport = "PROFIT_LOSS_REPORT";
}

/// <summary>Mã bước khóa sổ B-I trong <c>acc_period_lock_step.STEP_CODE</c> / <c>BI_STEP_CODE</c>.</summary>
public static class ProductionCostBiStepCode
{
    public const string FaPrepaidLock = "FA_PREPAID_LOCK";
    /// <summary>Bước 3 — PeriodLock COGS_SUMMARY (không dùng làm STEP_CODE G-K).</summary>
    public const string CogsSummary = "COGS_SUMMARY";
    public const string ProfitLossReport = "PROFIT_LOSS_REPORT";
}

/// <summary>Rule kết chuyển trên <c>production_cost_period.COGS_RULE</c>.</summary>
public static class ProductionCostCogsRule
{
    public const string ExpTo154Only = "EXP_TO_154_ONLY";
}

public static class ProductionCostStepStatus
{
    public const string NotStarted = "NOT_STARTED";
    public const string Processing = "PROCESSING";
    public const string Done = "DONE";
    public const string Error = "ERROR";
    public const string Reopened = "REOPENED";
    public const string Previewed = "PREVIEWED";
}

public static class ProductionCostEngine
{
    public const string None = "NONE";
    public const string Gk = "GK";
    public const string Bi = "BI";
}

/// <summary>Trạng thái triển khai schema production_cost_* trên DB công ty.</summary>
public static class ProductionCostSchemaStatus
{
    /// <summary>Chưa có bảng production_cost_* (migration 2B chưa chạy).</summary>
    public const string Missing = "MISSING";
    /// <summary>Có một phần bảng nhưng thiếu bảng bắt buộc cho API đọc.</summary>
    public const string Incomplete = "INCOMPLETE";
    /// <summary>Đủ bảng đọc 2C-01.</summary>
    public const string Ready = "READY";
}

/// <summary>Query chung: PERIOD_YM bắt buộc; RUN_ID / RUN_VERSION tùy chọn (mặc định CURRENT_RUN).</summary>
public sealed class ProductionCostQuery
{
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
}

/// <summary>Kết quả probe schema (không ghi DB).</summary>
public sealed class ProductionCostSchemaProbe
{
    public string Status { get; set; } = ProductionCostSchemaStatus.Missing;
    public IReadOnlyList<string> MissingTables { get; set; } = Array.Empty<string>();
    public bool IsReady => string.Equals(Status, ProductionCostSchemaStatus.Ready, StringComparison.OrdinalIgnoreCase);
    public bool IsMissing => string.Equals(Status, ProductionCostSchemaStatus.Missing, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Kết quả resolve RUN_VERSION cho đọc detail.</summary>
public sealed class ProductionCostRunVersionInfo
{
    public int? RunVersion { get; set; }
    public bool VersionExists { get; set; }
    public bool HasDetailData { get; set; }
    public string? Message { get; set; }
}

public sealed class ProductionCostPeriodDto
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public IReadOnlyList<string>? MissingTables { get; set; }
    public bool PeriodExists { get; set; }
    public long? PeriodCostId { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
    public string PeriodYm { get; set; } = string.Empty;
    public string FromYmd { get; set; } = string.Empty;
    public string ToYmd { get; set; } = string.Empty;
    public long? CurrentRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CostEngine { get; set; } = ProductionCostEngine.None;
    public string? CogsRule { get; set; }
    public ProductionCostRunDto? CurrentRun { get; set; }
    public string? Message { get; set; }
}

public sealed class ProductionCostRunDto
{
    public long RunId { get; set; }
    public long PeriodCostId { get; set; }
    public string CompanyCd { get; set; } = string.Empty;
    public string PeriodYm { get; set; } = string.Empty;
    public int RunVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public string WipMethod { get; set; } = string.Empty;
    public int AllocateType { get; set; }
    public string Is622627ByDepartment { get; set; } = "0";
    public string Is6212 { get; set; } = "0";
    public int AllocNumberDigit { get; set; }
    public string IncompleteMode { get; set; } = string.Empty;
    public decimal IncompletePercent { get; set; }
    public string EnableProductReduce { get; set; } = "0";
    public string? ConfirmedBy { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ReopenReason { get; set; }
    public DateTime? ReopenedAt { get; set; }
}

public sealed class ProductionCostStepsResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public IReadOnlyList<string>? MissingTables { get; set; }
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public bool? RunVersionExists { get; set; }
    public string? CogsRule { get; set; }
    public IReadOnlyList<ProductionCostStepDto> Steps { get; set; } = Array.Empty<ProductionCostStepDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostStepDto
{
    public int StepIndex { get; set; }
    /// <summary>STEP_CODE G-K (DDL) — bước 3 luôn <c>EXP_TO_154_ONLY</c>.</summary>
    public string StepCode { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>Mã PeriodLock khi bước mirror B-I — bước 3 = <c>COGS_SUMMARY</c>.</summary>
    public string? BiStepCode { get; set; }
    public string? BiStatus { get; set; }
    /// <summary>true khi STATUS G-K và B-I lệch; không dùng DONE G-K khi B-I chưa DONE.</summary>
    public bool StatusMismatch { get; set; }
    public string? ResultSummary { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Kết quả lần xác nhận trước khi REOPENED (nếu có trong history).</summary>
    public string? PreviousConfirmedSummary { get; set; }
}

public sealed class ProductionCostObjectsResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public IReadOnlyList<ProductionCostObjectDto> Items { get; set; } = Array.Empty<ProductionCostObjectDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostObjectDto
{
    public long ObjectSelId { get; set; }
    public long RunId { get; set; }
    public long DepartmentId { get; set; }
    public long ProductId { get; set; }
    public string? DepartmentCd { get; set; }
    public string? ProductCd { get; set; }
    public string IsSelected { get; set; } = "1";
    public string? WarningText { get; set; }
}

public sealed class ProductionCostAllocPoolResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public bool? RunVersionExists { get; set; }
    public bool? HasDetailData { get; set; }
    public int? AllocateType { get; set; }
    public IReadOnlyList<ProductionCostAllocPoolDto> Items { get; set; } = Array.Empty<ProductionCostAllocPoolDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostAllocPoolDto
{
    public long PoolId { get; set; }
    public long RunId { get; set; }
    public int RunVersion { get; set; }
    public string AccountCd { get; set; } = string.Empty;
    public string? CreditSample { get; set; }
    public long DepartmentId { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal FcAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal DebitAmount154 { get; set; }
    public decimal Reduction { get; set; }
    public decimal AllocationCosts { get; set; }
}

public sealed class ProductionCostAllocBasisResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public bool? RunVersionExists { get; set; }
    public bool? HasDetailData { get; set; }
    public int? AllocateType { get; set; }
    public IReadOnlyList<ProductionCostNvlBasisDto> NvlBasis { get; set; } = Array.Empty<ProductionCostNvlBasisDto>();
    public IReadOnlyList<ProductionCostNvlBasisLineDto> NvlBasisLines { get; set; } = Array.Empty<ProductionCostNvlBasisLineDto>();
    public IReadOnlyList<ProductionCostNvlOrphanDto> Orphans { get; set; } = Array.Empty<ProductionCostNvlOrphanDto>();
    /// <summary>Tiêu thức TPNK (allocate_type=1): QUANTITY theo PRODUCT từ alloc_detail (denorm).</summary>
    public IReadOnlyList<ProductionCostTpBasisDto> TpBasis { get; set; } = Array.Empty<ProductionCostTpBasisDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostNvlBasisDto
{
    public long NvlBasisId { get; set; }
    public long RunId { get; set; }
    public int RunVersion { get; set; }
    public long ProductId { get; set; }
    public decimal AmountQuantity { get; set; }
    public int LineCount { get; set; }
}

public sealed class ProductionCostNvlBasisLineDto
{
    public long NvlLineId { get; set; }
    public long RunId { get; set; }
    public int RunVersion { get; set; }
    public long ProductId { get; set; }
    public long InventoryId { get; set; }
    public long FinishedLineId { get; set; }
    public long? BomMaterialId { get; set; }
    public long OutputId { get; set; }
    public long? ChitdetailId { get; set; }
    public long? MaterialProductId { get; set; }
    public decimal ConfirmedQuantity { get; set; }
    public decimal SourceAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal SplitRatio { get; set; }
}

public sealed class ProductionCostNvlOrphanDto
{
    public long OrphanId { get; set; }
    public long? OutputId { get; set; }
    public long? ChitdetailId { get; set; }
    public decimal Amount { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? WarningText { get; set; }
}

public sealed class ProductionCostTpBasisDto
{
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string? ProductCd { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class ProductionCostAllocDetailResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public bool? RunVersionExists { get; set; }
    public bool? HasDetailData { get; set; }
    public int? AllocateType { get; set; }
    public IReadOnlyList<ProductionCostAllocDetailDto> Items { get; set; } = Array.Empty<ProductionCostAllocDetailDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostAllocDetailDto
{
    public long AllocDetailId { get; set; }
    public long RunId { get; set; }
    public int RunVersion { get; set; }
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    /// <summary>DEPARTMENT_ID của dòng pool nguồn; 0 = dùng chung. Khớp UNIQUE DDL 2C-02.</summary>
    public long PoolDepartmentId { get; set; }
    public string? ProductCd { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal AmountQuantity { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount6211 { get; set; }
    public decimal Amount6212 { get; set; }
    public string AccountCd { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public string IsLastRow { get; set; } = "0";
    public decimal Amount622Sum { get; set; }
    public decimal Amount627Sum { get; set; }
}

public sealed class ProductionCostWipResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public bool? RunVersionExists { get; set; }
    public bool? HasDetailData { get; set; }
    public string? WipMethod { get; set; }
    public string? RunStatus { get; set; }
    public IReadOnlyList<ProductionCostWipDto> Items { get; set; } = Array.Empty<ProductionCostWipDto>();
    public IReadOnlyList<ProductionCostSourceDto> Sources { get; set; } = Array.Empty<ProductionCostSourceDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostWipDto
{
    public long WipId { get; set; }
    public long RunId { get; set; }
    public int RunVersion { get; set; }
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string? ProductCd { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal IncompleteQtyOpening { get; set; }
    public decimal BeforeAmountResult621 { get; set; }
    public decimal BeforeAmountResult62N { get; set; }
    public decimal BeforeAmount154 { get; set; }
    public decimal Amount621 { get; set; }
    public decimal Amount6212 { get; set; }
    public decimal Amount622 { get; set; }
    public decimal Amount627 { get; set; }
    public decimal Amount154Direct { get; set; }
    public decimal TotalAmountIncurred { get; set; }
    public decimal Quantity { get; set; }
    public decimal PercentCompleted { get; set; }
    public decimal ClosingIncompleteQuantity { get; set; }
    public decimal AmountResult621 { get; set; }
    public decimal AmountResult62N { get; set; }
    public decimal ClosingIncompleteAmount154 { get; set; }
    public decimal ProductReduce { get; set; }
    public decimal TotalCostPrice { get; set; }
    public decimal UnitCost { get; set; }
    public decimal QHt { get; set; }
}

public sealed class ProductionCostSourceDto
{
    public long SourceId { get; set; }
    public long ProductId { get; set; }
    public long DepartmentId { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public sealed class ProductionCostReceiptsResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public bool? RunVersionExists { get; set; }
    public bool? HasDetailData { get; set; }
    public IReadOnlyList<ProductionCostReceiptDto> Items { get; set; } = Array.Empty<ProductionCostReceiptDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostReceiptDto
{
    public long ReceiptCostId { get; set; }
    public long RunId { get; set; }
    public int RunVersion { get; set; }
    public long ProductId { get; set; }
    /// <summary>Khớp WIP PRODUCT+DEPT; 0 = chứng từ thiếu DEPT hợp lệ (không tự suy đoán).</summary>
    public long DepartmentId { get; set; }
    public long InputId { get; set; }
    public long? InventoryId { get; set; }
    public string? ProductCd { get; set; }
    public string? InventoryNo { get; set; }
    public decimal Qty { get; set; }
    public decimal OldUnitPrice { get; set; }
    public decimal NewUnitPrice { get; set; }
    public decimal OldAmount { get; set; }
    public decimal NewAmount { get; set; }
    public decimal DeltaAmount { get; set; }
    public string IsApplied { get; set; } = "0";
}

public sealed class ProductionCostHistoryResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public IReadOnlyList<ProductionCostHistoryDto> Items { get; set; } = Array.Empty<ProductionCostHistoryDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostHistoryDto
{
    public long HistoryId { get; set; }
    public long RunId { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    /// <summary>STEP_CODE G-K — có sau DDL 2C-06A.</summary>
    public string? StepCode { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    /// <summary>Idempotency — có sau DDL 2C-06A.</summary>
    public string? OperationId { get; set; }
    /// <summary>PC_HIST:… — có sau DDL 2C-06A.</summary>
    public string? StableKey { get; set; }
    public string? StatusAfter { get; set; }
    public int? RunVersion { get; set; }
    public string? ResultBefore { get; set; }
    public string? ResultAfter { get; set; }
    public string? DifferenceText { get; set; }
    public string? Reason { get; set; }
    public string? UserName { get; set; }
    public DateTime? ActionAt { get; set; }
}

/// <summary>Probe cột history bắt buộc trước khi ghi (2C-06A).</summary>
public sealed class ProductionCostHistoryWriteSchemaProbe
{
    public bool Ready { get; set; }
    public IReadOnlyList<string> MissingColumns { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingIndexes { get; set; } = Array.Empty<string>();
    public string? Message { get; set; }
}

/// <summary>Payload INSERT history trong TX do C# sở hữu — không START/COMMIT trong SP.</summary>
public sealed class ProductionCostHistoryInsert
{
    public string CompanyCd { get; set; } = string.Empty;
    public string PeriodYm { get; set; } = string.Empty;
    public long RunId { get; set; }
    public int? RunVersion { get; set; }
    public int StepIndex { get; set; }
    public string? StepCode { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public string StableKey { get; set; } = string.Empty;
    /// <summary>Tuỳ chọn — lưu trong DIFFERENCE_TEXT prefix hoặc cột mở rộng sau; dùng cho Conflict nội dung.</summary>
    public string? ContentFingerprint { get; set; }
    public string? StatusAfter { get; set; }
    public string? ResultBefore { get; set; }
    public string? ResultAfter { get; set; }
    public string? DifferenceText { get; set; }
    public string? Reason { get; set; }
    public string? UserName { get; set; }
    public DateTime? ActionAt { get; set; }
}

public sealed class ProductionCostHistoryWriteResult
{
    public bool Inserted { get; set; }
    public string IdempotencyAction { get; set; } = string.Empty;
    public long? HistoryId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public string StableKey { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public sealed class ProductionCostComparisonResponse
{
    public bool SchemaAvailable { get; set; }
    public string SchemaStatus { get; set; } = ProductionCostSchemaStatus.Missing;
    public IReadOnlyList<string>? MissingTables { get; set; }
    public bool PeriodExists { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? BaseRunId { get; set; }
    public int? BaseRunVersion { get; set; }
    public bool? BaseRunVersionExists { get; set; }
    public long? CompareRunId { get; set; }
    public int? CompareRunVersion { get; set; }
    public bool? CompareRunVersionExists { get; set; }
    public IReadOnlyList<ProductionCostComparisonLineDto> Lines { get; set; } = Array.Empty<ProductionCostComparisonLineDto>();
    public string? Message { get; set; }
}

public sealed class ProductionCostComparisonLineDto
{
    public long ProductId { get; set; }
    public string? ProductCd { get; set; }
    public long DepartmentId { get; set; }
    public decimal BaseTotalCostPrice { get; set; }
    public decimal CompareTotalCostPrice { get; set; }
    public decimal DeltaTotalCostPrice { get; set; }
    public decimal BaseUnitCost { get; set; }
    public decimal CompareUnitCost { get; set; }
    public decimal DeltaUnitCost { get; set; }
    public decimal BaseAmount622 { get; set; }
    public decimal CompareAmount622 { get; set; }
    public decimal DeltaAmount622 { get; set; }
    public decimal BaseAmount627 { get; set; }
    public decimal CompareAmount627 { get; set; }
    public decimal DeltaAmount627 { get; set; }
}

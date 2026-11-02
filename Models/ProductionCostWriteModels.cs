namespace API_AMNOTE_WEB.Models;

/// <summary>Request chung cho POST G-K 2C-02 / 2C-03.</summary>
public sealed class ProductionCostWriteRequest
{
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    /// <summary>Tùy chọn — client gửi để dry-run phân bổ (2C-02).</summary>
    public AllocationDryRunPayload? DryRun { get; set; }
    /// <summary>Tùy chọn — dry-run tính dở dang / giá thành (2C-03).</summary>
    public WipDryRunPayload? WipDryRun { get; set; }
    /// <summary>Tùy chọn — dry-run VALUATION Bước 5 (2C-04).</summary>
    public ValuationDryRunPayload? ValuationDryRun { get; set; }
}

public sealed class AllocationDryRunPayload
{
    public int AllocateType { get; set; }
    public bool ByDepartment { get; set; }
    public int NumberDigit { get; set; }
    public bool Include6212 { get; set; }
    public IReadOnlyList<AllocationPoolInput> Pool { get; set; } = Array.Empty<AllocationPoolInput>();
    public IReadOnlyList<AllocationProductInput> Products { get; set; } = Array.Empty<AllocationProductInput>();
}

public sealed class ProductionCostAllocationWriteResponse
{
    public bool WriteEnabled { get; set; }
    public bool Persisted { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public int AllocateType { get; set; }
    public bool ByDepartment { get; set; }
    public IReadOnlyList<AllocationPoolInput> Pool { get; set; } = Array.Empty<AllocationPoolInput>();
    public IReadOnlyList<AllocationLineResult> Details { get; set; } = Array.Empty<AllocationLineResult>();
    public decimal Total622 { get; set; }
    public decimal Total627 { get; set; }
    public string? Message { get; set; }
    public string? IdempotencyAction { get; set; }
}

public sealed class ProductionCostTransferWriteResponse
{
    public bool WriteEnabled { get; set; }
    public bool Persisted { get; set; }
    public string PeriodYm { get; set; } = string.Empty;
    public long? RunId { get; set; }
    public int? RunVersion { get; set; }
    public string StepCode { get; set; } = ProductionCostStepCode.ExpTo154Only;
    public string BiStepCode { get; set; } = ProductionCostBiStepCode.CogsSummary;
    public string CogsRule { get; set; } = ProductionCostCogsRule.ExpTo154Only;
    public string CostEngine { get; set; } = ProductionCostEngine.Gk;
    public bool Downstream154To155Or632 { get; set; }
    public string? StableKey { get; set; }
    public string? IdempotencyAction { get; set; }
    public string? Message { get; set; }
}

namespace API_AMNOTE_WEB.Models;

public sealed class AllocationPoolInput
{
    public string AccountCd { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal DebitAmount154 { get; set; }
    public decimal Reduction { get; set; }
    public decimal AllocationCosts { get; set; }
}

public sealed class AllocationProductInput
{
    public long ProductId { get; set; }
    public string? ProductCd { get; set; }
    public long DepartmentId { get; set; }
    public string? DepartmentCd { get; set; }
    /// <summary>Tiêu thức type=0: Σ N621/C152.</summary>
    public decimal AmountQuantity { get; set; }
    /// <summary>Tiêu thức type=1: Σ SL N155/C154.</summary>
    public decimal Quantity { get; set; }
    public decimal Amount6211 { get; set; }
    public decimal Amount6212 { get; set; }
}

public sealed class AllocationEngineOptions
{
    /// <summary>0 = NVL N621/C152; 1 = SL TP N155/C154.</summary>
    public int AllocateType { get; set; }
    public bool ByDepartment { get; set; }
    public int NumberDigit { get; set; }
    public string? CompanyCd { get; set; }
}

public sealed class AllocationLineResult
{
    public long ProductId { get; set; }
    public string? ProductCd { get; set; }
    public long DepartmentId { get; set; }
    public string? DepartmentCd { get; set; }
    public decimal AmountQuantity { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount6211 { get; set; }
    public decimal Amount6212 { get; set; }
    public string AccountCd { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public bool IsLastRow { get; set; }
    public decimal Amount622Sum { get; set; }
    public decimal Amount627Sum { get; set; }
}

public sealed class AllocationComputeResult
{
    public IReadOnlyList<AllocationPoolInput> Pool { get; set; } = Array.Empty<AllocationPoolInput>();
    public IReadOnlyList<AllocationLineResult> Details { get; set; } = Array.Empty<AllocationLineResult>();
    public decimal Total622 { get; set; }
    public decimal Total627 { get; set; }
}

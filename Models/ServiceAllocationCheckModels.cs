namespace API_AMNOTE_WEB.Models;

public sealed class ServiceAllocationCheckRow
{
    public long SERVICES_CHIT_ID { get; set; }
    public string SERVICES_CHIT_CD { get; set; } = string.Empty;
    public string SERVICES_CHIT_NO { get; set; } = string.Empty;
    public long? CUSTOMER_ID { get; set; }
    public string? CUSTOMER_CD { get; set; }
    public DateTime? CHIT_YMD { get; set; }
    public string? VAT_CHIT_NO { get; set; }
    public DateTime? VAT_YMD { get; set; }
    public string? CONTENTS { get; set; }
    public decimal TOTAL_COST { get; set; }
    public decimal COST_NOT_YET_ALLOCATED { get; set; }
    public decimal COST_NOW_ALLOCATES { get; set; }
    public decimal COST_REMAIN { get; set; }

    public List<ServiceAllocationCheckDetailRow> ALLOCATIONS { get; set; } = new();
}

public sealed class ServiceAllocationCheckDetailRow
{
    public long SERVICES_CHIT_ID { get; set; }
    public string? SERVICES_CHIT_CD { get; set; }
    public string? CHIT_TYPE { get; set; }
    public long INVENTORY_CHIT_ID { get; set; }
    public string? INVENTORY_CHIT_CD { get; set; }
    public string? CHIT_NO { get; set; }
    public DateTime? CHIT_YMD { get; set; }
    public decimal AMOUNT_SHARE { get; set; }
}

public sealed class ServiceAllocationCheckResponse
{
    public IReadOnlyList<ServiceAllocationCheckRow> ITEMS { get; set; } = Array.Empty<ServiceAllocationCheckRow>();
    public int TOTAL_RECORDS { get; set; }
}

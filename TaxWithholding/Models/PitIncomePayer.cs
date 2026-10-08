namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitIncomePayer
{
    public string COMPANY_CD { get; set; } = "";
    public string PAYER_NM { get; set; } = "";
    public string TAX_CD { get; set; } = "";
    public string ADDRESS { get; set; } = "";
    public string? PHONE { get; set; }
    public string? EMAIL { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public string? CREATE_BY { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public DateTime? CREATE_AT { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public string? UPDATE_BY { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public DateTime? UPDATE_AT { get; set; }
}

public sealed class PitIncomePayerSaveRequest
{
    public string? PAYER_NM { get; set; }
    public string? TAX_CD { get; set; }
    public string? ADDRESS { get; set; }
    public string? PHONE { get; set; }
    public string? EMAIL { get; set; }
}

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitXslTemplate
{
    public long XSL_ID { get; set; }
    public string COMPANY_CD { get; set; } = "";
    public string TEMPLATE_CD { get; set; } = PitXslCodes.Certificate;
    public string TEMPLATE_NM { get; set; } = "";
    public string? SERIES { get; set; }
    public long FROM_DOC_NO { get; set; } = 1;
    public long? TO_DOC_NO { get; set; }
    public int IS_DEFAULT { get; set; }
    public int IS_ACTIVE { get; set; }
    public string? XSL_CONTENT { get; set; }
    public string? LOGO_PATH { get; set; }
    public string? BACKGROUND_PATH { get; set; }
    public string? NEN_PATH { get; set; }
    public int HAS_XSL_CONTENT { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public string? CREATE_BY { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public DateTime? CREATE_AT { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public string? UPDATE_BY { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public DateTime? UPDATE_AT { get; set; }
}

public sealed class PitXslTemplateSaveRequest
{
    public string? TEMPLATE_CD { get; set; }
    public string? TEMPLATE_NM { get; set; }
    public string? SERIES { get; set; }
    public long? FROM_DOC_NO { get; set; }
    public long? TO_DOC_NO { get; set; }
    public int? IS_DEFAULT { get; set; }
    public int? IS_ACTIVE { get; set; }
    public string? XSL_CONTENT { get; set; }
    public string? LOGO_PATH { get; set; }
    public string? BACKGROUND_PATH { get; set; }
    public string? NEN_PATH { get; set; }
}

public sealed class PitXslImageResult
{
    public string PATH { get; set; } = "";
    public string FILE_NAME { get; set; } = "";
    public string IMAGE_KIND { get; set; } = "";
    public long XSL_ID { get; set; }
}

public static class PitXslCodes
{
    public const string Certificate = "03/TNCN";
}

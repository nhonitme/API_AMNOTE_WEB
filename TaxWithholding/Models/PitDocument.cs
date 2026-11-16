namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitKinds
{
    public const string Declaration = "declaration", Certificate = "certificate", ErrorNotice = "error-notice";
    public static string Message(string kind) => kind switch { Declaration => "109", Certificate => "211", ErrorNotice => "304", _ => throw new ArgumentException("Loại chứng từ không hợp lệ.") };
    public static string Target(string kind) => kind switch { Declaration => "PIT_DECLARATION", Certificate => "PIT_CERTIFICATE", ErrorNotice => "PIT_ERROR_NOTICE", _ => throw new ArgumentException("Loại chứng từ không hợp lệ.") };
    public static string KindFromTarget(string target) => target switch { "PIT_DECLARATION" => Declaration, "PIT_CERTIFICATE" => Certificate, "PIT_ERROR_NOTICE" => ErrorNotice, _ => throw new ArgumentException("Loại đích chứng từ không hợp lệ.") };
    public static bool IsTarget(string? target) => target is "PIT_DECLARATION" or "PIT_CERTIFICATE" or "PIT_ERROR_NOTICE";
}
public sealed class PitOutboxItem
{
    public long TARGET_ID { get; set; }
    public string TARGET_TYPE { get; set; } = "";
}
public sealed class PitCertificateRegistration
{
    public string TTCHUC { get; set; } = "";
    public string SERI { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired]
    public DateTimeOffset TNGAY { get; set; }
    [System.Text.Json.Serialization.JsonRequired]
    public DateTimeOffset DNGAY { get; set; }
    public int HTHUC { get; set; } = 1;
}
public sealed class PitNoticeItem
{
    public long? REF_CTU_ID { get; set; }
    public string KHMSCTU { get; set; } = "03/TNCN";
    public string KHCTU { get; set; } = "";
    public string SCTU { get; set; } = "";
    [System.Text.Json.Serialization.JsonRequired]
    [System.Text.Json.Serialization.JsonConverter(typeof(PitDateJsonConverter))]
    public DateTime NLAP { get; set; }
    public string LCTDT { get; set; } = "8";
    public string LDO { get; set; } = "";
}
[System.Text.Json.Serialization.JsonDerivedType(typeof(PitCertificateDto))]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PitDeclarationDto))]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PitErrorNoticeDto))]
public abstract class PitDocument
{
    public long DOCUMENT_ID { get; set; }
    public string KIND { get; set; } = "";
    public int DOC_VERSION { get; set; }
    [System.Text.Json.Serialization.JsonConverter(typeof(PitDateJsonConverter))]
    public DateTime? DOC_DATE { get; set; }
    public string TAX_CD { get; set; } = "";
    public string DISPLAY_NAME { get; set; } = "";
    public string? SERIES { get; set; }
    public long? XSL_ID { get; set; }
    public long? DOC_NO { get; set; }
    public int IS_SIGNED { get; set; }
    public int CQT_STATUS { get; set; }
    public string? ERROR_MESSAGE { get; set; }
    public string? MTDIEP { get; set; }
    public string? MGDDTU { get; set; }
    public string? RAW_XML { get; set; }
    public string? SIGNED_XML { get; set; }
    public string? RESPONSE_XML { get; set; }
    public int QUEUED { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public string? UPDATE_BY { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public DateTime? UPDATE_AT { get; set; }
}
public abstract class PitStorageRow
{
    public string COMPANY_CD { get; set; } = "";
    public int DOC_VERSION { get; set; }
    public string? RAW_XML { get; set; }
    public string? SIGNED_XML { get; set; }
    public string? RESPONSE_XML { get; set; }
    public string? MTDIEP { get; set; }
    public string? MGDDTU { get; set; }
    public int IS_SIGNED { get; set; }
    public int CQT_STATUS { get; set; }
    public string? ERROR_MESSAGE { get; set; }
    public int QUEUED { get; set; }
    public int RESPONSE_PRIORITY { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public string? UPDATE_BY { get; set; }
    [API_AMNOTE_WEB.Helpers.BackendAudit]
    public DateTime? UPDATE_AT { get; set; }
}
public sealed class PitSignRequest { public int DOC_VERSION { get; set; } public string XML { get; set; } = ""; }
public sealed record PitSigningPayload(int DOC_VERSION, string RAW_XML, string SIGN_TYPE);

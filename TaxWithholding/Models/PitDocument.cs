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
public sealed class PitData
{
    public Dictionary<string, string> Fields { get; set; } = new();
    public List<PitCertificateRegistration> Certificates { get; set; } = new();
    public List<PitNoticeItem> Items { get; set; } = new();
    public string Get(string key) => Fields.GetValueOrDefault(key, "").Trim();
}
public sealed class PitCertificateRegistration
{
    public string TTCHUC { get; set; } = "";
    public string SERI { get; set; } = "";
    public string TNGAY { get; set; } = "";
    public string DNGAY { get; set; } = "";
    public int HTHUC { get; set; } = 1;
}
public sealed class PitNoticeItem
{
    public long? REF_CTU_ID { get; set; }
    public string KHMSCTU { get; set; } = "03/TNCN";
    public string KHCTU { get; set; } = "";
    public string SCTU { get; set; } = "";
    public string NLAP { get; set; } = "";
    public string LCTDT { get; set; } = "8";
    public string LDO { get; set; } = "";
}
public sealed class PitDocument
{
    public long DOCUMENT_ID { get; set; }
    public string KIND { get; set; } = "";
    public int DOC_VERSION { get; set; }
    public DateTime? DOC_DATE { get; set; }
    public string TAX_CD { get; set; } = "";
    public string DISPLAY_NAME { get; set; } = "";
    public string? SERIES { get; set; }
    public long? XSL_ID { get; set; }
    public long? DOC_NO { get; set; }
    public PitData DATA { get; set; } = new();
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
public sealed class PitDeclaration : PitStorageRow
{
    public long TKHAI_ID { get; set; }
    public int HTHUC { get; set; }
    public string TNNT { get; set; } = "";
    public string MST { get; set; } = "";
    public string CQTQLY { get; set; } = "";
    public string MCQTQLY { get; set; } = "";
    public string NLHE { get; set; } = "";
    public string DCLHE { get; set; } = "";
    public string DCTDTU { get; set; } = "";
    public string DTLHE { get; set; } = "";
    public string DDANH { get; set; } = "";
    public DateTime NLAP { get; set; }
}
public sealed class PitDeclarationCertificate
{
    public int STT { get; set; }
    public string TTCHUC { get; set; } = "";
    public string SERI { get; set; } = "";
    public DateTime TNGAY { get; set; }
    public DateTime DNGAY { get; set; }
    public int HTHUC { get; set; }
}
public sealed class PitCertificate : PitStorageRow
{
    public long CTU_ID { get; set; }
    public string KHCTU { get; set; } = "";
    public long? SCTU { get; set; }
    public DateTime? NLAP { get; set; }
    public int? TCCTU { get; set; }
    public int? LHCTLQUAN { get; set; }
    public string? KHMSCTCLQUAN { get; set; }
    public string? KHCTCLQUAN { get; set; }
    public string? SCTCLQUAN { get; set; }
    public DateTime? NLCTCLQUAN { get; set; }
    public string? GCHU { get; set; }
    public string TCTTNHAP_TEN { get; set; } = "";
    public string TCTTNHAP_MST { get; set; } = "";
    public string TCTTNHAP_DCHI { get; set; } = "";
    public string? TCTTNHAP_SDTHOAI { get; set; }
    public string NNT_TEN { get; set; } = "";
    public string? NNT_MST { get; set; }
    public string NNT_DCHI { get; set; } = "";
    public string? NNT_QTICH { get; set; }
    public int NNT_CNCTRU { get; set; }
    public string? NNT_CCCDAN { get; set; }
    public string NNT_SDTHOAI { get; set; } = "";
    public string? NNT_DCTDTU { get; set; }
    public string? NNT_GCHU { get; set; }
    public string KTNHAP { get; set; } = "";
    public int TTHANG { get; set; }
    public int DTHANG { get; set; }
    public int NAM { get; set; }
    public decimal BHIEM { get; set; }
    public decimal TTHIEN { get; set; }
    public decimal TTNCTHUE { get; set; }
    public decimal TTNTTHUE { get; set; }
    public decimal STHUE { get; set; }
    public long? XSL_ID { get; set; }
}
public sealed class PitErrorNotice : PitStorageRow
{
    public long TBAO_ID { get; set; }
    public int LOAI { get; set; }
    public string? SO { get; set; }
    public DateTime? NTBCCQT { get; set; }
    public string MCQT { get; set; } = "";
    public string TCQT { get; set; } = "";
    public string TNNT { get; set; } = "";
    public string? MST { get; set; }
    public string? MDVQHNSACH { get; set; }
    public string DDANH { get; set; } = "";
    public DateTime NTBAO { get; set; }
}
public sealed class PitErrorNoticeCertificate
{
    public long? REF_CTU_ID { get; set; }
    public int STT { get; set; }
    public string KHMSCTU { get; set; } = "";
    public string KHCTU { get; set; } = "";
    public string SCTU { get; set; } = "";
    public DateTime NLAP { get; set; }
    public string LCTDT { get; set; } = "";
    public string? LDO { get; set; }
}
public sealed class PitSaveRequest { public int DOC_VERSION { get; set; } public long? XSL_ID { get; set; } public PitData DATA { get; set; } = new(); }
public sealed class PitSignRequest { public int DOC_VERSION { get; set; } public string XML { get; set; } = ""; }
public sealed record PitSigningPayload(int DOC_VERSION, string RAW_XML, string SIGN_TYPE);
public sealed record PitOption(string Value, string Label);
public sealed record PitField(string Key, string Label, string Group, string Type = "text", bool Required = true, int MaxLength = 400, string? Path = null, PitOption[]? Options = null, string? Default = null);
public sealed record PitSchema(string Title, string Form, string Root, string DataRoot, string Signer, PitField[] Fields);

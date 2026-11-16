namespace API_AMNOTE_WEB.TaxWithholding;

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

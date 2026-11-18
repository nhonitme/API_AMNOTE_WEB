namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitCertificate : PitStorageRow
{
    public long CTU_ID { get; set; }
    public string? TCTU { get; set; }
    public string? MSCTU { get; set; }
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
    public int KTNHAP { get; set; }
    public int TTHANG { get; set; }
    public int DTHANG { get; set; }
    public int NAM { get; set; }
    public decimal BHIEM { get; set; }
    public decimal TTHIEN { get; set; }
    public decimal TTNCTHUE { get; set; }
    public decimal TTNTTHUE { get; set; }
    public decimal STHUE { get; set; }
    public long? XSL_ID { get; set; }
    public string? MTRACUU { get; set; }
}

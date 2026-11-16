namespace API_AMNOTE_WEB.TaxWithholding;

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

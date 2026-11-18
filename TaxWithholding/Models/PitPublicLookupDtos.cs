using System.Text.Json.Serialization;
using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitCertificatePublicLookupDto
{
    public string? MTRACUU { get; set; }
    public string? TCTU { get; set; }
    public string MSCTU { get; set; } = "03/TNCN";
    public string? KHCTU { get; set; }
    public long? SCTU { get; set; }
    [JsonConverter(typeof(NullableDateTimeJsonConverter))]
    public DateTime? NLAP { get; set; }
    public string? TCTTNHAP_TEN { get; set; }
    public string? TCTTNHAP_MST { get; set; }
    public string? TCTTNHAP_DCHI { get; set; }
    public string? NNT_TEN { get; set; }
    public string? NNT_MST { get; set; }
    public string? NNT_DCHI { get; set; }
    public int? KTNHAP { get; set; }
    public int? TTHANG { get; set; }
    public int? DTHANG { get; set; }
    public int? NAM { get; set; }
    public decimal? BHIEM { get; set; }
    public decimal? TTHIEN { get; set; }
    public decimal? TTNCTHUE { get; set; }
    public decimal? TTNTTHUE { get; set; }
    public decimal? STHUE { get; set; }
    public bool CAN_DOWNLOAD_PDF { get; set; } = true;
    public bool CAN_DOWNLOAD_XML { get; set; } = true;
}

public sealed class PitCertificatePublicDownloadInfo
{
    public long CTU_ID { get; set; }
    public string? COMPANY_CD { get; set; }
    public string? DB_NAME { get; set; }
    public string? MTRACUU { get; set; }
    public string? KHCTU { get; set; }
    public long? SCTU { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.TaxWithholding;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PitCertificateDto : PitDocument
{
    [Display(Name = "Ký hiệu chứng từ (CT26AA)")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(6, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string KHCTU { get; set; } = "";
    [JsonConverter(typeof(PitDateJsonConverter))]
    public DateTime? NLAP { get; set; }
    public int? TCCTU { get; set; }
    public int? LHCTLQUAN { get; set; }
    [Display(Name = "Mẫu số chứng từ liên quan")]
    [MaxLength(11, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string KHMSCTCLQUAN { get; set; } = "";
    [Display(Name = "Ký hiệu chứng từ liên quan")]
    [MaxLength(9, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string KHCTCLQUAN { get; set; } = "";
    [Display(Name = "Số chứng từ liên quan")]
    [MaxLength(8, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string SCTCLQUAN { get; set; } = "";
    [JsonConverter(typeof(PitDateJsonConverter))]
    public DateTime? NLCTCLQUAN { get; set; }
    [Display(Name = "Ghi chú liên quan")]
    [MaxLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string GCHU { get; set; } = "";
    [Display(Name = "Tên tổ chức, cá nhân trả thu nhập")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TCTTNHAP_TEN { get; set; } = "";
    [Display(Name = "Mã số thuế tổ chức")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(14, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TCTTNHAP_MST { get; set; } = "";
    [Display(Name = "Địa chỉ tổ chức")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TCTTNHAP_DCHI { get; set; } = "";
    [Display(Name = "Điện thoại tổ chức")]
    [MaxLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TCTTNHAP_SDTHOAI { get; set; } = "";
    [Display(Name = "Họ tên người nhận thu nhập")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NNT_TEN { get; set; } = "";
    [Display(Name = "Mã số thuế cá nhân")]
    [MaxLength(14, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NNT_MST { get; set; } = "";
    [Display(Name = "Địa chỉ cá nhân")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NNT_DCHI { get; set; } = "";
    [Display(Name = "Quốc tịch")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NNT_QTICH { get; set; } = "";
    [JsonRequired]
    public int NNT_CNCTRU { get; set; }
    [Display(Name = "CCCD / hộ chiếu / số định danh")]
    [MaxLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NNT_CCCDAN { get; set; } = "";
    [Display(Name = "Điện thoại cá nhân")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NNT_SDTHOAI { get; set; } = "";
    [Display(Name = "Email cá nhân")]
    [MaxLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    [PitEmail]
    public string NNT_DCTDTU { get; set; } = "";
    [Display(Name = "Ghi chú")]
    [MaxLength(255, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NNT_GCHU { get; set; } = "";
    [JsonRequired]
    public int KTNHAP { get; set; }
    [JsonRequired]
    public int TTHANG { get; set; }
    [JsonRequired]
    public int DTHANG { get; set; }
    [JsonRequired]
    public int NAM { get; set; }
    [JsonRequired]
    public decimal BHIEM { get; set; }
    [JsonRequired]
    public decimal TTHIEN { get; set; }
    [JsonRequired]
    public decimal TTNCTHUE { get; set; }
    [JsonRequired]
    public decimal TTNTTHUE { get; set; }
    [JsonRequired]
    public decimal STHUE { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.TaxWithholding;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PitDeclarationDto : PitDocument
{
    [JsonRequired]
    public int HTHUC { get; set; }
    [Display(Name = "Tên tổ chức, cá nhân trả thu nhập")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TNNT { get; set; } = "";
    [Display(Name = "Mã số thuế")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(14, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MST { get; set; } = "";
    [Display(Name = "Cơ quan thuế quản lý")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string CQTQLY { get; set; } = "";
    [Display(Name = "Mã cơ quan thuế")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(5, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MCQTQLY { get; set; } = "";
    [Display(Name = "Người liên hệ")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string NLHE { get; set; } = "";
    [Display(Name = "Địa chỉ liên hệ")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string DCLHE { get; set; } = "";
    [Display(Name = "Email")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    [PitEmail]
    public string DCTDTU { get; set; } = "";
    [Display(Name = "Điện thoại liên hệ")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(20, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string DTLHE { get; set; } = "";
    [Display(Name = "Địa danh")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string DDANH { get; set; } = "";
    [JsonConverter(typeof(PitDateJsonConverter))]
    [JsonRequired]
    public DateTime NLAP { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    public List<PitCertificateRegistration> Certificates { get; set; } = new();
}

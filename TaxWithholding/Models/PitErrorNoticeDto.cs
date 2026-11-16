using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.TaxWithholding;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PitErrorNoticeDto : PitDocument
{
    [JsonRequired]
    public int LOAI { get; set; }
    [Display(Name = "Số thông báo của CQT")]
    [MaxLength(30, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string SO { get; set; } = "";
    [JsonConverter(typeof(PitDateJsonConverter))]
    public DateTime? NTBCCQT { get; set; }
    [Display(Name = "Mã cơ quan thuế")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(5, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MCQT { get; set; } = "";
    [Display(Name = "Tên cơ quan thuế")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(100, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TCQT { get; set; } = "";
    [Display(Name = "Tên tổ chức, cá nhân trả thu nhập")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(400, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string TNNT { get; set; } = "";
    [Display(Name = "Mã số thuế")]
    [MaxLength(14, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MST { get; set; } = "";
    [Display(Name = "Mã đơn vị quan hệ ngân sách (nội bộ)")]
    [MaxLength(7, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string MDVQHNSACH { get; set; } = "";
    [Display(Name = "Địa danh")]
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(50, ErrorMessage = "{0} tối đa {1} ký tự.")]
    public string DDANH { get; set; } = "";
    [JsonConverter(typeof(PitDateJsonConverter))]
    [JsonRequired]
    public DateTime NTBAO { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    public List<PitNoticeItem> Items { get; set; } = new();
}

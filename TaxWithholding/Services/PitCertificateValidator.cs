using System.Globalization;
using System.Text.RegularExpressions;
namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitCertificateValidator
{
    public static void Validate(PitCertificateDto data)
    {
        PitValidation.Annotations(data);
        if (data.KTNHAP is < 1 or > 8) throw new ArgumentException("Khoản thu nhập phải là mã từ 1 đến 8 theo danh mục.");

        if (data.TCCTU is not (null or 1 or 2)) throw new ArgumentException("Tính chất không thuộc danh mục.");
        if (data.LHCTLQUAN is not (null or 1 or 7 or 8)) throw new ArgumentException("Loại chứng từ liên quan không thuộc danh mục.");












        if (data.NNT_CNCTRU is not (1 or 0)) throw new ArgumentException("Tình trạng cư trú không thuộc danh mục.");





        PitValidation.Money(data.BHIEM, "Bảo hiểm bắt buộc", data.TCCTU == 2);
        PitValidation.Money(data.TTHIEN, "Từ thiện, nhân đạo, khuyến học", data.TCCTU == 2);
        PitValidation.Money(data.TTNCTHUE, "Tổng thu nhập chịu thuế", data.TCCTU == 2);
        PitValidation.Money(data.TTNTTHUE, "Tổng thu nhập tính thuế", data.TCCTU == 2);
        PitValidation.Money(data.STHUE, "Số thuế TNCN đã khấu trừ", data.TCCTU == 2);
        var date = data.NLAP ?? DateTime.UtcNow.AddHours(7).Date;
        PitValidation.DocumentDate(date);
        if (!Regex.IsMatch(data.KHCTU, $"^CT{date:yy}[A-Z]{{2}}$")) throw new ArgumentException("Ký hiệu phải là CT + hai số năm lập + hai chữ in hoa (ví dụ CT26AA).");
        if (string.IsNullOrWhiteSpace(data.NNT_MST) && string.IsNullOrWhiteSpace(data.NNT_CCCDAN)) throw new ArgumentException("Cá nhân chưa có MST phải có CCCD, hộ chiếu hoặc số định danh.");
        if (data.TTHANG < 1 || data.DTHANG > 12 || data.TTHANG > data.DTHANG || data.NAM < 1900 || data.NAM > date.Year) throw new ArgumentException("Kỳ trả thu nhập không hợp lệ.");
        if (data.TCCTU.HasValue && (data.LHCTLQUAN == null || string.IsNullOrWhiteSpace(data.KHMSCTCLQUAN) || string.IsNullOrWhiteSpace(data.KHCTCLQUAN) || string.IsNullOrWhiteSpace(data.SCTCLQUAN) || data.NLCTCLQUAN == null)) throw new ArgumentException("Nhập đầy đủ thông tin chứng từ bị thay thế/điều chỉnh.");
    }
}

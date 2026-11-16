namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitDeclarationValidator
{
    public static void Validate(PitDeclarationDto data)
    {
        PitValidation.Annotations(data);
        if (data.HTHUC is not (1 or 2)) throw new ArgumentException("Hình thức đăng ký không thuộc danh mục.");










        PitValidation.DocumentDate(data.NLAP);
        if (data.Certificates.Count == 0 || data.Certificates.Count > 999) throw new ArgumentException("Khai báo từ 1 đến 999 chứng thư số sử dụng.");
        foreach (var certificate in data.Certificates)
            if (string.IsNullOrWhiteSpace(certificate.TTCHUC) || certificate.TTCHUC.Length > 400 || string.IsNullOrWhiteSpace(certificate.SERI) || certificate.SERI.Length > 40
                || certificate.TNGAY == default || certificate.DNGAY <= certificate.TNGAY || certificate.HTHUC is < 1 or > 3)
                throw new ArgumentException("Thông tin chứng thư số chưa hợp lệ: tổ chức cấp, serial, thời hạn và hình thức.");
    }
}

namespace API_AMNOTE_WEB.TaxWithholding;

public static class PitErrorNoticeValidator
{
    public static void Validate(PitErrorNoticeDto data)
    {
        PitValidation.Annotations(data);
        if (data.LOAI is not (1 or 2)) throw new ArgumentException("Loại thông báo không thuộc danh mục.");







        var date = data.NTBAO;
        PitValidation.DocumentDate(date);
        if (data.LOAI == 2 && (string.IsNullOrWhiteSpace(data.SO) || data.NTBCCQT == null)) throw new ArgumentException("Giải trình phải có số và ngày thông báo của CQT.");
        var hasTaxCode = !string.IsNullOrWhiteSpace(data.MST);
        var hasBudgetCode = !string.IsNullOrWhiteSpace(data.MDVQHNSACH);
        if (!hasTaxCode && !hasBudgetCode) throw new ArgumentException("Nhập mã số thuế; đơn vị sự nghiệp không có MST cần khai báo mã đơn vị quan hệ ngân sách nội bộ.");
        if (data.Items.Count == 0 || data.Items.Count > 9999) throw new ArgumentException("Chọn từ 1 đến 9999 chứng từ đã lập sai.");
        foreach (var item in data.Items)
            if (string.IsNullOrWhiteSpace(item.KHMSCTU) || item.KHMSCTU.Length > 10 || string.IsNullOrWhiteSpace(item.KHCTU) || item.KHCTU.Length > 9 || string.IsNullOrWhiteSpace(item.SCTU) || item.SCTU.Length > 8
               || item.NLAP == default || item.NLAP.Date > date.Date || !PitDocumentRules.IsNoticeDocumentType(item.LCTDT) || item.LDO?.Length > 255)
                throw new ArgumentException("Dòng chứng từ sai sót chưa hợp lệ: mẫu số, ký hiệu, số, ngày hoặc loại chứng từ.");
        if (data.Items.Select(i => $"{i.KHMSCTU}|{i.KHCTU}|{i.SCTU}|{i.NLAP}").Distinct().Count() != data.Items.Count) throw new ArgumentException("Danh sách chứng từ sai sót bị trùng.");
    }
}

using System.Net.Mail;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitIncomePayerService(PitIncomePayerRepository repository)
{
    public Task<PitIncomePayer?> GetAsync(string companyCd)
        => repository.GetAsync(companyCd);

    public Task<PitIncomePayer> SaveAsync(
        string companyCd,
        string userId,
        PitIncomePayerSaveRequest request)
    {
        request.PAYER_NM = NormalizeRequired(request.PAYER_NM, "Tên tổ chức trả thu nhập", 255);
        request.TAX_CD = NormalizeRequired(request.TAX_CD, "Mã số thuế", 14);
        request.ADDRESS = NormalizeOptional(request.ADDRESS, 500) ?? "";
        request.PHONE = NormalizeOptional(request.PHONE, 20);
        request.EMAIL = NormalizeOptional(request.EMAIL, 255);

        if (request.EMAIL != null)
        {
            try
            {
                _ = new MailAddress(request.EMAIL);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Email không hợp lệ.");
            }
        }

        return repository.SaveAsync(companyCd, userId, request);
    }

    private static string NormalizeRequired(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim() ?? "";
        if (normalized.Length == 0)
            throw new ArgumentException($"{fieldName} là bắt buộc.");
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maxLength)
            throw new ArgumentException($"Dữ liệu không được vượt quá {maxLength} ký tự.");
        return normalized;
    }
}

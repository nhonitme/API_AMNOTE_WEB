using System.ComponentModel.DataAnnotations;

namespace API_AMNOTE_WEB.TaxWithholding;

internal static class PitValidation
{
    public static void Annotations(object data)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(data, new ValidationContext(data), errors, validateAllProperties: true))
            throw new ArgumentException(string.Join(" ", errors.Select(error => error.ErrorMessage)));
    }
    public static void Money(decimal value, string label, bool adjustment)
    {
        if (decimal.Abs(value) > 999999999999999.999999m || decimal.Round(value, 6) != value || (value < 0 && !adjustment))
            throw new ArgumentException($"{label} không hợp lệ (tối đa 15 số nguyên, 6 số lẻ).");
    }
    public static void DocumentDate(DateTime date)
    {
        if (date == default) throw new ArgumentException("Vui lòng nhập ngày lập.");
        if (date.Date > DateTime.UtcNow.AddHours(7).Date) throw new ArgumentException("Ngày lập không được ở tương lai.");
    }
}

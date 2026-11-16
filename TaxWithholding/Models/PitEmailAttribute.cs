using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace API_AMNOTE_WEB.TaxWithholding;

public sealed class PitEmailAttribute : ValidationAttribute
{
    public PitEmailAttribute() : base("{0} không hợp lệ.") { }
    public override bool IsValid(object? value)
        => value is null || value is string email && (email.Length == 0 || MailAddress.TryCreate(email, out _));
}

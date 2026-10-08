using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    public static class EInvoiceBuyerEmailHelper
    {
        public static string? NormalizeBuyerEmailList(string? value, int maxLength = 500)
        {
            var parts = ParseBuyerEmails(value);
            if (parts.Count == 0)
            {
                return null;
            }

            var normalized = string.Join(";", parts);
            if (maxLength > 0 && normalized.Length > maxLength)
            {
                throw new ArgumentException($"Buyer email list must be less than or equal to {maxLength} characters");
            }

            return normalized;
        }

        public static string? GetPrimaryBuyerEmailForXml(string? value)
        {
            return ParseBuyerEmails(value).FirstOrDefault();
        }

        public static IReadOnlyList<string> ParseBuyerEmails(string? value)
        {
            var normalized = Common.NormalizeNullableText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Array.Empty<string>();
            }

            return normalized
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static void ValidateBuyerEmailList(string? value, string fieldName = "NMUA_DCTDTU")
        {
            var normalized = Common.NormalizeNullableText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            var parts = ParseBuyerEmails(normalized);
            if (parts.Count == 0)
            {
                throw new ArgumentException($"{fieldName} is invalid");
            }

            foreach (var email in parts)
            {
                if (!Common.IsValidEmail(email))
                {
                    throw new ArgumentException($"{fieldName} contains invalid email: {email}");
                }
            }
        }
    }
}

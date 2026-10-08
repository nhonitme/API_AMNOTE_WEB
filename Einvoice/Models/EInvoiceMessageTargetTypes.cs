namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceMessageTargetTypes
    {
        public const string Invoice = "INVOICE";
        public const string Declaration = "DECLARATION";
        public const string ErrorNotice = "ERROR_NOTICE";
        public const string Summary = "SUMMARY";

        public static string Normalize(string? targetType)
        {
            var normalized = (targetType ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                Declaration => Declaration,
                ErrorNotice => ErrorNotice,
                Summary => Summary,
                "PIT_DECLARATION" => "PIT_DECLARATION",
                "PIT_CERTIFICATE" => "PIT_CERTIFICATE",
                "PIT_ERROR_NOTICE" => "PIT_ERROR_NOTICE",
                _ => Invoice
            };
        }

        public static bool IsDeclaration(string? targetType)
            => string.Equals(Normalize(targetType), Declaration, StringComparison.Ordinal);

        public static bool IsErrorNotice(string? targetType)
            => string.Equals(Normalize(targetType), ErrorNotice, StringComparison.Ordinal);
    }
}

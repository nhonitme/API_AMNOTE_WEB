using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    internal static class EInvoiceValidationMessages
    {
        public static string Required(string fieldKey, string? lang = null, string? fieldFallback = null)
            => Common.GetFieldRequiredMessage(fieldKey, lang, fieldFallback);

        public static ArgumentException RequiredArgument(string fieldKey, string? lang = null, string? fieldFallback = null)
            => Common.FieldRequiredArgument(fieldKey, lang, fieldFallback);

        public static string RequiredAtLine(string fieldKey, int lineNo, string? lang = null, string? fieldFallback = null)
            => Common.GetFieldRequiredAtLineMessage(fieldKey, lineNo, lang, fieldFallback);

        public static ArgumentException RequiredAtLineArgument(string fieldKey, int lineNo, string? lang = null, string? fieldFallback = null)
            => Common.FieldRequiredAtLineArgument(fieldKey, lineNo, lang, fieldFallback);
    }
}

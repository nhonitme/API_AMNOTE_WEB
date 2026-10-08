using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    public static class EInvoiceMinuteMailTemplateRenderer
    {
        public const string SendMinuteTemplateCd = "EMAIL_SEND_BBAN";

        public static (string Subject, string Body, bool IsBodyHtml) Render(
            EInvoiceTemplate template,
            EInvoiceMinuteInfo minute,
            string displayNo,
            string? frontendOrigin = null)
        {
            if (template == null)
            {
                throw new InvalidOperationException("E-invoice minute email template is not configured");
            }

            var subjectTemplate = Common.NormalizeNullableText(template.SUBJECT);
            var bodyTemplate = Common.NormalizeNullableText(template.CONTENT);

            if (string.IsNullOrWhiteSpace(subjectTemplate))
            {
                throw new InvalidOperationException("E-invoice minute email template subject is empty");
            }

            if (string.IsNullOrWhiteSpace(bodyTemplate))
            {
                throw new InvalidOperationException("E-invoice minute email template content is empty");
            }

            var tokens = BuildTokens(minute, displayNo, frontendOrigin);
            var subject = ReplaceTokens(subjectTemplate, tokens);
            var body = ReplaceTokens(bodyTemplate, tokens);

            return (subject, body, true);
        }

        public static string BuildPublicLookupUrl(string? frontendOrigin, EInvoiceMinuteInfo minute)
        {
            var lookupCode = Common.NormalizeNullableText(minute.MTRACUU);
            var taxCode = Common.NormalizeNullableText(minute.MSTNBAN)
                ?? Common.NormalizeNullableText(minute.MSTNMUA);
            var origin = Common.NormalizeNullableText(frontendOrigin)?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(origin)
                || string.IsNullOrWhiteSpace(lookupCode)
                || string.IsNullOrWhiteSpace(taxCode))
            {
                return string.Empty;
            }

            return $"{origin}/tra-cuu-bien-ban-amnote/{Uri.EscapeDataString(taxCode)}?mtracuu={Uri.EscapeDataString(lookupCode)}";
        }

        private static Dictionary<string, string> BuildTokens(
            EInvoiceMinuteInfo minute,
            string displayNo,
            string? frontendOrigin)
        {
            var lookupCode = Common.NormalizeNullableText(minute.MTRACUU) ?? string.Empty;

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DISPLAY_NO"] = displayNo,
                ["SBBAN"] = Common.NormalizeNullableText(minute.SBBAN) ?? string.Empty,
                ["TBBAN"] = Common.NormalizeNullableText(minute.TBBAN) ?? string.Empty,
                ["NBBAN"] = minute.NBBAN?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                ["SELLER_NM"] = Common.NormalizeNullableText(minute.NBAN) ?? string.Empty,
                ["SELLER_MST"] = Common.NormalizeNullableText(minute.MSTNBAN) ?? string.Empty,
                ["NMUA_TEN"] = Common.NormalizeNullableText(minute.NMUA) ?? string.Empty,
                ["NMUA_MST"] = Common.NormalizeNullableText(minute.MSTNMUA) ?? string.Empty,
                ["KHMSHDON"] = Common.NormalizeNullableText(minute.KHMSHDON) ?? string.Empty,
                ["KHHDON"] = Common.NormalizeNullableText(minute.KHHDON) ?? string.Empty,
                ["SHDON"] = Common.NormalizeNullableText(minute.SHDON) ?? string.Empty,
                ["NLAP"] = minute.NLAP?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                ["MTRACUU"] = lookupCode,
                ["LOOKUP_URL"] = BuildPublicLookupUrl(frontendOrigin, minute),
            };
        }

        private static string ReplaceTokens(string template, IReadOnlyDictionary<string, string> tokens)
        {
            var result = template;
            foreach (var token in tokens)
            {
                result = result.Replace($"{{{{{token.Key}}}}}", token.Value, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}

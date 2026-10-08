using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    public static class EInvoiceErrorNoticeMailTemplateRenderer
    {
        public const string SendErrorNoticeTemplateCd = "EMAIL_SEND_TBAO";

        public static (string Subject, string Body, bool IsBodyHtml) Render(
            EInvoiceTemplate template,
            EInvoiceErrorNoticeInfo notice,
            string displayNo)
        {
            if (template == null)
            {
                throw new InvalidOperationException("E-invoice error notice email template is not configured");
            }

            var subjectTemplate = Common.NormalizeNullableText(template.SUBJECT);
            var bodyTemplate = Common.NormalizeNullableText(template.CONTENT);

            if (string.IsNullOrWhiteSpace(subjectTemplate))
            {
                throw new InvalidOperationException("E-invoice error notice email template subject is empty");
            }

            if (string.IsNullOrWhiteSpace(bodyTemplate))
            {
                throw new InvalidOperationException("E-invoice error notice email template content is empty");
            }

            var tokens = BuildTokens(notice, displayNo);
            var subject = ReplaceTokens(subjectTemplate, tokens);
            var body = ReplaceTokens(bodyTemplate, tokens);

            return (subject, body, true);
        }

        private static Dictionary<string, string> BuildTokens(EInvoiceErrorNoticeInfo notice, string displayNo)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DISPLAY_NO"] = displayNo,
                ["MSO"] = Common.NormalizeNullableText(notice.MSO) ?? string.Empty,
                ["TEN"] = Common.NormalizeNullableText(notice.TEN) ?? string.Empty,
                ["SO"] = Common.NormalizeNullableText(notice.SO) ?? string.Empty,
                ["NTBAO"] = notice.NTBAO?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                ["NTBCCQT"] = notice.NTBCCQT?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                ["MST"] = Common.NormalizeNullableText(notice.MST) ?? string.Empty,
                ["TNNT"] = Common.NormalizeNullableText(notice.TNNT) ?? string.Empty,
                ["DDANH"] = Common.NormalizeNullableText(notice.DDANH) ?? string.Empty,
                ["MCQT"] = Common.NormalizeNullableText(notice.MCQT) ?? string.Empty,
                ["TCQT"] = Common.NormalizeNullableText(notice.TCQT) ?? string.Empty,
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

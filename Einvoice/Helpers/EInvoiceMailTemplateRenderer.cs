using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    public sealed record EInvoiceMailRenderOptions(
        string? FrontendOrigin = null,
        string? Website = null,
        string? SupportEmail = null,
        string? SupportPhone = null)
    {
        public static EInvoiceMailRenderOptions FromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            var origin = Common.NormalizeNullableText(configuration["Frontend:Origin"]);
            return new(
                origin,
                Common.NormalizeNullableText(configuration["EInvoice:Website"]) ?? origin,
                Common.NormalizeNullableText(configuration["EInvoice:SupportEmail"]) ?? string.Empty,
                Common.NormalizeNullableText(configuration["EInvoice:SupportPhone"]) ?? string.Empty);
        }
    }

    public static class EInvoiceMailTemplateRenderer
    {
        public const string EmailTemplateType = "EMAIL";
        public const string SendInvoiceTemplateCd = "EMAIL_SEND_INVOICE";

        public static (string Subject, string Body, bool IsBodyHtml) Render(
            EInvoiceTemplate template,
            EInvoiceInfo invoice,
            EInvoiceSellerInfo? seller,
            string displayNo,
            EInvoiceMailRenderOptions? options = null)
        {
            if (template == null)
            {
                throw new InvalidOperationException("E-invoice email template is not configured");
            }

            var subjectTemplate = Common.NormalizeNullableText(template.SUBJECT);
            var bodyTemplate = Common.NormalizeNullableText(template.CONTENT);
            if (string.IsNullOrWhiteSpace(subjectTemplate))
            {
                throw new InvalidOperationException("E-invoice email template subject is empty");
            }

            if (string.IsNullOrWhiteSpace(bodyTemplate))
            {
                throw new InvalidOperationException("E-invoice email template content is empty");
            }

            var tokens = BuildTokens(invoice, seller, displayNo, options ?? new EInvoiceMailRenderOptions());
            var subject = ReplaceTokens(subjectTemplate, tokens);
            var body = ReplaceTokens(bodyTemplate, tokens);
            return (subject, body, true);
        }

        public static string BuildPublicLookupUrl(
            string? frontendOrigin,
            EInvoiceInfo invoice,
            EInvoiceSellerInfo? seller)
        {
            var lookupCode = Common.NormalizeNullableText(invoice.MTRACUU);
            var taxCode = Common.NormalizeNullableText(seller?.SELLER_TAX_CD)
                ?? Common.NormalizeNullableText(invoice.SELLER_TAX_CD)
                ?? Common.NormalizeNullableText(invoice.NMUA_MST);
            var origin = Common.NormalizeNullableText(frontendOrigin)?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(origin)
                || string.IsNullOrWhiteSpace(lookupCode)
                || string.IsNullOrWhiteSpace(taxCode))
            {
                return string.Empty;
            }

            return $"{origin}/tra-cuu-einvoice-amnote/{Uri.EscapeDataString(taxCode)}?mtracuu={Uri.EscapeDataString(lookupCode)}";
        }

        private static Dictionary<string, string> BuildTokens(
            EInvoiceInfo invoice,
            EInvoiceSellerInfo? seller,
            string displayNo,
            EInvoiceMailRenderOptions options)
        {
            var lookupCode = Common.NormalizeNullableText(invoice.MTRACUU) ?? string.Empty;
            var website = Common.NormalizeNullableText(options.Website) ?? string.Empty;
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DISPLAY_NO"] = displayNo,
                ["SELLER_NM"] = Common.NormalizeNullableText(seller?.SELLER_NM) ?? Common.NormalizeNullableText(invoice.SELLER_NM) ?? string.Empty,
                ["SELLER_MST"] = Common.NormalizeNullableText(seller?.SELLER_TAX_CD) ?? Common.NormalizeNullableText(invoice.SELLER_TAX_CD) ?? string.Empty,
                ["NMUA_TEN"] = Common.NormalizeNullableText(invoice.NMUA_TEN) ?? string.Empty,
                ["NMUA_MST"] = Common.NormalizeNullableText(invoice.NMUA_MST) ?? string.Empty,
                ["NLAP"] = invoice.NLAP?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
                ["TGTTTBSO"] = invoice.TGTTTBSO?.ToString("N0", CultureInfo.InvariantCulture) ?? string.Empty,
                ["DVTTE"] = Common.NormalizeNullableText(invoice.DVTTE) ?? "VND",
                ["MTRACUU"] = lookupCode,
                ["KHMSHDON"] = Common.NormalizeNullableText(invoice.KHMSHDON) ?? string.Empty,
                ["KHHDON"] = Common.NormalizeNullableText(invoice.KHHDON) ?? string.Empty,
                ["SHDON"] = Common.NormalizeNullableText(invoice.SHDON) ?? string.Empty,
                ["LOOKUP_URL"] = BuildPublicLookupUrl(options.FrontendOrigin, invoice, seller),
                ["AMNOTE_WEBSITE"] = website,
                ["SUPPORT_EMAIL"] = Common.NormalizeNullableText(options.SupportEmail) ?? string.Empty,
                ["SUPPORT_PHONE"] = Common.NormalizeNullableText(options.SupportPhone) ?? string.Empty,
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

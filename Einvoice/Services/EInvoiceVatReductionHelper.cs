using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoiceVatReductionHelper
    {
        public const string TtkhacFieldName = "NQ204_GTTGT";
        public const string TtkhacLabelMessageKey = "EINV_NQ204_TTKHAC_LABEL";
        private const string ExtraFlagKey = "NQ204";

        public static bool IsSalesForm(string? khmsHDON)
        {
            return EInvoiceTaxCalculator.ResolveFormNumber(khmsHDON) == 2;
        }

        public static bool HasReduction(decimal? tgtKhac)
        {
            return (tgtKhac ?? 0m) > 0m;
        }

        public static bool IsActive(EInvoiceInfo invoice)
        {
            if (!IsSalesForm(invoice.KHMSHDON))
            {
                return false;
            }

            if (ReadExtraFlag(invoice.EXTRA_JSON))
            {
                return true;
            }

            return HasReduction(invoice.TGTKHAC);
        }

        public static string BuildTtkhacDescription(
            decimal amount,
            EInvoiceDecimalFormatter formatter,
            string? currencyCode,
            string? lang = null)
        {
            var formattedAmount = formatter.FormatHeader("TGTKHAC", amount, currencyCode);
            var language = Common.NormalizeLanguageCode(lang ?? Common.GetCurrentLanguage());
            var template = Common.getLanguageV2(TtkhacLabelMessageKey, language);
            if (string.IsNullOrWhiteSpace(template) && !string.Equals(language, "VIET", StringComparison.OrdinalIgnoreCase))
            {
                template = Common.getLanguageV2(TtkhacLabelMessageKey, "VIET");
            }

            if (string.IsNullOrWhiteSpace(template))
            {
                return formattedAmount;
            }

            return string.Format(CultureInfo.InvariantCulture, template, formattedAmount);
        }

        public static XElement? BuildPaymentTtkhac(
            EInvoiceInfo invoice,
            EInvoiceDecimalFormatter formatter,
            string? lang = null)
        {
            if (!IsSalesForm(invoice.KHMSHDON) || !HasReduction(invoice.TGTKHAC))
            {
                return null;
            }

            var description = BuildTtkhacDescription(invoice.TGTKHAC!.Value, formatter, invoice.DVTTE, lang);
            return new XElement(
                "TTKhac",
                new XElement(
                    "TTin",
                    new XElement("TTruong", TtkhacFieldName),
                    new XElement("KDLieu", "string"),
                    new XElement("DLieu", description)));
        }

        private static bool ReadExtraFlag(string? extraJson)
        {
            if (!TryReadExtraValue(extraJson, ExtraFlagKey, out var value))
            {
                return false;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => value.TryGetDecimal(out var number) && number != 0m,
                JsonValueKind.String => IsTruthy(value.GetString()),
                _ => false,
            };
        }

        private static bool TryReadExtraValue(string? extraJson, string key, out JsonElement value)
        {
            value = default;
            var text = Common.NormalizeNullableText(extraJson);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    value = property.Value.Clone();
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool IsTruthy(string? value)
        {
            var text = Common.NormalizeNullableText(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text is "1" or "true" or "True" or "TRUE" or "yes" or "Yes" or "YES";
        }
    }
}

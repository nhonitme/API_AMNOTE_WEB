using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;
using System.Globalization;
using System.Text.Json;

namespace API_AMNOTE_WEB.Helpers
{
    internal static class EInvoiceWarehouseHelper
    {
        public static bool IsWarehouseForm(string? khmsHDON)
        {
            return EInvoiceTaxCalculator.ResolveFormNumber(khmsHDON) == 6;
        }

        public static char? ResolveWarehouseVariant(string? khhHDON)
        {
            var text = (khhHDON ?? string.Empty).Trim().ToUpperInvariant();
            if (text.Length < 4)
            {
                return null;
            }

            return text[3] switch
            {
                'B' => 'B',
                'N' => 'N',
                _ => null
            };
        }

        public static string ResolvePxkType(string? khhHDON)
        {
            return ResolveWarehouseVariant(khhHDON) switch
            {
                'B' => "AGENCY",
                'N' => "INTERNAL",
                _ => string.Empty
            };
        }

        public static EInvoiceWarehouseFields ReadFields(EInvoicePxkInfo? pxkInfo, string? fallbackExtraJson = null)
        {
            if (pxkInfo == null)
            {
                return ReadFields(fallbackExtraJson);
            }

            return new EInvoiceWarehouseFields
            {
                NbanDChi = pxkInfo.NBAN_DCHI,
                HdktSo = pxkInfo.HDKTSO,
                HdktNgay = pxkInfo.HDKTNGAY?.ToString("yyyy-MM-dd"),
                LddnBo = pxkInfo.LDDNBO,
                HvtnxHang = pxkInfo.HVTNXHANG,
                TnvChuyen = pxkInfo.TNVCHUYEN,
                HdSo = pxkInfo.HDSO,
                PtvChuyen = pxkInfo.PTVCHUYEN
            };
        }

        public static EInvoiceWarehouseFields ReadFields(string? extraJson)
        {
            var fields = new EInvoiceWarehouseFields();
            if (string.IsNullOrWhiteSpace(extraJson))
            {
                return fields;
            }

            try
            {
                using var document = JsonDocument.Parse(extraJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return fields;
                }

                fields.NbanDChi = ReadText(document.RootElement, "NBAN_DCHI");
                fields.HdktSo = ReadText(document.RootElement, "HDKTSo");
                fields.HdktNgay = ReadText(document.RootElement, "HDKTNgay");
                fields.LddnBo = ReadText(document.RootElement, "LDDNBo");
                fields.HvtnxHang = ReadText(document.RootElement, "HVTNXHang");
                fields.TnvChuyen = ReadText(document.RootElement, "TNVChuyen");
                fields.HdSo = ReadText(document.RootElement, "HDSo");
                fields.PtvChuyen = ReadText(document.RootElement, "PTVChuyen");
            }
            catch
            {
                return new EInvoiceWarehouseFields();
            }

            return fields;
        }

        public static bool IsWarehouseConsignment(string? khmsHDON, string? khhHDON)
        {
            return IsWarehouseForm(khmsHDON) && ResolveWarehouseVariant(khhHDON) == 'B';
        }

        public static bool IsWarehouseInternal(string? khmsHDON, string? khhHDON)
        {
            return IsWarehouseForm(khmsHDON) && ResolveWarehouseVariant(khhHDON) == 'N';
        }

        public static void ValidateWarehouseInvoice(EInvoiceInfo invoice)
        {
            if (!IsWarehouseForm(invoice.KHMSHDON))
            {
                return;
            }

            var fields = ReadFields(invoice.PXK_INFO, invoice.EXTRA_JSON);
            var variant = ResolveWarehouseVariant(invoice.KHHDON);

            if (IsWarehouseConsignment(invoice.KHMSHDON, invoice.KHHDON))
            {
                if (string.IsNullOrWhiteSpace(fields.HdktSo))
                    throw EInvoiceValidationMessages.RequiredArgument("HDKTSo");

                if (!IsValidWarehouseDate(fields.HdktNgay))
                    throw EInvoiceValidationMessages.RequiredArgument("HDKTNgay");
            }

            if (string.IsNullOrWhiteSpace(fields.TnvChuyen))
                throw EInvoiceValidationMessages.RequiredArgument("TNVChuyen");

            if (string.IsNullOrWhiteSpace(fields.PtvChuyen))
                throw EInvoiceValidationMessages.RequiredArgument("PTVChuyen");

            if (variant == 'N' && string.IsNullOrWhiteSpace(fields.LddnBo))
                throw EInvoiceValidationMessages.RequiredArgument("LDDNBo");
        }

        private static bool IsValidWarehouseDate(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length >= 10)
            {
                text = text[..10];
            }

            if (text.Length != 10)
            {
                return false;
            }

            return DateTime.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }

        private static string? ReadText(JsonElement element, string key)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }

            return null;
        }
    }

    internal sealed class EInvoiceWarehouseFields
    {
        public string? NbanDChi { get; set; }
        public string? HdktSo { get; set; }
        public string? HdktNgay { get; set; }
        public string? LddnBo { get; set; }
        public string? HvtnxHang { get; set; }
        public string? TnvChuyen { get; set; }
        public string? HdSo { get; set; }
        public string? PtvChuyen { get; set; }
    }
}

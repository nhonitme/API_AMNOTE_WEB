using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using System.Globalization;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.Services
{
    internal static class EInvoicePrintXmlHelper
    {
        private static readonly string[] LookupInfoFieldNames =
        {
            "MaTraCuu",
            "MTRACUU",
            "Mã tra cứu",
            "Ma tra cuu",
            "Mã tra cứu hóa đơn",
            "Ma tra cuu hoa don"
        };

        public static string ApplyPrintInfo(string xml, EInvoicePrintOptions? options, string? convertedByNm, string? lookupCode = null)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return xml;
            }

            var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var root = doc.Root ?? throw new InvalidOperationException("Invoice XML root element is missing.");

            root.Elements("PrintInfo").Remove();

            var normalizedLookupCode = Common.NormalizeNullableText(lookupCode);
            if (!string.IsNullOrWhiteSpace(normalizedLookupCode))
            {
                ApplyLookupInfo(root, normalizedLookupCode);
            }

            if (options?.IsConvertedPrint != true)
            {
                return doc.ToString(SaveOptions.DisableFormatting);
            }

            var convertedName = Common.NormalizeNullableText(options.ConvertedByNm)
                ?? Common.NormalizeNullableText(convertedByNm);
            if (string.IsNullOrWhiteSpace(convertedName))
            {
                convertedName = Common.GetUserId();
            }

            root.Add(
                new XElement(
                    "PrintInfo",
                    new XElement("IS_CONVERTED_PRINT", "1"),
                    new XElement("CONVERTED_BY_NM", convertedName),
                    new XElement(
                        "CONVERTED_AT",
                        DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture))));

            return doc.ToString(SaveOptions.DisableFormatting);
        }

        private static void ApplyLookupInfo(XElement root, string lookupCode)
        {
            root.Elements()
                .Where(element => string.Equals(element.Name.LocalName, "MaTraCuu", StringComparison.OrdinalIgnoreCase))
                .Remove();

            var ttChung = root.Descendants()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TTChung", StringComparison.OrdinalIgnoreCase));

            if (ttChung == null)
            {
                return;
            }

            var ttKhac = ttChung.Elements()
                .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TTKhac", StringComparison.OrdinalIgnoreCase));

            if (ttKhac == null)
            {
                ttKhac = new XElement("TTKhac");
                ttChung.Add(ttKhac);
            }

            foreach (var element in ttKhac.Elements().ToList())
            {
                if (string.Equals(element.Name.LocalName, "MaTraCuu", StringComparison.OrdinalIgnoreCase) ||
                    IsLookupInfoRow(element))
                {
                    element.Remove();
                }
            }

            ttKhac.Add(new XElement("TTin",
                new XElement("TTruong", "MaTraCuu"),
                new XElement("KDLieu", "string"),
                new XElement("DLieu", lookupCode)));
        }

        private static bool IsLookupInfoRow(XElement element)
        {
            if (!string.Equals(element.Name.LocalName, "TTin", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fieldName = element.Elements()
                .FirstOrDefault(child => string.Equals(child.Name.LocalName, "TTruong", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            var normalizedFieldName = Common.NormalizeNullableText(fieldName);
            return !string.IsNullOrWhiteSpace(normalizedFieldName) &&
                LookupInfoFieldNames.Any(name => string.Equals(name, normalizedFieldName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
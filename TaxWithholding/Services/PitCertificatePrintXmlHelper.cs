using API_AMNOTE_WEB.Helpers;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.TaxWithholding;

internal static class PitCertificatePrintXmlHelper
{
    private static readonly string[] LookupInfoFieldNames =
    {
        "MaTraCuu",
        "MTRACUU",
        "Mã tra cứu",
        "Ma tra cuu",
        "Mã tra cứu chứng từ",
        "Ma tra cuu chung tu",
    };

    public static string EnsureLookupInfo(string xml, string? lookupCode)
    {
        var normalizedLookupCode = Common.NormalizeNullableText(lookupCode)?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(xml) || string.IsNullOrWhiteSpace(normalizedLookupCode))
            return xml;

        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var ttChung = doc.Root?.Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TTChung", StringComparison.OrdinalIgnoreCase));
        if (ttChung == null) return xml;

        var ttKhac = ttChung.Elements()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "TTKhac", StringComparison.OrdinalIgnoreCase));
        if (ttKhac == null)
        {
            ttKhac = new XElement("TTKhac");
            ttChung.Add(ttKhac);
        }

        foreach (var element in ttKhac.Elements().ToList())
        {
            if (string.Equals(element.Name.LocalName, "MaTraCuu", StringComparison.OrdinalIgnoreCase)
                || IsLookupInfoRow(element))
            {
                element.Remove();
            }
        }

        ttKhac.Add(new XElement("TTin",
            new XElement("TTruong", "MaTraCuu"),
            new XElement("KDLieu", "string"),
            new XElement("DLieu", normalizedLookupCode)));

        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static bool IsLookupInfoRow(XElement element)
    {
        if (!string.Equals(element.Name.LocalName, "TTin", StringComparison.OrdinalIgnoreCase))
            return false;

        var fieldName = element.Elements()
            .FirstOrDefault(child => string.Equals(child.Name.LocalName, "TTruong", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var normalizedFieldName = Common.NormalizeNullableText(fieldName);
        return !string.IsNullOrWhiteSpace(normalizedFieldName)
            && LookupInfoFieldNames.Any(name => string.Equals(name, normalizedFieldName, StringComparison.OrdinalIgnoreCase));
    }
}

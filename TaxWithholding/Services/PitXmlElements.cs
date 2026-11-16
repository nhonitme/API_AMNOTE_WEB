using System.Globalization;
using System.Xml.Linq;

namespace API_AMNOTE_WEB.TaxWithholding;

internal static class PitXmlElements
{
    public static XElement? Value(string name, object? value)
    {
        var text = value switch
        {
            null => "",
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal amount => amount.ToString("0.######", CultureInfo.InvariantCulture),
            string s => s.Trim(),
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
        return string.IsNullOrEmpty(text) ? null : new XElement(name, text);
    }
    public static string Serialize(string root, XElement payload, string signer)
        => new XElement(root, payload, new XElement("DSCKS", new XElement(signer))).ToString(SaveOptions.DisableFormatting);
}

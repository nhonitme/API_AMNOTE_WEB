using System.Globalization;
using System.Text.RegularExpressions;

namespace API_AMNOTE_WEB.Services
{
    internal static partial class EInvoiceTaxCalculator
    {
        private static readonly HashSet<string> NonPercentCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "KCT",
            "KKKNT",
            "KHAC",
            "CTTC"
        };

        public static bool IsWithoutTaxRate(string? khmsHDON)
        {
            var formNumber = ResolveFormNumber(khmsHDON);
            return formNumber is 2 or 6;
        }

        public static int? ResolveFormNumber(string? khmsHDON)
        {
            var text = (khmsHDON ?? string.Empty).Trim();
            var match = FormNumberRegex().Match(text);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var formNumber))
            {
                return null;
            }

            return formNumber;
        }

        public static string? NormalizeTaxRateCode(string? value)
        {
            var text = (value ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (NonPercentCodes.Contains(text))
                return text;

            var specialMatch = SpecialTaxRateRegex().Match(text);
            if (specialMatch.Success)
                return $"{specialMatch.Groups[1].Value}:{specialMatch.Groups[2].Value.Replace(',', '.')}%";

            var numberMatch = NumericTaxRateRegex().Match(text);
            if (numberMatch.Success)
                return $"{numberMatch.Groups[1].Value.Replace(',', '.')}%";

            return text;
        }

        public static decimal ResolveVatRatePercent(string? value)
        {
            return ResolveTaxPercent(value);
        }

        private static decimal ResolveTaxPercent(string? value)
        {
            var code = NormalizeTaxRateCode(value);
            if (string.IsNullOrWhiteSpace(code))
                return 0m;

            var match = PercentRegex().Match(code);
            if (!match.Success)
                return 0m;

            return decimal.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent)
                ? Math.Max(percent, 0m)
                : 0m;
        }

        [GeneratedRegex("^([0-9]+)", RegexOptions.CultureInvariant)]
        private static partial Regex FormNumberRegex();

        [GeneratedRegex("^(KHAC|CTTC):?([0-9]+(?:[\\.,][0-9]+)?)%?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex SpecialTaxRateRegex();

        [GeneratedRegex("^([0-9]+(?:[\\.,][0-9]+)?)%?$", RegexOptions.CultureInvariant)]
        private static partial Regex NumericTaxRateRegex();

        [GeneratedRegex("([0-9]+(?:\\.[0-9]+)?)%$", RegexOptions.CultureInvariant)]
        private static partial Regex PercentRegex();
    }
}

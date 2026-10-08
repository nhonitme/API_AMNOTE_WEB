using API_AMNOTE_WEB.Models;
using System.Globalization;

namespace API_AMNOTE_WEB.Services
{
    internal sealed class EInvoiceDecimalFormatter
    {
        private static readonly string[] ApplyTargetPriorities = ["TAX_XML", "UI", "INTERNAL_REPORT"];
        private static readonly CultureInfo XmlNumberCulture = CultureInfo.InvariantCulture;

        private readonly IReadOnlyList<EInvoiceDecimalSetting> _rules;

        private EInvoiceDecimalFormatter(IReadOnlyList<EInvoiceDecimalSetting> rules)
        {
            _rules = rules;
        }

        public static EInvoiceDecimalFormatter Create(IEnumerable<EInvoiceDecimalSetting> rules)
        {
            var normalized = rules
                .Where(rule => rule.IS_ACTIVE == 1 && !string.IsNullOrWhiteSpace(rule.FIELD_NAME))
                .Select(NormalizeRule)
                .OrderByDescending(rule => rule.XSL_ID)
                .ThenByDescending(rule => string.IsNullOrWhiteSpace(rule.COMPANY_CD) ? 0 : 1)
                .ThenBy(rule => rule.SORT_ORDER)
                .ThenBy(rule => rule.SETTING_ID)
                .ToList();

            return new EInvoiceDecimalFormatter(normalized);
        }

        public string FormatDetail(string fieldKey, decimal? value, string? currencyCode)
        {
            return Format("DETAIL", fieldKey, value, currencyCode, GetDetailFallbackPrecision(fieldKey, currencyCode));
        }

        public string FormatHeader(string fieldKey, decimal? value, string? currencyCode)
        {
            return Format("HEADER", fieldKey, value, currencyCode, GetHeaderFallbackPrecision(fieldKey, currencyCode));
        }

        public decimal RoundDetail(string fieldKey, decimal value, string? currencyCode)
        {
            return Round("DETAIL", fieldKey, value, currencyCode, GetDetailFallbackPrecision(fieldKey, currencyCode));
        }

        public decimal RoundHeader(string fieldKey, decimal value, string? currencyCode)
        {
            return Round("HEADER", fieldKey, value, currencyCode, GetHeaderFallbackPrecision(fieldKey, currencyCode));
        }

        private string Format(string fieldScope, string fieldKey, decimal? value, string? currencyCode, int fallbackPrecision)
        {
            if (!value.HasValue)
            {
                return string.Empty;
            }

            var precision = GetPrecision(fieldScope, fieldKey, currencyCode, fallbackPrecision);
            var rounded = RoundValue(value.Value, precision, GetRoundMode(fieldScope, fieldKey, currencyCode));
            return FormatRounded(rounded, precision);
        }

        private decimal Round(string fieldScope, string fieldKey, decimal value, string? currencyCode, int fallbackPrecision)
        {
            var precision = GetPrecision(fieldScope, fieldKey, currencyCode, fallbackPrecision);
            return RoundValue(value, precision, GetRoundMode(fieldScope, fieldKey, currencyCode));
        }

        private int GetPrecision(string fieldScope, string fieldKey, string? currencyCode, int fallbackPrecision)
        {
            return GetRule(fieldScope, fieldKey, currencyCode)?.DECIMAL_SCALE ?? ClampScale(fallbackPrecision);
        }

        private string GetRoundMode(string fieldScope, string fieldKey, string? currencyCode)
        {
            return GetRule(fieldScope, fieldKey, currencyCode)?.ROUND_MODE ?? "ROUND";
        }

        private EInvoiceDecimalSetting? GetRule(string fieldScope, string fieldKey, string? currencyCode)
        {
            var normalizedScope = NormalizeFieldScope(fieldScope);
            var normalizedFieldKey = NormalizeFieldKey(fieldKey);
            if (string.IsNullOrWhiteSpace(normalizedFieldKey))
            {
                return null;
            }

            var desiredCurrencyScope = ResolveCurrencyScope(normalizedFieldKey, currencyCode);

            foreach (var applyTarget in ApplyTargetPriorities)
            {
                var targetRules = _rules.Where(rule => string.Equals(rule.APPLY_TARGET, applyTarget, StringComparison.OrdinalIgnoreCase));
                var matched = PickFieldRule(targetRules, normalizedScope, normalizedFieldKey, desiredCurrencyScope);
                if (matched != null)
                {
                    return matched;
                }
            }

            return null;
        }

        private static EInvoiceDecimalSetting? PickFieldRule(
            IEnumerable<EInvoiceDecimalSetting> fieldRules,
            string fieldScope,
            string fieldKey,
            string currencyScope)
        {
            return fieldRules.FirstOrDefault(rule =>
                    string.Equals(rule.FIELD_SCOPE, fieldScope, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(rule.FIELD_NAME, fieldKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(rule.CURRENCY_SCOPE, currencyScope, StringComparison.OrdinalIgnoreCase))
                ?? fieldRules.FirstOrDefault(rule =>
                    string.Equals(rule.FIELD_SCOPE, fieldScope, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(rule.FIELD_NAME, fieldKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(rule.CURRENCY_SCOPE, "ANY", StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatRounded(decimal value, int precision)
        {
            var scale = ClampScale(precision);
            if (scale <= 0)
            {
                return value.ToString("0", XmlNumberCulture);
            }

            return value.ToString($"F{scale}", XmlNumberCulture);
        }

        private static decimal RoundValue(decimal value, int precision, string roundMode)
        {
            var scale = ClampScale(precision);
            var factor = (decimal)Math.Pow(10, scale);
            var scaled = value * factor;

            return (roundMode?.Trim().ToUpperInvariant()) switch
            {
                "TRUNCATE" => (scaled < 0m ? Math.Ceiling(scaled) : Math.Floor(scaled)) / factor,
                "CEIL" => Math.Ceiling(scaled) / factor,
                "FLOOR" => Math.Floor(scaled) / factor,
                _ => Math.Round(value, scale, MidpointRounding.AwayFromZero),
            };
        }

        private static int GetDetailFallbackPrecision(string fieldKey, string? currencyCode)
        {
            return fieldKey switch
            {
                "SLUONG" => 6,
                "TLCKHAU" => 4,
                "DGIA" or "STCKHAU" or "THTIEN" or "TTHUE" or "TSAUTHUE" => IsForeignCurrency(currencyCode) ? 2 : 0,
                _ => 2,
            };
        }

        private static int GetHeaderFallbackPrecision(string fieldKey, string? currencyCode)
        {
            return fieldKey switch
            {
                "TGIA" => 6,
                "TGTTTHUE" or "TGTCTHUE" or "TGTKCTHUE" or "TTCKTMAI" or "TGTKHAC" or "TGTTTBSO" => IsForeignCurrency(currencyCode) ? 2 : 0,
                _ => 2,
            };
        }

        private static bool IsForeignCurrency(string? currencyCode)
        {
            var normalized = (currencyCode ?? "VND").Trim().ToUpperInvariant();
            return normalized.Length > 0 && normalized != "VND";
        }

        private static string ResolveCurrencyScope(string fieldKey, string? currencyCode)
        {
            if (fieldKey.EndsWith("_VND", StringComparison.OrdinalIgnoreCase))
            {
                return "VND";
            }

            return IsForeignCurrency(currencyCode) ? "FC" : "VND";
        }

        private static EInvoiceDecimalSetting NormalizeRule(EInvoiceDecimalSetting rule)
        {
            return new EInvoiceDecimalSetting
            {
                SETTING_ID = rule.SETTING_ID,
                COMPANY_CD = rule.COMPANY_CD?.Trim() ?? string.Empty,
                XSL_ID = rule.XSL_ID > 0 ? rule.XSL_ID : 0,
                APPLY_TARGET = NormalizeApplyTarget(rule.APPLY_TARGET),
                FIELD_SCOPE = NormalizeFieldScope(rule.FIELD_SCOPE),
                FIELD_NAME = NormalizeFieldKey(rule.FIELD_NAME),
                LABEL_TEXT = rule.LABEL_TEXT?.Trim(),
                CAPTION = rule.CAPTION?.Trim(),
                CURRENCY_SCOPE = NormalizeCurrencyScope(rule.CURRENCY_SCOPE),
                DECIMAL_SCALE = ClampScale(rule.DECIMAL_SCALE),
                ROUND_MODE = NormalizeRoundMode(rule.ROUND_MODE),
                IS_ACTIVE = rule.IS_ACTIVE == 1 ? 1 : 0,
                SORT_ORDER = rule.SORT_ORDER,
                NOTE = rule.NOTE?.Trim() ?? string.Empty,
            };
        }

        private static string NormalizeFieldScope(string? value)
        {
            return string.Equals(value?.Trim(), "DETAIL", StringComparison.OrdinalIgnoreCase) ? "DETAIL" : "HEADER";
        }

        private static string NormalizeApplyTarget(string? value)
        {
            var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
            return normalized is "UI" or "INTERNAL_REPORT" ? normalized : "TAX_XML";
        }

        private static string NormalizeCurrencyScope(string? value)
        {
            var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
            return normalized is "VND" or "FC" ? normalized : "ANY";
        }

        private static string NormalizeRoundMode(string? value)
        {
            var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
            return normalized is "TRUNCATE" or "CEIL" or "FLOOR" ? normalized : "ROUND";
        }

        private static string NormalizeFieldKey(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();
            return normalized switch
            {
                "TGTTHUE" => "TGTTTHUE",
                _ => normalized,
            };
        }

        private static int ClampScale(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            return value > 12 ? 12 : value;
        }
    }
}

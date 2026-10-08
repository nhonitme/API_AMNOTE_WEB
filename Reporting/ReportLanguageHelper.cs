using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Reports
{
    internal static class ReportLanguageHelper
    {
        private static readonly string[] ColumnLanguageSuffixes = { "VIET", "ENG", "KOR", "CHINA" };

        public static string NormalizeLanguage(string? language)
            => Common.NormalizeLanguageCode(language);

        public static string LocalizeLabel(string key, string? language)
        {
            var normalizedKey = Common.NormalizeNullableText(key);
            if (normalizedKey == null)
            {
                return string.Empty;
            }

            var translated = Common.getLanguageV2(normalizedKey, NormalizeLanguage(language));
            return string.IsNullOrWhiteSpace(translated)
                ? normalizedKey
                : translated.Trim();
        }

        public static string LocalizeLabelOrFallback(string? key, string? fallback, string? language)
        {
            var normalizedFallback = Common.NormalizeNullableText(fallback);
            var normalizedKey = Common.NormalizeNullableText(key);
            if (normalizedKey == null)
            {
                return normalizedFallback ?? string.Empty;
            }

            var translated = LocalizeLabel(normalizedKey, language);
            return string.Equals(translated, normalizedKey, StringComparison.OrdinalIgnoreCase)
                ? normalizedFallback ?? translated
                : translated;
        }

        /// <summary>
        /// Match web getSysCodeDisplayText: translate(CODE_NAME) only. No CODE_CD fallback.
        /// </summary>
        public static string ResolveSysCodeDisplayText(string? codeName, string? language)
        {
            var normalizedName = Common.NormalizeNullableText(codeName);
            if (normalizedName == null)
            {
                return string.Empty;
            }

            return LocalizeLabelOrFallback(normalizedName, normalizedName, language);
        }

        /// <summary>
        /// Match web getFaStatusDisplayText: translate(status code) → translate(CODE_NAME) → CODE_NAME.
        /// </summary>
        public static string ResolveFaStatusDisplayText(string? statusCode, string? codeName, string? language)
        {
            var code = NormalizeFaStatusCode(statusCode);
            if (TryTranslateDistinct(code, language, out var translatedCode))
            {
                return translatedCode;
            }

            var name = Common.NormalizeNullableText(codeName);
            if (TryTranslateDistinct(name, language, out var translatedName))
            {
                return translatedName;
            }

            return name ?? code ?? string.Empty;
        }

        public static string? NormalizeFaStatusCode(string? status)
        {
            var normalized = Common.NormalizeNullableText(status)?.ToUpperInvariant();
            return normalized switch
            {
                null => null,
                "USING" => "IN_USE",
                "STOP" => "SUSPENDED",
                "FINISHED" => "SOLD",
                _ => normalized
            };
        }

        public static string ResolveDisplayLabel(string? labelText, string? language)
        {
            var label = Common.NormalizeNullableText(labelText);
            if (label == null)
            {
                return string.Empty;
            }

            var translated = Common.getLanguageV2(label, NormalizeLanguage(language));
            return string.IsNullOrWhiteSpace(translated) ? string.Empty : translated.Trim();
        }

        private static bool TryTranslateDistinct(string? key, string? language, out string translated)
        {
            translated = string.Empty;
            var normalizedKey = Common.NormalizeNullableText(key);
            if (normalizedKey == null)
            {
                return false;
            }

            translated = LocalizeLabel(normalizedKey, language);
            return !string.Equals(translated, normalizedKey, StringComparison.OrdinalIgnoreCase);
        }

        public static string RemoveLanguageSuffix(string? columnName)
        {
            var normalizedColumnName = Common.NormalizeNullableText(columnName);
            if (normalizedColumnName == null)
            {
                return string.Empty;
            }

            foreach (var suffix in ColumnLanguageSuffixes)
            {
                var token = $"_{suffix}";
                if (normalizedColumnName.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                {
                    return normalizedColumnName[..^token.Length];
                }
            }

            return normalizedColumnName;
        }

    }
}

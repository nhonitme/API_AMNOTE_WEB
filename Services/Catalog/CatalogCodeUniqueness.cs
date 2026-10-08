using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Services.Catalog;

/// <summary>
/// Shared code-uniqueness gate for catalog Create/BulkInsert (Excel import).
/// In-file duplicates + already-in-DB — one place for master services.
/// </summary>
public static class CatalogCodeUniqueness
{
    public static async Task EnsureNewCodesUniqueAsync(
        IEnumerable<string?> codes,
        Func<string, Task<bool>> existsAsync,
        string fieldName,
        string? lang = null)
    {
        var normalized = codes
            .Select(c => c?.Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .ToList();

        var duplicateInFile = normalized
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateInFile.Count > 0)
        {
            throw new InvalidOperationException(
                await BuildAlreadyExistsMessageAsync(fieldName, duplicateInFile, lang));
        }

        var existingInDb = new List<string>();
        foreach (var code in normalized.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await existsAsync(code))
                existingInDb.Add(code);
        }

        if (existingInDb.Count > 0)
        {
            throw new InvalidOperationException(
                await BuildAlreadyExistsMessageAsync(fieldName, existingInDb, lang));
        }
    }

    private static async Task<string> BuildAlreadyExistsMessageAsync(
        string fieldName,
        IEnumerable<string> duplicateValues,
        string? lang)
    {
        var normalizedLang = Common.NormalizeLanguageCode(lang);
        var fieldLabel = await Common.getLanguage(fieldName, normalizedLang);
        if (string.IsNullOrWhiteSpace(fieldLabel)
            || string.Equals(fieldLabel, fieldName, StringComparison.OrdinalIgnoreCase))
        {
            fieldLabel = fieldName;
        }

        const string alreadyExistsKey = "ALREADY_EXISTS";
        var alreadyExistsLabel = await Common.getLanguage(alreadyExistsKey, normalizedLang);
        if (string.IsNullOrWhiteSpace(alreadyExistsLabel)
            || string.Equals(alreadyExistsLabel, alreadyExistsKey, StringComparison.OrdinalIgnoreCase))
        {
            alreadyExistsLabel = "đã tồn tại";
        }

        return $"{fieldLabel} {alreadyExistsLabel}: {string.Join(", ", duplicateValues)}";
    }
}

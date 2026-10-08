using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    internal static class ExcelImportReferenceValidation
    {
        public static string? GetString(IDictionary<string, object> row, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = Common.GetStringValue(row, key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        public static void AddMissingCodeError(
            List<ExcelImportResultRowDto> validateResults,
            int rowNo,
            string fieldKey,
            string? fieldValue,
            IReadOnlyDictionary<string, long> lookup,
            string lang)
        {
            var normalizedValue = Common.NormalizeNullableText(fieldValue);
            if (normalizedValue == null || lookup.ContainsKey(normalizedValue))
            {
                return;
            }

            validateResults.Add(ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, fieldKey, normalizedValue, lang));
        }

        /// <summary>
        /// Optional FK: clear bad/legacy code and WARNING — do not fail the row (Product kind/store, Customer bank…).
        /// </summary>
        public static void SoftOptionalCode(
            List<ExcelImportResultRowDto> validateResults,
            IReadOnlyList<Dictionary<string, object>> rows,
            int index,
            int rowNo,
            string fieldKey,
            IReadOnlyDictionary<string, long> lookup,
            string lang)
        {
            var normalized = Common.NormalizeNullableText(Common.GetString(rows[index], fieldKey));
            if (normalized == null || lookup.ContainsKey(normalized))
            {
                return;
            }

            rows[index][fieldKey] = null!;
            validateResults.Add(new ExcelImportResultRowDto
            {
                RowNo = rowNo,
                Status = "WARNING",
                Message = ExcelImportHandlerHelper.BuildReferenceNotFoundError(rowNo, fieldKey, normalized, lang).Message
                    + $" (đã bỏ qua {fieldKey})",
                KeyValue = normalized
            });
        }

        public static long? ResolveId(string? fieldValue, IReadOnlyDictionary<string, long> lookup)
        {
            var normalizedValue = Common.NormalizeNullableText(fieldValue);
            if (normalizedValue == null)
            {
                return null;
            }

            return lookup.TryGetValue(normalizedValue, out var id) ? id : null;
        }
    }
}

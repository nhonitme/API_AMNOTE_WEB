using API_AMNOTE_WEB.Helpers;

namespace API_AMNOTE_WEB.Helpers
{
    public static class LookupMapBuilder
    {
        public static IReadOnlyDictionary<string, long> Build<T>(
            IEnumerable<T> rows,
            Func<T, string?> codeSelector,
            Func<T, long?> idSelector)
        {
            var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows ?? Enumerable.Empty<T>())
            {
                var code = Common.NormalizeNullableText(codeSelector(row));
                var id = idSelector(row).GetValueOrDefault();
                if (code == null || id <= 0 || result.ContainsKey(code))
                {
                    continue;
                }

                result[code] = id;
            }

            return result;
        }
    }
}

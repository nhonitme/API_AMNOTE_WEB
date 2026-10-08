namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// Report/grid convention: fields starting with <c>__</c> are system/technical
    /// (link targets, drill keys). Procedures may return them; UI auto-hides them.
    /// </summary>
    public static class ReportSystemFields
    {
        public const string Prefix = "__";

        public static bool IsSystemField(string? fieldName)
        {
            var name = Common.NormalizeNullableText(fieldName);
            if (name == null)
            {
                return false;
            }

            return name.StartsWith(Prefix, StringComparison.Ordinal);
        }
    }
}


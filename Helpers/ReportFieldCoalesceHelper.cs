using System.Data;

namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// BOOK layout may bind one print column to several SP fields via FIELD_NAME
    /// <c>RECEIPT_NO|PAYMENT_NO</c> — first non-empty wins (per row).
    /// Materializes a DataTable column named exactly as the FIELD_NAME expression.
    /// </summary>
    public static class ReportFieldCoalesceHelper
    {
        public const char Separator = '|';

        public static bool IsCoalesceExpression(string? fieldName)
        {
            var name = Common.NormalizeNullableText(fieldName);
            return name != null && name.Contains(Separator);
        }

        /// <summary>
        /// For each coalesce FIELD_NAME on <paramref name="layoutMetadata"/>, ensure the data
        /// table has a matching column filled with first non-empty source value.
        /// </summary>
        public static void MaterializeFromLayout(DataTable dataTable, DataTable? layoutMetadata)
        {
            if (dataTable == null || layoutMetadata == null || layoutMetadata.Rows.Count == 0)
            {
                return;
            }

            var fieldNameColumn = ResolveColumn(layoutMetadata, "FIELD_NAME");
            if (fieldNameColumn == null)
            {
                return;
            }

            var expressions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in layoutMetadata.Rows)
            {
                var fieldName = Common.NormalizeNullableText(row[fieldNameColumn]?.ToString());
                if (IsCoalesceExpression(fieldName))
                {
                    expressions.Add(fieldName!);
                }
            }

            foreach (var expression in expressions)
            {
                Materialize(dataTable, expression);
            }
        }

        public static void Materialize(DataTable dataTable, string coalesceExpression)
        {
            var expression = Common.NormalizeNullableText(coalesceExpression);
            if (expression == null || !expression.Contains(Separator))
            {
                return;
            }

            var sources = expression
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Common.NormalizeNullableText(part))
                .Where(part => part != null)
                .Cast<string>()
                .ToList();

            if (sources.Count == 0)
            {
                return;
            }

            var missingSources = sources
                .Where(source => ResolveColumn(dataTable, source) == null)
                .ToList();
            if (missingSources.Count > 0)
            {
                throw new InvalidOperationException(
                    $"BOOK layout coalesce FIELD_NAME '{expression}' references missing data columns: {string.Join(", ", missingSources)}.");
            }

            ReportDataTableHelper.EnsureColumn(dataTable, expression, typeof(string));
            var target = ResolveColumn(dataTable, expression)!;
            var sourceColumns = sources
                .Select(source => ResolveColumn(dataTable, source)!)
                .ToList();

            foreach (DataRow row in dataTable.Rows)
            {
                row[target] = PickFirstNonEmpty(row, sourceColumns);
            }
        }

        private static string PickFirstNonEmpty(DataRow row, IReadOnlyList<DataColumn> sourceColumns)
        {
            foreach (var column in sourceColumns)
            {
                var value = Common.NormalizeNullableText(row[column]?.ToString());
                if (value != null)
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static DataColumn? ResolveColumn(DataTable table, string columnName)
        {
            var exact = table.Columns.Cast<DataColumn>()
                .FirstOrDefault(column => string.Equals(column.ColumnName, columnName, StringComparison.Ordinal));
            if (exact != null)
            {
                return exact;
            }

            return table.Columns.Cast<DataColumn>()
                .FirstOrDefault(column => string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
        }
    }
}

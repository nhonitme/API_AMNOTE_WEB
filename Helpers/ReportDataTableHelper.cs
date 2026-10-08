using System.Data;

namespace API_AMNOTE_WEB.Helpers
{
    public static class ReportDataTableHelper
    {
        public static void EnsureColumn(DataTable table, string columnName, Type dataType)
        {
            if (!table.Columns.Contains(columnName))
            {
                var column = new DataColumn(columnName, dataType);
                if (dataType == typeof(string))
                {
                    column.MaxLength = -1;
                }

                table.Columns.Add(column);
                return;
            }

            if (dataType != typeof(string))
            {
                return;
            }

            var existing = table.Columns[columnName]!;
            existing.ReadOnly = false;

            // '' AS STATUS_TEXT from MySQL often yields MaxLength=0; writing display text then throws.
            // MaxLength cannot be widened once the table has rows — rebuild the column.
            if (existing.DataType == typeof(string) && existing.MaxLength >= 0)
            {
                RebuildUnboundedStringColumn(table, existing);
            }
        }

        private static void RebuildUnboundedStringColumn(DataTable table, DataColumn existing)
        {
            var columnName = existing.ColumnName;
            var caption = existing.Caption;
            var ordinal = existing.Ordinal;
            var values = new object[table.Rows.Count];
            for (var index = 0; index < table.Rows.Count; index++)
            {
                values[index] = table.Rows[index][existing];
            }

            table.Columns.Remove(existing);

            var replacement = new DataColumn(columnName, typeof(string))
            {
                MaxLength = -1,
                AllowDBNull = true,
                Caption = caption
            };
            table.Columns.Add(replacement);
            replacement.SetOrdinal(ordinal);

            for (var index = 0; index < table.Rows.Count; index++)
            {
                var value = values[index];
                table.Rows[index][replacement] = value ?? DBNull.Value;
            }
        }
    }
}

using System.Data;
using System.Data.OleDb;
using System.Text.Json.Nodes;

namespace Netade.Common.Helpers
{
    public static class RowsetBuilder
    {
        public static async Task<(List<Netade.Common.Messaging.ColumnDef> Columns, List<JsonNode?[]> Rows, bool HasMore)>
            ReadRowsAsync(OleDbDataReader reader, int maxRows, CancellationToken cancellationToken)
        {
            var columns = await ReadColumnsAsync(reader, cancellationToken).ConfigureAwait(false);
            var rows = new List<JsonNode?[]>(capacity: Math.Max(0, Math.Min(maxRows, 1024)));

            int fieldCount = reader.FieldCount;
            int read = 0;

            while (read < maxRows && await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new JsonNode?[fieldCount];

                for (int i = 0; i < fieldCount; i++)
                {
                    object value = reader.GetValue(i);
                    row[i] = ToJsonNode(value);
                }

                rows.Add(row);
                read++;
            }

            // If we hit maxRows, there *may* be more. We don’t want to consume an extra row here
            // because that would complicate cursor paging. So HasMore is “best-effort”.
            bool hasMore = (read == maxRows);

            return (columns, rows, hasMore);
        }

        private static async Task<List<Netade.Common.Messaging.ColumnDef>> ReadColumnsAsync(
            OleDbDataReader reader,
            CancellationToken cancellationToken)
        {
            var schemaTable = await reader.GetSchemaTableAsync(cancellationToken).ConfigureAwait(false);

            var cols = new List<Netade.Common.Messaging.ColumnDef>();
            if (schemaTable == null)
                return cols;

            foreach (DataRow row in schemaTable.Rows)
            {
                cols.Add(new Netade.Common.Messaging.ColumnDef
                {
                    Name = row.Field<string>("ColumnName") ?? "",
                    TypeName = (row["DataType"] as Type)?.FullName ?? "System.Object",
                    AllowDBNull = row.Table.Columns.Contains("AllowDBNull") ? row.Field<bool?>("AllowDBNull") : null,
                    MaxLength = row.Table.Columns.Contains("ColumnSize") ? row.Field<int?>("ColumnSize") : null
                });
            }

            return cols;
        }

        public static JsonNode? ToJsonNode(object value)
        {
            if (value is DBNull)
                return null;

            // Common primitives
            if (value is string s) return JsonValue.Create(s);
            if (value is bool b) return JsonValue.Create(b);
            if (value is short i16) return JsonValue.Create(i16);
            if (value is int i32) return JsonValue.Create(i32);
            if (value is long i64) return JsonValue.Create(i64);
            if (value is float f) return JsonValue.Create(f);
            if (value is double d) return JsonValue.Create(d);
            if (value is decimal dec) return JsonValue.Create(dec);

            // Dates: choose a consistent representation.
            // I recommend ISO-8601 strings for interoperability.
            if (value is DateTime dt) return JsonValue.Create(dt.ToString("O"));

            // GUIDs: as string
            if (value is Guid g) return JsonValue.Create(g.ToString());

            // Binary: base64 string
            if (value is byte[] bytes) return JsonValue.Create(Convert.ToBase64String(bytes));

            // Fallback: string
            return JsonValue.Create(value.ToString());
        }
    }
}

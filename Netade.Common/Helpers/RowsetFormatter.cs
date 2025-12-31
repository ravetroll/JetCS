using Netade.Common.Messaging;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Netade.Common.Helpers
{
    public static class RowsetFormatter
    {
        /// <summary>
        /// Matches the original DataTableFormatter behavior:
        /// - For each row: append each column value + "\t"
        /// - Then AppendLine()
        /// </summary>
        public static string RowsetToString(Rowset rowset, bool includeHeader = false)
        {
            var builder = new StringBuilder();

            if (rowset == null)
                return string.Empty;

            if (includeHeader && rowset.Columns != null)
            {
                foreach (var col in rowset.Columns)
                {
                    builder.Append((col?.Name ?? "") + "\t");
                }
                builder.AppendLine();
            }

            if (rowset.Rows == null)
                return builder.ToString();

            foreach (JsonNode?[] row in rowset.Rows)
            {
                if (row == null)
                {
                    builder.AppendLine();
                    continue;
                }

                for (int i = 0; i < row.Length; i++)
                {
                    builder.Append(FormatCell(row[i]) + "\t");
                }
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string FormatCell(JsonNode? cell)
        {
            if (cell is null)
                return "";

            if (cell is JsonValue v)
            {
                if (v.TryGetValue<string>(out var s))
                    return s ?? "";

                if (v.TryGetValue<bool>(out var b))
                    return b ? "true" : "false";

                if (v.TryGetValue<long>(out var l))
                    return l.ToString();

                if (v.TryGetValue<int>(out var i))
                    return i.ToString();

                if (v.TryGetValue<double>(out var d))
                    return d.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (v.TryGetValue<decimal>(out var m))
                    return m.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (v.TryGetValue<DateTime>(out var dt))
                    return dt.ToString("O");

                // Fallback for other primitive-ish values
                return v.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            }

            // Objects/arrays -> compact JSON, same cell slot
            return cell.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
    }


}



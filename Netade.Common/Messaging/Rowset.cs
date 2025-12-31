using System.Text.Json.Nodes;

namespace Netade.Common.Messaging
{
    public sealed class Rowset
    {
        public List<ColumnDef> Columns { get; set; } = new();
        public List<JsonNode?[]> Rows { get; set; } = new();

        /// <summary>
        /// Optional: set when known; otherwise -1.
        /// </summary>
        public int RecordCount { get; set; } = -1;
    }

    
}

using System.Text.Json.Nodes;

namespace Netade.Common.Messaging
{
   

    public sealed class ColumnDef
    {
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = ""; // "System.Int32", "System.String", ...
        public int? MaxLength { get; set; }
        public bool? AllowDBNull { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Common.Messaging
{
    public enum QueryResultMode
    {
        Snapshot = 0,
        Cursor = 1
    }

    public sealed class CommandOptions
    {
        // Query execution/result shaping
        public QueryResultMode ResultMode { get; set; } = QueryResultMode.Snapshot;

        // Cursor/paging
        public int FetchSize { get; set; } = 500;

        // Future-friendly: add more without changing all command constructors
        public int? CommandTimeoutSeconds { get; set; } = null;
        public int? MaxRows { get; set; } = null;

        // You can also add flags later
        public bool IncludeSchema { get; set; } = true;
    }
}

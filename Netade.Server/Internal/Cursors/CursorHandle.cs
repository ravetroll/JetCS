using Netade.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Internal.Cursors
{
    public sealed class CursorHandle : IAsyncDisposable
    {
        public Guid CursorId { get; init; }
        public string DatabaseName { get; init; } = "";
        public OleDbDataReader Reader { get; init; } = default!;
        public DbCommand Command { get; init; } = default!;
        public DbConnection Connection { get; init; } = default!;

        // Optional: columns schema cached at open time
        public ColumnDef[] Columns { get; init; } = Array.Empty<ColumnDef>();

        // IMPORTANT: a per-cursor gate so FETCH is serialized
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public async ValueTask DisposeAsync()
        {
            try { Reader?.Dispose(); } catch { }
            try { Command?.Dispose(); } catch { }
            try { await Connection.DisposeAsync().ConfigureAwait(false); } catch { }
            Gate.Dispose();
        }
    }
}




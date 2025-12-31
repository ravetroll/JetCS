using Netade.Common.Messaging;
using Netade.Server.Internal.Cursors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Services.Interfaces
{
    public interface ICursorRegistryService : IAsyncDisposable
    {
        Task<CursorOpenResult> OpenCursorAsync(
            string databaseName,
            string connectionString,
            string sql,
            int fetchSize,
            CancellationToken cancellationToken);

        Task<CursorPageResult> FetchAsync(
            string cursorId,
            int fetchSize,
            CancellationToken cancellationToken);

        Task<bool> CloseAsync(string cursorId);
    }
}


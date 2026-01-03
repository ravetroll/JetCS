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
            string login,
            string connectionString,
            string sql,
            int fetchSize,
            CancellationToken cancellationToken);

        Task<CursorPageResult> FetchAsync(
            string cursorId,
            string login,
            int fetchSize,
            CancellationToken cancellationToken);

        Task<bool> CloseAsync(string cursorId, string login);
        Task CloseAllAsync(CancellationToken cancellationToken = default);
    }
}


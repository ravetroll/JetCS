using Netade.Server.Internal.Database;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.Server.Services
{
    /// <summary>
    /// Global lock for protecting server-wide invariants (metadata, filesystem sync, cursor registry, etc.).
    /// This is NOT a per-database lock manager.
    /// </summary>
    public sealed class SystemLockService
    {
        private readonly AsyncReaderWriterLock systemLock = new();

        public ValueTask<AsyncReaderWriterLock.Releaser> EnterReadAsync(CancellationToken ct = default)
            => systemLock.EnterReadAsync(ct);

        public ValueTask<AsyncReaderWriterLock.Releaser> EnterWriteAsync(CancellationToken ct = default)
            => systemLock.EnterWriteAsync(ct);
    }
}

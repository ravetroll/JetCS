using Netade.Server.Internal.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.Server.Services
{
    public sealed class DatabaseLockService
    {
        private readonly ConcurrentDictionary<string, AsyncReaderWriterLock> locks =
            new(StringComparer.OrdinalIgnoreCase);

        public AsyncReaderWriterLock GetLock(string databaseKey)
            => locks.GetOrAdd(databaseKey ?? "", static _ => new AsyncReaderWriterLock());

        public ValueTask<IAsyncDisposable> EnterWriteManyAsync(
            IEnumerable<string> databaseKeys,
            CancellationToken ct = default)
            => EnterManyAsync(databaseKeys, LockMode.Write, ct);

        public ValueTask<IAsyncDisposable> EnterReadManyAsync(
            IEnumerable<string> databaseKeys,
            CancellationToken ct = default)
            => EnterManyAsync(databaseKeys, LockMode.Read, ct);

        /// <summary>
        /// Acquire multiple locks in a globally consistent order to prevent deadlocks.
        /// </summary>
        public async ValueTask<IAsyncDisposable> EnterManyAsync(
            IEnumerable<string> databaseKeys,
            LockMode mode,
            CancellationToken ct = default)
        {
            if (databaseKeys is null) throw new ArgumentNullException(nameof(databaseKeys));

            // Normalize + distinct
            var keys = databaseKeys
                .Select(k => k ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Global order: System "" first, then sorted.
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            if (keys.Remove(""))
                keys.Insert(0, "");

            // Acquire in-order; release in reverse order.
            var releasers = new List<AsyncReaderWriterLock.Releaser>(capacity: keys.Count);

            try
            {
                foreach (var key in keys)
                {
                    ct.ThrowIfCancellationRequested();

                    var lck = GetLock(key);

                    AsyncReaderWriterLock.Releaser r =
                        mode == LockMode.Write
                            ? await lck.EnterWriteAsync(ct).ConfigureAwait(false)
                            : await lck.EnterReadAsync(ct).ConfigureAwait(false);

                    releasers.Add(r);
                }

                return new MultiReleaser(releasers);
            }
            catch
            {
                // If acquisition fails/cancels halfway, release what we already took.
                for (var i = releasers.Count - 1; i >= 0; i--)
                {
                    try { await releasers[i].DisposeAsync().ConfigureAwait(false); }
                    catch { /* swallow to preserve original exception */ }
                }
                throw;
            }
        }

        public enum LockMode { Read, Write }

        private sealed class MultiReleaser : IAsyncDisposable
        {
            private List<AsyncReaderWriterLock.Releaser>? releasers;

            public MultiReleaser(List<AsyncReaderWriterLock.Releaser> releasers)
                => this.releasers = releasers;

            public async ValueTask DisposeAsync()
            {
                var list = Interlocked.Exchange(ref releasers, null);
                if (list is null) return;

                for (var i = list.Count - 1; i >= 0; i--)
                    await list[i].DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

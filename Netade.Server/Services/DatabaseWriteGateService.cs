
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.Server.Services
{
    /// <summary>
    /// Serializes writers per database key. Does NOT block writers on readers.
    /// </summary>
    public sealed class DatabaseWriteGateService
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> gates =
            new(StringComparer.OrdinalIgnoreCase);

        private SemaphoreSlim GetGate(string key)
            => gates.GetOrAdd(key ?? "", static _ => new SemaphoreSlim(1, 1));

        public async ValueTask<IAsyncDisposable> EnterWriteAsync(string databaseKey, CancellationToken ct = default)
        {
            var key = databaseKey ?? "";
            var gate = GetGate(key);
            await gate.WaitAsync(ct).ConfigureAwait(false);
            return new Releaser(gate);
        }

        /// <summary>
        /// Acquire multiple write gates in deterministic order to avoid deadlocks.
        /// </summary>
        public async ValueTask<IAsyncDisposable> EnterWriteManyAsync(IEnumerable<string> databaseKeys, CancellationToken ct = default)
        {
            if (databaseKeys is null) throw new ArgumentNullException(nameof(databaseKeys));

            var keys = databaseKeys
                .Select(k => k ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            keys.Sort(StringComparer.OrdinalIgnoreCase);

            var acquired = new List<SemaphoreSlim>(keys.Count);

            try
            {
                foreach (var key in keys)
                {
                    ct.ThrowIfCancellationRequested();
                    var gate = GetGate(key);
                    await gate.WaitAsync(ct).ConfigureAwait(false);
                    acquired.Add(gate);
                }

                return new MultiReleaser(acquired);
            }
            catch
            {
                for (var i = acquired.Count - 1; i >= 0; i--)
                {
                    try { acquired[i].Release(); } catch { /* swallow */ }
                }
                throw;
            }
        }

        private sealed class Releaser : IAsyncDisposable
        {
            private SemaphoreSlim? gate;
            public Releaser(SemaphoreSlim gate) => this.gate = gate;

            public ValueTask DisposeAsync()
            {
                Interlocked.Exchange(ref gate, null)?.Release();
                return ValueTask.CompletedTask;
            }
        }

        private sealed class MultiReleaser : IAsyncDisposable
        {
            private List<SemaphoreSlim>? gates;
            public MultiReleaser(List<SemaphoreSlim> gates) => this.gates = gates;

            public ValueTask DisposeAsync()
            {
                var list = Interlocked.Exchange(ref gates, null);
                if (list is null) return ValueTask.CompletedTask;

                for (var i = list.Count - 1; i >= 0; i--)
                {
                    try { list[i].Release(); } catch { /* swallow */ }
                }
                return ValueTask.CompletedTask;
            }
        }
    }
}

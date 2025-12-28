using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;





namespace Netade.Server.Internal.Database
{
    /// <summary>
    /// Async-compatible reader/writer lock.
    /// - Multiple concurrent readers
    /// - Writers are exclusive
    /// - Writers are given priority once any writer is waiting (prevents writer starvation)
    /// </summary>
    public sealed class AsyncReaderWriterLock
    {
        private readonly object gate = new();

        private int activeReaders = 0;
        private bool activeWriter = false;

        private int waitingWriters = 0;

        private readonly Queue<Waiter> waitingReadersQueue = new();
        private readonly Queue<Waiter> waitingWritersQueue = new();

        public ValueTask<Releaser> EnterReadAsync(CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                // Allow new readers only if no writer is active AND no writer is waiting.
                // This ensures writers don't starve behind a constant stream of readers.
                if (!activeWriter && waitingWriters == 0)
                {
                    activeReaders++;
                    return ValueTask.FromResult(new Releaser(this, ReleaserKind.Read));
                }

                return new ValueTask<Releaser>(EnqueueWaiter(waitingReadersQueue, ReleaserKind.Read, cancellationToken));
            }
        }

        public ValueTask<Releaser> EnterWriteAsync(CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                if (!activeWriter && activeReaders == 0)
                {
                    activeWriter = true;
                    return ValueTask.FromResult(new Releaser(this, ReleaserKind.Write));
                }

                waitingWriters++;
                return new ValueTask<Releaser>(EnqueueWaiter(waitingWritersQueue, ReleaserKind.Write, cancellationToken));
            }
        }

        private Task<Releaser> EnqueueWaiter(Queue<Waiter> queue, ReleaserKind kind, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<Releaser>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiter = new Waiter(kind, tcs, ct);
            queue.Enqueue(waiter);

            if (ct.CanBeCanceled)
            {
                waiter.CancellationRegistration = ct.Register(static state =>
                {
                    var w = (Waiter)state!;
                    // Mark canceled and attempt to complete; removal from queue happens lazily.
                    w.TryCancel();
                }, waiter);
            }

            return tcs.Task;
        }

        private void ExitRead()
        {
            Waiter? toReleaseWriter = null;

            lock (gate)
            {
                if (activeReaders <= 0)
                    throw new SynchronizationLockException("ExitRead called without an active reader.");

                activeReaders--;

                // If last reader left, a waiting writer (if any) gets priority.
                if (activeReaders == 0)
                {
                    toReleaseWriter = DequeueNextNonCanceled(waitingWritersQueue, decrementWaitingWriters: true);
                    if (toReleaseWriter != null)
                    {
                        activeWriter = true;
                    }
                    else
                    {
                        // No writers waiting; readers will be released by ExitWrite or future transitions.
                    }
                }
            }

            toReleaseWriter?.Release(this);
        }

        private void ExitWrite()
        {
            List<Waiter>? toReleaseReaders = null;
            Waiter? toReleaseWriter = null;

            lock (gate)
            {
                if (!activeWriter)
                    throw new SynchronizationLockException("ExitWrite called without an active writer.");

                activeWriter = false;

                // Prefer writers first (prevents writer starvation).
                toReleaseWriter = DequeueNextNonCanceled(waitingWritersQueue, decrementWaitingWriters: true);
                if (toReleaseWriter != null)
                {
                    activeWriter = true;
                }
                else
                {
                    // No writers waiting: release ALL queued readers.
                    toReleaseReaders = DequeueAllNonCanceled(waitingReadersQueue);
                    if (toReleaseReaders != null)
                    {
                        activeReaders += toReleaseReaders.Count;
                    }
                }
            }

            // Complete outside lock.
            if (toReleaseWriter != null)
            {
                toReleaseWriter.Release(this);
                return;
            }

            if (toReleaseReaders != null)
            {
                foreach (var r in toReleaseReaders)
                    r.Release(this);
            }
        }

        private Waiter? DequeueNextNonCanceled(Queue<Waiter> queue, bool decrementWaitingWriters)
        {
            while (queue.Count > 0)
            {
                var w = queue.Dequeue();
                if (w.IsCanceled)
                {
                    if (decrementWaitingWriters && w.Kind == ReleaserKind.Write)
                        waitingWriters--;
                    w.DisposeCancellationRegistration();
                    continue;
                }

                if (decrementWaitingWriters && w.Kind == ReleaserKind.Write)
                    waitingWriters--;

                w.DisposeCancellationRegistration();
                return w;
            }
            return null;
        }

        private static List<Waiter>? DequeueAllNonCanceled(Queue<Waiter> queue)
        {
            if (queue.Count == 0) return null;

            List<Waiter>? list = null;
            while (queue.Count > 0)
            {
                var w = queue.Dequeue();
                if (w.IsCanceled)
                {
                    w.DisposeCancellationRegistration();
                    continue;
                }

                w.DisposeCancellationRegistration();
                (list ??= new List<Waiter>()).Add(w);
            }
            return list;
        }

        public readonly struct Releaser : IAsyncDisposable, IDisposable
        {
            private readonly AsyncReaderWriterLock owner;
            private readonly ReleaserKind kind;

            internal Releaser(AsyncReaderWriterLock owner, ReleaserKind kind)
            {
                this.owner = owner;
                this.kind = kind;
            }

            public void Dispose()
            {
                if (owner == null) return;

                if (kind == ReleaserKind.Read) owner.ExitRead();
                else owner.ExitWrite();
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }

        internal enum ReleaserKind { Read, Write }

        private sealed class Waiter
        {
            public ReleaserKind Kind { get; }
            public TaskCompletionSource<Releaser> Tcs { get; }
            public CancellationTokenRegistration CancellationRegistration { get; set; }

            private int canceled = 0;
            public bool IsCanceled => Volatile.Read(ref canceled) != 0;

            public Waiter(ReleaserKind kind, TaskCompletionSource<Releaser> tcs, CancellationToken ct)
            {
                Kind = kind;
                Tcs = tcs;

                // If already canceled, mark immediately; we'll be skipped when dequeued.
                if (ct.IsCancellationRequested) canceled = 1;
            }

            public void TryCancel()
            {
                if (Interlocked.Exchange(ref canceled, 1) == 0)
                {
                    // Try to complete as canceled; if it loses a race with Release, one will win.
                    Tcs.TrySetCanceled();
                }
            }

            public void Release(AsyncReaderWriterLock owner)
            {
                // If canceled raced, this will fail and the lock slot was already accounted for.
                // That’s OK because we avoid granting canceled waiters by skipping them on dequeue.
                Tcs.TrySetResult(new Releaser(owner, Kind));
            }

            public void DisposeCancellationRegistration()
            {
                try { CancellationRegistration.Dispose(); } catch { /* ignore */ }
            }
        }
    }

    /// <summary>
    /// Example per-database locker store (Option 2).
    /// </summary>
    public sealed class DatabaseLockManager
    {
       

        
    }
}


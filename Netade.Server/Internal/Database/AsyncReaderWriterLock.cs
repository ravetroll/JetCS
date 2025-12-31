using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.Server.Internal.Database
{
    public sealed class AsyncReaderWriterLock
    {
        private readonly object gate = new();

        private int activeReaders = 0;
        private bool activeWriter = false;

        private readonly Queue<Waiter> waitingReadersQueue = new();
        private readonly Queue<Waiter> waitingWritersQueue = new();

        public ValueTask<Releaser> EnterReadAsync(CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                // writer-priority: if any writer is waiting, do not admit new readers
                PruneCanceledHead(waitingWritersQueue);

                if (!activeWriter && waitingWritersQueue.Count == 0)
                {
                    activeReaders++;
                    return ValueTask.FromResult(new Releaser(this, ReleaserKind.Read));
                }

                return new ValueTask<Releaser>(
                    EnqueueWaiter(waitingReadersQueue, ReleaserKind.Read, cancellationToken));
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

                return new ValueTask<Releaser>(
                    EnqueueWaiter(waitingWritersQueue, ReleaserKind.Write, cancellationToken));
            }
        }

        private Task<Releaser> EnqueueWaiter(Queue<Waiter> queue, ReleaserKind kind, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<Releaser>(TaskCreationOptions.RunContinuationsAsynchronously);
            var waiter = new Waiter(kind, tcs);

            queue.Enqueue(waiter);

            if (ct.CanBeCanceled)
            {
                waiter.CancellationRegistration = ct.Register(static state =>
                {
                    var w = (Waiter)state!;
                    w.TryCancel(); // only cancels if still Pending
                }, waiter);
            }

            // If ct already canceled, ensure we cancel promptly.
            if (ct.IsCancellationRequested)
                waiter.TryCancel();

            return tcs.Task;
        }

        private void ExitRead()
        {
            Waiter? writerToGrant = null;

            lock (gate)
            {
                if (activeReaders <= 0)
                    throw new SynchronizationLockException("ExitRead called without an active reader.");

                activeReaders--;

                if (activeReaders == 0)
                {
                    writerToGrant = DequeueNextGrantable(waitingWritersQueue);
                    if (writerToGrant != null)
                        activeWriter = true;
                }
            }

            writerToGrant?.Complete(this);
        }

        private void ExitWrite()
        {
            Waiter? writerToGrant = null;
            List<Waiter>? readersToGrant = null;

            lock (gate)
            {
                if (!activeWriter)
                    throw new SynchronizationLockException("ExitWrite called without an active writer.");

                activeWriter = false;

                writerToGrant = DequeueNextGrantable(waitingWritersQueue);
                if (writerToGrant != null)
                {
                    activeWriter = true;
                }
                else
                {
                    readersToGrant = DequeueAllGrantable(waitingReadersQueue);
                    if (readersToGrant != null)
                        activeReaders += readersToGrant.Count;
                }
            }

            if (writerToGrant != null)
            {
                writerToGrant.Complete(this);
                return;
            }

            if (readersToGrant != null)
            {
                foreach (var r in readersToGrant)
                    r.Complete(this);
            }
        }

        private static void PruneCanceledHead(Queue<Waiter> queue)
        {
            while (queue.Count > 0)
            {
                var w = queue.Peek();
                if (!w.IsCanceled) break;

                queue.Dequeue();
                w.DisposeCancellationRegistration();
            }
        }

        private static Waiter? DequeueNextGrantable(Queue<Waiter> queue)
        {
            while (queue.Count > 0)
            {
                var w = queue.Dequeue();

                // If it was canceled (or gets canceled), skip it.
                if (!w.TryGrant())
                {
                    w.DisposeCancellationRegistration();
                    continue;
                }

                w.DisposeCancellationRegistration();
                return w;
            }

            return null;
        }

        private static List<Waiter>? DequeueAllGrantable(Queue<Waiter> queue)
        {
            if (queue.Count == 0) return null;

            List<Waiter>? list = null;

            while (queue.Count > 0)
            {
                var w = queue.Dequeue();

                if (!w.TryGrant())
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
            // 0 = Pending, 1 = Granted, 2 = Canceled
            private int state = 0;

            public ReleaserKind Kind { get; }
            public TaskCompletionSource<Releaser> Tcs { get; }
            public CancellationTokenRegistration CancellationRegistration { get; set; }

            public bool IsCanceled => Volatile.Read(ref state) == 2;

            public Waiter(ReleaserKind kind, TaskCompletionSource<Releaser> tcs)
            {
                Kind = kind;
                Tcs = tcs;
            }

            public bool TryGrant()
                => Interlocked.CompareExchange(ref state, 1, 0) == 0;

            public void TryCancel()
            {
                if (Interlocked.CompareExchange(ref state, 2, 0) == 0)
                    Tcs.TrySetCanceled();
            }

            public void Complete(AsyncReaderWriterLock owner)
            {
                // At this point we must already be Granted.
                Tcs.TrySetResult(new Releaser(owner, Kind));
            }

            public void DisposeCancellationRegistration()
            {
                try { CancellationRegistration.Dispose(); } catch { }
            }
        }
    }
}

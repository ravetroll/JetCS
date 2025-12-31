
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netade.Server.Internal.Database;

namespace Netade.ServerTest.Concurrency
{
    [TestClass]
    public sealed class AsyncReaderWriterLockConcurrencyTests
    {
        [TestMethod]
        public async Task Stress_MixedReadersAndWriters_CompletesWithoutDeadlock()
        {
            var lck = new AsyncReaderWriterLock();

            const int workers = 32;
            var duration = TimeSpan.FromSeconds(3);
            var globalTimeout = TimeSpan.FromSeconds(15);

            using var runCts = new CancellationTokenSource(duration);

            long reads = 0;
            long writes = 0;
            long cancels = 0;
            long faults = 0;

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            Task[] tasks = Enumerable.Range(0, workers).Select(i => Task.Run(async () =>
            {
                var rng = new Random(unchecked(Environment.TickCount * 31 + i));

                await start.Task.ConfigureAwait(false);

                while (true)
                {
                    // Quick exit path (covers the case where we’re not currently blocked)
                    if (runCts.IsCancellationRequested)
                        break;

                    var isWrite = rng.Next(0, 4) == 0; // ~25% writes

                    // Link the operation token to runCts so the test always ends.
                    using var opCts = CancellationTokenSource.CreateLinkedTokenSource(runCts.Token);

                    if (rng.Next(0, 3) == 0)
                        opCts.CancelAfter(rng.Next(1, 8)); // 1-7 ms

                    try
                    {
                        if (!isWrite)
                        {
                            await using (await lck.EnterReadAsync(opCts.Token).ConfigureAwait(false))
                            {
                                Interlocked.Increment(ref reads);
                                if (rng.Next(0, 5) == 0)
                                    await Task.Yield();
                            }
                        }
                        else
                        {
                            await using (await lck.EnterWriteAsync(opCts.Token).ConfigureAwait(false))
                            {
                                Interlocked.Increment(ref writes);
                                if (rng.Next(0, 3) == 0)
                                    await Task.Delay(rng.Next(0, 2)).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref cancels);
                    }
                    catch
                    {
                        Interlocked.Increment(ref faults);
                        throw;
                    }
                }
            })).ToArray();

            start.SetResult();

            await WithTimeout(Task.WhenAll(tasks), globalTimeout).ConfigureAwait(false);

            Assert.AreEqual(0, Volatile.Read(ref faults), "No faults expected.");
            Assert.IsTrue(Volatile.Read(ref reads) > 0, "Expected at least some reads.");
            Assert.IsTrue(Volatile.Read(ref writes) > 0, "Expected at least some writes.");
        }


        [TestMethod]
        public async Task WriterPriority_ReaderQueuedAfterWriter_DoesNotSlipAhead()
        {
            var lck = new AsyncReaderWriterLock();

            var r0 = await lck.EnterReadAsync().ConfigureAwait(false);

            var wEnter = lck.EnterWriteAsync().AsTask();
            Assert.IsFalse(wEnter.IsCompleted, "Writer unexpectedly acquired immediately; test setup invalid.");

            var rEnter = lck.EnterReadAsync().AsTask();

            await r0.DisposeAsync().ConfigureAwait(false);

            var winner = await Task.WhenAny(
                wEnter,
                rEnter,
                Task.Delay(TimeSpan.FromSeconds(2))
            ).ConfigureAwait(false);

            Assert.AreSame(wEnter, winner, "Writer should acquire before the later reader when queued.");

            // Clean up: dispose acquired releasers in correct order.
            var wRel = await wEnter.ConfigureAwait(false);
            await wRel.DisposeAsync().ConfigureAwait(false);

            var rRel = await rEnter.ConfigureAwait(false);
            await rRel.DisposeAsync().ConfigureAwait(false);
        }


        [TestMethod]
        public async Task Cancellation_QueuedOperations_CanCancelUnderContention_WithoutHanging()
        {
            var lck = new AsyncReaderWriterLock();

            // Acquire write lock (not "await using" because we want to release early without double-dispose).
            var w0 = await lck.EnterWriteAsync().ConfigureAwait(false);

            const int n = 200;
            var tasks = new List<Task>(n);

            for (int i = 0; i < n; i++)
            {
                int capture = i;
                tasks.Add(Task.Run(async () =>
                {
                    using var cts = new CancellationTokenSource();
                    cts.CancelAfter(5);

                    try
                    {
                        if ((capture & 1) == 0)
                        {
                            await using (await lck.EnterReadAsync(cts.Token).ConfigureAwait(false))
                            {
                            }
                        }
                        else
                        {
                            await using (await lck.EnterWriteAsync(cts.Token).ConfigureAwait(false))
                            {
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // expected for many
                    }
                }));
            }

            // Release the initial writer exactly once.
            await w0.DisposeAsync().ConfigureAwait(false);

            await WithTimeout(Task.WhenAll(tasks), TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            // Ensure lock still works.
            await using (await lck.EnterReadAsync().ConfigureAwait(false))
            {
            }
        }

        private static async Task WithTimeout(Task task, TimeSpan timeout)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (!ReferenceEquals(completed, task))
                Assert.Fail($"Timed out after {timeout} (possible deadlock/hang).");

            await task.ConfigureAwait(false); // propagate exceptions
        }
    }
}

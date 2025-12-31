using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netade.Common;
using Netade.Common.Messaging;
using Netade.Server.Commands;
using Netade.Server.Internal.Cursors;
using Netade.Server.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public sealed class CloseCommandTests
    {
        [TestMethod]
        public async Task CloseCommand_InvalidArity_ReturnsError()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new CloseCommand(databases: null!, cursorRegistry: registry);

            // Missing cursorId
            var input = new Command { CommandText = "CLOSE" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual("CLOSE", result.CommandName);
            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            StringAssert.Contains(result.ErrorMessage ?? "", "Invalid 'CLOSE' Command");
        }

        [TestMethod]
        public async Task CloseCommand_InvalidGuid_ReturnsError()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new CloseCommand(databases: null!, cursorRegistry: registry);

            var input = new Command { CommandText = "CLOSE not-a-guid" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.AreEqual("Invalid cursor id.", result.ErrorMessage);
        }

        [TestMethod]
        public async Task CloseCommand_CursorNotFound_ReturnsError()
        {
            var registry = new FakeCursorRegistryService
            {
                CloseResult = false // simulate unknown cursor
            };
            var cmd = new CloseCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"CLOSE {id}" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.AreEqual("Cursor not found (maybe already closed).", result.ErrorMessage);

            // Ensure it normalized to "N" format before calling registry
            Assert.AreEqual(id.ToString("N"), registry.LastCloseCursorId);
        }

        [TestMethod]
        public async Task CloseCommand_Success_ReturnsAck()
        {
            var registry = new FakeCursorRegistryService
            {
                CloseResult = true
            };
            var cmd = new CloseCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"CLOSE {id}" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual("CLOSE", result.CommandName);
            Assert.AreEqual(CommandResultKind.Ack, result.Kind);
            Assert.AreEqual(id.ToString("N"), result.CursorId);
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
        }

        [TestMethod]
        public async Task CloseCommand_WhenRegistryThrowsObjectDisposed_ReturnsAck_Idempotent()
        {
            var registry = new FakeCursorRegistryService
            {
                ThrowObjectDisposedOnClose = true
            };
            var cmd = new CloseCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"CLOSE {id}" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(CommandResultKind.Ack, result.Kind);
            Assert.AreEqual(id.ToString("N"), result.CursorId);
        }

        [TestMethod]
        public async Task CloseCommand_Cancellation_ThrowsOperationCanceledException()
        {
            var registry = new FakeCursorRegistryService
            {
                // even if registry would succeed, we cancel before it matters
                CloseResult = true
            };
            var cmd = new CloseCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"CLOSE {id}" };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => cmd.ExecuteAsync(input, cts.Token));
        }

        /// <summary>
        /// Minimal fake for CloseCommand unit tests.
        /// (No Moq required; easy to debug.)
        /// </summary>
        private sealed class FakeCursorRegistryService : ICursorRegistryService
        {
            public bool CloseResult { get; set; } = true;
            public bool ThrowObjectDisposedOnClose { get; set; }

            public string? LastCloseCursorId { get; private set; }

            // Only CloseAsync is used by CloseCommand. The rest can throw if accidentally called.
            public Task<bool> CloseAsync(string cursorId)
            {
                LastCloseCursorId = cursorId;

                if (ThrowObjectDisposedOnClose)
                    throw new ObjectDisposedException(nameof(FakeCursorRegistryService));

                return Task.FromResult(CloseResult);
            }

            public Task<CursorOpenResult> OpenCursorAsync(string databaseName, string connectionString, string sql, int fetchSize, CancellationToken cancellationToken)
                => throw new NotSupportedException();

            public Task<CursorPageResult> FetchAsync(string cursorId, int fetchSize, CancellationToken cancellationToken)
                => throw new NotSupportedException();

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;
        }
    }
}

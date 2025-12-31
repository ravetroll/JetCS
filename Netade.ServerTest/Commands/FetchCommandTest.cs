using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netade.Common;
using Netade.Common.Messaging;
using Netade.Server.Commands;
using Netade.Server.Internal.Cursors;
using Netade.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public sealed class FetchCommandTests
    {
        [TestMethod]
        public async Task FetchCommand_InvalidArity_ReturnsError()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            // Missing cursorId
            var input = new Command { CommandText = "FETCH" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual("FETCH", result.CommandName);
            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            StringAssert.Contains(result.ErrorMessage ?? "", "Invalid 'FETCH' Command");
        }

        [TestMethod]
        public async Task FetchCommand_InvalidCursorId_ReturnsError()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var input = new Command { CommandText = "FETCH not-a-guid" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.AreEqual("Invalid cursor id.", result.ErrorMessage);
        }

        [TestMethod]
        public async Task FetchCommand_InvalidCountKeyword_ReturnsError()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var input = new Command { CommandText = $"FETCH {Guid.NewGuid()} SIZE 10" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            StringAssert.Contains(result.ErrorMessage ?? "", "Expected COUNT keyword.");
        }

        [TestMethod]
        public async Task FetchCommand_CountNotInteger_ReturnsError()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var input = new Command { CommandText = $"FETCH {Guid.NewGuid()} COUNT abc" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.AreEqual("Invalid COUNT value.", result.ErrorMessage);
        }

        [TestMethod]
        public async Task FetchCommand_CursorNotFound_ReturnsError()
        {
            var registry = new FakeCursorRegistryService
            {
                ThrowKeyNotFoundOnFetch = true
            };
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"FETCH {id}" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual("FETCH", result.CommandName);
            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.AreEqual("Cursor not found (maybe already closed).", result.ErrorMessage);

            // Ensure it normalizes to "N" before calling registry
            Assert.AreEqual(id.ToString("N"), registry.LastFetchCursorId);
        }

        [TestMethod]
        public async Task FetchCommand_DefaultCount_Uses100()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"FETCH {id}" };

            _ = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(id.ToString("N"), registry.LastFetchCursorId);
            Assert.AreEqual(100, registry.LastFetchSize);
        }

        [TestMethod]
        public async Task FetchCommand_Count_ClampsToMin1()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"FETCH {id} COUNT 0" };

            _ = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(1, registry.LastFetchSize);
        }

        [TestMethod]
        public async Task FetchCommand_Count_ClampsToMax5000()
        {
            var registry = new FakeCursorRegistryService();
            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"FETCH {id} COUNT 999999" };

            _ = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual(5000, registry.LastFetchSize);
        }

        [TestMethod]
        public async Task FetchCommand_Success_ReturnsCursorPage()
        {
            var testValues = new List<ColumnDef> { new ColumnDef { Name = "Id", TypeName = "System.Int32" }, new ColumnDef { Name = "Name", TypeName = "System.String" } };
            var registry = new FakeCursorRegistryService
            {
                FetchResult = new CursorPageResult
                {
                    HasMore = true,
                    Page = new Rowset
                    {
                        Columns = testValues,
                        Rows = new List<System.Text.Json.Nodes.JsonNode?[]>
                        {
                            new System.Text.Json.Nodes.JsonNode?[]
                            {
                                System.Text.Json.Nodes.JsonValue.Create(1),
                                System.Text.Json.Nodes.JsonValue.Create("Alice")
                            }
                        }
                    }
                }
            };

            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"FETCH {id} COUNT 10" };

            var result = await cmd.ExecuteAsync(input, CancellationToken.None);

            Assert.AreEqual("FETCH", result.CommandName);
            Assert.AreEqual(CommandResultKind.CursorPage, result.Kind);
            Assert.AreEqual(id.ToString("N"), result.CursorId);
            Assert.AreEqual(true, result.HasMore);

            Assert.IsNotNull(result.Data);
            var rowset = (Rowset)result.Data!;
            
            CollectionAssert.AreEqual(testValues, (System.Collections.ICollection)rowset.Columns);
            Assert.AreEqual(1, rowset.Rows.Count);
        }

        [TestMethod]
        public async Task FetchCommand_Cancellation_ThrowsOperationCanceledException()
        {
            var registry = new FakeCursorRegistryService
            {
                DelayFetchUntilCancelled = true
            };

            var cmd = new FetchCommand(databases: null!, cursorRegistry: registry);

            var id = Guid.NewGuid();
            var input = new Command { CommandText = $"FETCH {id} COUNT 10" };

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => cmd.ExecuteAsync(input, cts.Token));
        }

        /// <summary>
        /// Minimal fake registry to unit test FetchCommand without OleDb.
        /// Adjust method signatures if your ICursorRegistryService differs.
        /// </summary>
        private sealed class FakeCursorRegistryService : ICursorRegistryService
        {
            public string? LastFetchCursorId { get; private set; }
            public int LastFetchSize { get; private set; }

            public bool ThrowKeyNotFoundOnFetch { get; set; }
            public bool DelayFetchUntilCancelled { get; set; }

            public CursorPageResult FetchResult { get; set; } = new CursorPageResult
            {
                HasMore = false,
                Page = new Rowset
                {
                    Columns = new(),
                    Rows = new List<System.Text.Json.Nodes.JsonNode?[]>()
                }
            };

            public Task<CursorPageResult> FetchAsync(string cursorId, int fetchSize, CancellationToken cancellationToken)
            {
                LastFetchCursorId = cursorId;
                LastFetchSize = fetchSize;

                if (DelayFetchUntilCancelled)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (ThrowKeyNotFoundOnFetch)
                    throw new KeyNotFoundException();

                return Task.FromResult(FetchResult);
            }

            public Task<bool> CloseAsync(string cursorId) => throw new NotSupportedException();
            public Task<CursorOpenResult> OpenCursorAsync(string databaseName, string connectionString, string sql, int fetchSize, CancellationToken cancellationToken)
                => throw new NotSupportedException();
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

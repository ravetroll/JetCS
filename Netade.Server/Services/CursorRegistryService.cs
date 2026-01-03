using Netade.Common.Messaging;
using Netade.Domain;
using Netade.Server.Internal.Cursors;
using Netade.Server.Services.Interfaces;
using Serilog;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Text.Json.Nodes;

namespace Netade.Server.Services
{
    public sealed class CursorRegistryService : ICursorRegistryService, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, CursorState> _cursors = new();
        private volatile bool _shuttingDown;
        private readonly TimeSpan _idleTimeout;
        private readonly TimeSpan _sweepInterval;
        public int CursorCount => _cursors.Count; // for tests/diagnostics (or internal)

        private readonly CancellationTokenSource _disposeCts = new();
        private readonly Task _sweeper;

        public CursorRegistryService(TimeSpan? idleTimeout = null, TimeSpan? sweepInterval = null)
        {
            _idleTimeout = idleTimeout ?? TimeSpan.FromMinutes(2);
            _sweepInterval = sweepInterval ?? TimeSpan.FromSeconds(15);

            _sweeper = Task.Run(SweeperLoopAsync);
        }



        public async Task<CursorOpenResult> OpenCursorAsync(
            string databaseName,
            string login,
            string connectionString,
            string sql,
            int fetchSize,
            CancellationToken cancellationToken)
        {
            if (fetchSize <= 0) fetchSize = 500;

            // Create cursor state first so we can guarantee cleanup on any failure.
            var cursorId = Guid.NewGuid().ToString("N");
            var state = new CursorState(cursorId, databaseName,login);

            if (!_cursors.TryAdd(cursorId, state))
                throw new InvalidOperationException("Failed to allocate cursor.");

            try
            {
                state.Connection = new OleDbConnection(connectionString);
                await state.Connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                state.Command = new OleDbCommand(sql, state.Connection);

                // Important: keep reader open across multiple fetches
                state.Reader = await state.Command.ExecuteReaderAsync(
                    CommandBehavior.SequentialAccess,
                    cancellationToken).ConfigureAwait(false);

                if (state.Reader is null)
                    throw new InvalidOperationException("Command executed but returned null reader.");

                // Cache schema once
                state.Columns = await ReadColumnsAsync(state.Reader, cancellationToken).ConfigureAwait(false);

                // Prime 1-row lookahead (accurate HasMore without losing rows)
                await EnsureLookaheadAsync(state, cancellationToken).ConfigureAwait(false);

                // Return first page
                var page = await ReadPageAsync(state, fetchSize, cancellationToken).ConfigureAwait(false);

                return new CursorOpenResult
                {
                    CursorId = cursorId,
                    FirstPage = new Rowset { Columns = state.Columns, Rows = page.Rows, RecordCount = -1 },
                    HasMore = page.HasMore
                };
            }
            catch
            {
                await CloseInternalAsync(cursorId).ConfigureAwait(false);
                throw;
            }
        }

        public async Task<CursorPageResult> FetchAsync(string cursorId, string login, int fetchSize, CancellationToken cancellationToken)
        {
            if (fetchSize <= 0) fetchSize = 500;

            if (!_cursors.TryGetValue(cursorId, out var state))
                throw new KeyNotFoundException($"Cursor not found: {cursorId}");

            if (state.Login != login)
                throw new UnauthorizedAccessException("Login does not match cursor owner.");

            state.Touch();

            await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfClosed(state);

                // If we have no lookahead and cannot prime it => EOF, no more rows.
                await EnsureLookaheadAsync(state, cancellationToken).ConfigureAwait(false);

                if (!state.HasLookahead)
                {
                    state.Exhausted = true; // Mark the cursor as exhausted
                    return new CursorPageResult
                    {
                        Page = new Rowset { Columns = state.Columns, Rows = new List<JsonNode?[]>(), RecordCount = 0 },
                        HasMore = false
                    };
                }

                var page = await ReadPageAsync(state, fetchSize, cancellationToken).ConfigureAwait(false);

                // Check if the fetched page indicates exhaustion
                if (!page.HasMore)
                {
                    state.Exhausted = true; // Mark the cursor as exhausted
                }

                return new CursorPageResult
                {
                    Page = new Rowset { Columns = state.Columns, Rows = page.Rows, RecordCount = -1 },
                    HasMore = page.HasMore
                };
            }
            finally
            {
                state.Gate.Release();
            }
        }


        public async Task<bool> CloseAsync(string cursorId, string login)
        {
            if (!_cursors.TryGetValue(cursorId, out var state))
                throw new KeyNotFoundException($"Cursor not found: {cursorId}");

            if (state.Login != login)
                throw new UnauthorizedAccessException("Login does not match cursor owner.");
            return await CloseInternalAsync(cursorId);
        }


        public async ValueTask DisposeAsync()
        {
            _disposeCts.Cancel();

            try { await _sweeper.ConfigureAwait(false); }
            catch { /* ignore */ }

            await CloseAllAsync(CancellationToken.None).ConfigureAwait(false);

            _disposeCts.Dispose();
        }

        public async Task CloseAllAsync(CancellationToken cancellationToken = default)
        {
            _shuttingDown = true;

            var states = _cursors.Values.ToArray();

            // Close concurrently, but safely per-cursor using its Gate
            await Task.WhenAll(states.Select(s => CloseCursorStateAsync(s, remove: true, cancellationToken)))
                      .ConfigureAwait(false);
        }





        // ---------------------------
        // Internals
        // ---------------------------


        private async Task CloseCursorStateAsync(CursorState state, bool remove, CancellationToken cancellationToken)
        {
            await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (state.Closed) return;

                state.Closed = true;

                try { await state.Reader.DisposeAsync().ConfigureAwait(false); } catch { /* ignore/log */ }
                try { state.Command.Dispose(); } catch { /* ignore/log */ }
                try { await state.Connection.DisposeAsync().ConfigureAwait(false); } catch { /* ignore/log */ }

                if (remove)
                    _cursors.TryRemove(state.CursorId, out _);
            }
            finally
            {
                state.Gate.Release();
            }
        }

        private async Task SweeperLoopAsync()
        {
            using var timer = new PeriodicTimer(_sweepInterval);

            try
            {
                while (await timer.WaitForNextTickAsync(_disposeCts.Token).ConfigureAwait(false))
                {
                    var now = DateTimeOffset.UtcNow;

                    foreach (var kvp in _cursors)
                    {
                        var state = kvp.Value;
                        if (now - state.LastAccessUtc > _idleTimeout)
                        {
                            await CloseInternalAsync(state.CursorId).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // disposing
            }
        }

        private async Task<bool> CloseInternalAsync(string cursorId)
        {
            

            if (!_cursors.TryRemove(cursorId, out var state))
                return false;

            // Ensure no fetch is in progress while closing
            await state.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                state.IsClosed = true;

                if (state.Reader is not null)
                    await state.Reader.DisposeAsync().ConfigureAwait(false);

                state.Command?.Dispose();

                if (state.Connection is not null)
                    await state.Connection.DisposeAsync().ConfigureAwait(false);

                return true;
            }
            finally
            {
                state.Gate.Release();
                state.Gate.Dispose();
            }
        }

        private static void ThrowIfClosed(CursorState state)
        {
            if (state.Exhausted) 
                throw new InvalidOperationException("Cursor has been exhausted and cannot be fetched from."); 
            if (state.IsClosed)
                throw new ObjectDisposedException(nameof(CursorState), "Cursor is closed.");
            if (state.Reader is null)
                throw new ObjectDisposedException(nameof(CursorState), "Cursor reader is not available.");
        }

        /// <summary>
        /// Ensure there is either a lookahead row buffered, or we are at EOF.
        /// </summary>
        private static async Task EnsureLookaheadAsync(CursorState state, CancellationToken cancellationToken)
        {
            ThrowIfClosed(state);

            if (state.HasLookahead)
                return;

            // Attempt to read one row and buffer it
            if (await state.Reader!.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                state.LookaheadRow = ReadCurrentRowAsJson(state.Reader);
                state.HasLookahead = true;
            }
        }

        /// <summary>
        /// Reads up to fetchSize rows, using lookahead as the first row if present.
        /// Leaves lookahead populated if there are more rows.
        /// </summary>
        private static async Task<(List<JsonNode?[]> Rows, bool HasMore)> ReadPageAsync(
            CursorState state,
            int fetchSize,
            CancellationToken cancellationToken)
        {
            ThrowIfClosed(state);

            var rows = new List<JsonNode?[]>(capacity: Math.Min(fetchSize, 1024));

            // Consume buffered lookahead first (if any)
            if (state.HasLookahead)
            {
                rows.Add(state.LookaheadRow!);
                state.LookaheadRow = null;
                state.HasLookahead = false;
            }

            // Read remaining rows up to fetchSize
            while (rows.Count < fetchSize && await state.Reader!.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadCurrentRowAsJson(state.Reader));
            }

            // Prime lookahead for accurate HasMore (but only if we filled the page)
            if (rows.Count == fetchSize)
            {
                await EnsureLookaheadAsync(state, cancellationToken).ConfigureAwait(false);
                return (rows, state.HasLookahead);
            }

            // We did not fill page => EOF
            return (rows, false);
        }

        private static JsonNode?[] ReadCurrentRowAsJson(DbDataReader reader)
        {
            int fieldCount = reader.FieldCount;
            var row = new JsonNode?[fieldCount];

            for (int i = 0; i < fieldCount; i++)
            {
                object value = reader.GetValue(i);
                row[i] = ToJsonNode(value);
            }

            return row;
        }

        private static async Task<List<ColumnDef>> ReadColumnsAsync(DbDataReader reader, CancellationToken cancellationToken)
        {
            var schemaTable = await reader.GetSchemaTableAsync(cancellationToken).ConfigureAwait(false);

            var cols = new List<ColumnDef>();
            if (schemaTable is null)
                return cols;

            foreach (DataRow row in schemaTable.Rows)
            {
                cols.Add(new ColumnDef
                {
                    Name = row.Field<string>("ColumnName") ?? "",
                    TypeName = (row["DataType"] as Type)?.FullName ?? "System.Object",
                    AllowDBNull = schemaTable.Columns.Contains("AllowDBNull") ? row.Field<bool?>("AllowDBNull") : null,
                    MaxLength = schemaTable.Columns.Contains("ColumnSize") ? row.Field<int?>("ColumnSize") : null
                });
            }

            return cols;
        }

        // JSON representations for non-JSON CLR types
        private static JsonNode? ToJsonNode(object value)
        {
            if (value is DBNull) return null;

            if (value is string s) return JsonValue.Create(s);
            if (value is bool b) return JsonValue.Create(b);
            if (value is short i16) return JsonValue.Create(i16);
            if (value is int i32) return JsonValue.Create(i32);
            if (value is long i64) return JsonValue.Create(i64);
            if (value is float f) return JsonValue.Create(f);
            if (value is double d) return JsonValue.Create(d);
            if (value is decimal dec) return JsonValue.Create(dec);

            // Recommend ISO-8601 string for interoperability
            if (value is DateTime dt) return JsonValue.Create(dt.ToString("O"));

            if (value is Guid g) return JsonValue.Create(g.ToString());

            // Binary -> base64
            if (value is byte[] bytes) return JsonValue.Create(Convert.ToBase64String(bytes));

            // Fallback
            return JsonValue.Create(value.ToString());
        }

        private sealed class CursorState
        {
            public CursorState(string cursorId, string databaseName, string login)
            {
                CursorId = cursorId;
                DatabaseName = databaseName;
                Login = login;
                Touch();
            }

            public string CursorId { get; }
            public string DatabaseName { get; }
            public string Login { get; }

            public OleDbConnection? Connection { get; set; }
            public OleDbCommand? Command { get; set; }
            public DbDataReader? Reader { get; set; }

            public bool Exhausted { get; set; } = false; // Newly added
            public bool Closed { get; set; } = false; // Keep track of cursor closure if needed

            public List<ColumnDef> Columns { get; set; } = new();

            // One-row lookahead buffer
            public bool HasLookahead { get; set; }
            public JsonNode?[]? LookaheadRow { get; set; }

            public bool IsClosed { get; set; }

            public DateTimeOffset LastAccessUtc { get; private set; }

            // Per-cursor gate to serialize Fetch/Close operations
            public SemaphoreSlim Gate { get; } = new(1, 1);

            public void Touch() => LastAccessUtc = DateTimeOffset.UtcNow;
        }
    }
}


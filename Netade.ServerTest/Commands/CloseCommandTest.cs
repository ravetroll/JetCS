using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netade.Common.Messaging;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public class CloseCommandTests
    {
        private Server.Server server;

        private static string CloseCmd(string cursorId) => $"CLOSE {cursorId}";

        [TestMethod]
        public void Close_OpenCursor_ThenFetchErrors()
        {
            server = ServerSetup.BuildAndStartServer();

            // Admin bootstrap
            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST2");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test2 user1");

            // User session
            var cli = ServerSetup.BuildNetadeClient("TEST2", "127.0.0.1", "user1", "password", server.CompressedMode);

            // Setup data
            cli.SendCommand("CREATE TABLE Table1 (field2 int)");
            for (int i = 1; i <= 3; i++)
                cli.SendCommand($"INSERT INTO Table1 VALUES ({i})");

            // Open cursor
            var opened = cli.SendCommand("SELECT field2 FROM Table1 ORDER BY field2", new CommandOptions
            {
                ResultMode = QueryResultMode.Cursor,
                FetchSize = 1
            });

            Assert.AreEqual(CommandResultKind.CursorOpened, opened.Kind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(opened.CursorId));
            var cursorId = opened.CursorId!;

            // Close cursor
            var closed = cli.SendCommand(CloseCmd(cursorId));
            Assert.AreEqual("CLOSE", closed.CommandName);
            Assert.AreEqual(CommandResultKind.Ack, closed.Kind, "Expected CLOSE to succeed.");

            // Fetch should now error
            var after = cli.SendCommand($"FETCH {cursorId} COUNT 1");
            Assert.AreEqual("FETCH", after.CommandName);
            Assert.AreEqual(CommandResultKind.Error, after.Kind);
            Assert.IsTrue(
                after.ErrorMessage == "Invalid cursor id." ||
                after.ErrorMessage == "Cursor not found (maybe already closed)." ||
                after.ErrorMessage == "Cursor is closed.",
                $"Unexpected error message: {after.ErrorMessage}");
        }

        [TestMethod]
        public void Close_UnknownCursor_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();

            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST3");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test3 user1");

            var cli = ServerSetup.BuildNetadeClient("TEST3", "127.0.0.1", "user1", "password", server.CompressedMode);

            var result = cli.SendCommand(CloseCmd("doesnotexist"));

            Assert.AreEqual("CLOSE", result.CommandName);
            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
            Assert.IsTrue(
                result.ErrorMessage == "Invalid cursor id." ||
                result.ErrorMessage.StartsWith("Cursor not found", System.StringComparison.OrdinalIgnoreCase),
                $"Unexpected error message: {result.ErrorMessage}");
        }

        [TestMethod]
        public void Close_Twice_SecondCloseReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();

            // Admin bootstrap
            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST4");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test4 user1");

            var cli = ServerSetup.BuildNetadeClient("TEST4", "127.0.0.1", "user1", "password", server.CompressedMode);

            cli.SendCommand("CREATE TABLE Table1 (field2 int)");
            cli.SendCommand("INSERT INTO Table1 VALUES (1)");

            var opened = cli.SendCommand("SELECT field2 FROM Table1 ORDER BY field2", new CommandOptions
            {
                ResultMode = QueryResultMode.Cursor,
                FetchSize = 1
            });

            var cursorId = opened.CursorId!;

            var first = cli.SendCommand(CloseCmd(cursorId));
            Assert.AreEqual("CLOSE", first.CommandName);
            Assert.AreEqual(CommandResultKind.Ack, first.Kind);

            var second = cli.SendCommand(CloseCmd(cursorId));
            Assert.AreEqual("CLOSE", second.CommandName);
            Assert.AreEqual(CommandResultKind.Error, second.Kind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(second.ErrorMessage));
        }

        [TestMethod]
        public void Close_CursorOwnedByOtherLogin_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();

            // Admin bootstrap
            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST5");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("CREATE LOGIN USER2 password");
            admin.SendCommand("GRANT DATABASE test5 user1");
            admin.SendCommand("GRANT DATABASE test5 user2");

            var user1 = ServerSetup.BuildNetadeClient("TEST5", "127.0.0.1", "user1", "password", server.CompressedMode);
            var user2 = ServerSetup.BuildNetadeClient("TEST5", "127.0.0.1", "user2", "password", server.CompressedMode);

            user1.SendCommand("CREATE TABLE Table1 (field2 int)");
            user1.SendCommand("INSERT INTO Table1 VALUES (1)");
            user1.SendCommand("INSERT INTO Table1 VALUES (2)");

            var opened = user1.SendCommand("SELECT field2 FROM Table1 ORDER BY field2", new CommandOptions
            {
                ResultMode = QueryResultMode.Cursor,
                FetchSize = 1
            });

            var cursorId = opened.CursorId!;

            // Attempt to close as USER2
            var result = user2.SendCommand(CloseCmd(cursorId));

            Assert.AreEqual("CLOSE", result.CommandName);
            Assert.AreEqual(CommandResultKind.Error, result.Kind);

            // Security best practice: "Invalid cursor id." to avoid leaking existence/ownership.
            Assert.IsTrue(
                result.ErrorMessage == "Invalid cursor id." ||
                result.ErrorMessage.Contains("owner", System.StringComparison.OrdinalIgnoreCase) ||
                result.ErrorMessage.Contains("Unauthorized", System.StringComparison.OrdinalIgnoreCase),
                $"Unexpected error message: {result.ErrorMessage}");

            // Cursor should still be usable by the rightful owner (unless your implementation revokes it on attempted theft)
            var fetch = user1.SendCommand($"FETCH {cursorId} COUNT 1");
            Assert.AreEqual("FETCH", fetch.CommandName);
            Assert.AreEqual(CommandResultKind.CursorPage, fetch.Kind);
        }

        [TestMethod]
        public void Close_AfterCursorExhausted_BehaviorIsConsistent()
        {
            server = ServerSetup.BuildAndStartServer();

            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST6");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test6 user1");

            var cli = ServerSetup.BuildNetadeClient("TEST6", "127.0.0.1", "user1", "password", server.CompressedMode);

            cli.SendCommand("CREATE TABLE Table1 (field2 int)");
            cli.SendCommand("INSERT INTO Table1 VALUES (1)");

            var opened = cli.SendCommand("SELECT field2 FROM Table1 ORDER BY field2", new CommandOptions
            {
                ResultMode = QueryResultMode.Cursor,
                FetchSize = 1
            });

            var cursorId = opened.CursorId!;

            // Exhaust it (first page returns 1 row, HasMore=false)
            Assert.IsNotNull(opened.Data);
            Assert.AreEqual(1, opened.Data.Rows.Count);
            Assert.IsFalse(opened.HasMore);

            // Now CLOSE: depending on how you implemented exhaustion cleanup, this might be:
            // - OK (idempotent close) OR
            // - Error "Invalid cursor id." (cursor already removed on exhaustion)
            var close = cli.SendCommand(CloseCmd(cursorId));
            Assert.AreEqual("CLOSE", close.CommandName);
            Assert.IsTrue(
                close.Kind == CommandResultKind.Ack || close.Kind == CommandResultKind.Error,
                "Expected CLOSE to either succeed (idempotent) or error (already removed).");

            if (close.Kind == CommandResultKind.Error)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(close.ErrorMessage));
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            server?.Stop();
        }
    }
}

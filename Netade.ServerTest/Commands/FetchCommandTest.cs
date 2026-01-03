
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Netade.Common.Messaging;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public class FetchCommandTests
    {
        private Server.Server server;

        [TestMethod]
        public void Select_CursorMode_ThenFetch_PagesUntilExhausted_ThenFetchErrors()
        {
            server = ServerSetup.BuildAndStartServer();

            // Admin bootstrap
            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST1");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test1 user1");

            // User session
            var cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);

            // Setup data
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar, field2 int)");
            for (int i = 1; i <= 5; i++)
                cli.SendCommand($"INSERT INTO Table1 VALUES ('value{i}', {i})");

            // OPEN CURSOR via SELECT cursor mode
    
            var opened = cli.SendCommand("SELECT field2 FROM Table1 ORDER BY field2", new CommandOptions
            {
                ResultMode = QueryResultMode.Cursor,
                FetchSize = 2
            });

            Assert.AreEqual("SELECT", opened.CommandName);
            Assert.AreEqual(CommandResultKind.CursorOpened, opened.Kind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(opened.CursorId), "Expected CursorId from CursorOpened result.");

            Assert.IsNotNull(opened.Data);
            Assert.AreEqual(2, opened.Data.Rows.Count, "FirstPage should respect FetchSize=2.");
            Assert.IsTrue(opened.HasMore, "With 5 rows and FetchSize=2, HasMore should be true.");

            var cursorId = opened.CursorId!;

            // FETCH page 2
            var page2 = cli.SendCommand($"FETCH {cursorId} COUNT 2");
            Assert.AreEqual("FETCH", page2.CommandName);
            Assert.AreEqual(CommandResultKind.CursorPage, page2.Kind);
            Assert.IsNotNull(page2.Data);
            Assert.AreEqual(2, page2.Data.Rows.Count);
            Assert.IsTrue(page2.HasMore);

            // FETCH page 3 (final page: 1 row)
            var page3 = cli.SendCommand($"FETCH {cursorId} COUNT 2");
            Assert.AreEqual("FETCH", page3.CommandName);
            Assert.AreEqual(CommandResultKind.CursorPage, page3.Kind);
            Assert.IsNotNull(page3.Data);
            Assert.AreEqual(1, page3.Data.Rows.Count);
            Assert.IsFalse(page3.HasMore);

            // FETCH after HasMore=false should error (cursor removed/closed)
            var after = cli.SendCommand($"FETCH {cursorId} COUNT 2");
            Assert.AreEqual("FETCH", after.CommandName);
            Assert.AreEqual(CommandResultKind.Error, after.Kind);
            Assert.AreEqual("Cursor has been exhausted and cannot be fetched from.", after.ErrorMessage);
        }

        [TestMethod]
        public void Fetch_UnknownCursor_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();

            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST1");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test1 user1");

            var cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);

            var result = cli.SendCommand("FETCH doesnotexist COUNT 10");

            Assert.AreEqual("FETCH", result.CommandName);
            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.AreEqual("Invalid cursor id.", result.ErrorMessage);
        }

        [TestMethod]
        public void Fetch_CursorFromOtherLogin_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();

            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST1");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("CREATE LOGIN USER2 password");
            admin.SendCommand("GRANT DATABASE test1 user1");
            admin.SendCommand("GRANT DATABASE test1 user2");

            var user1 = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            var user2 = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user2", "password", server.CompressedMode);

            user1.SendCommand("CREATE TABLE Table1 (field2 int)");
            user1.SendCommand("INSERT INTO Table1 VALUES (1)");
            user1.SendCommand("INSERT INTO Table1 VALUES (2)");

            var opened = user1.SendCommand("SELECT field2 FROM Table1 ORDER BY field2", new CommandOptions
            {
                ResultMode = QueryResultMode.Cursor,
                FetchSize = 1
            });

            var cursorId = opened.CursorId!;
            var stolen = user2.SendCommand($"FETCH {cursorId} COUNT 1");

            Assert.AreEqual("FETCH", stolen.CommandName);
            Assert.AreEqual(CommandResultKind.Error, stolen.Kind);

            // Prefer not leaking that the cursor exists; either message is acceptable depending on your policy.
            // If you do return Unauthorized explicitly, assert that.
            Assert.IsTrue(
                stolen.ErrorMessage == "Invalid cursor id." ||
                stolen.ErrorMessage == "Login does not match cursor owner.");
        }

        [TestMethod]
        public void Fetch_CountZero_UsesDefaultFetchSize_AndDoesNotError()
        {
            server = ServerSetup.BuildAndStartServer();

            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST1");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test1 user1");

            var cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);

            cli.SendCommand("CREATE TABLE Table1 (field2 int)");
            for (int i = 1; i <= 3; i++)
                cli.SendCommand($"INSERT INTO Table1 VALUES ({i})");

            var opened = cli.SendCommand("SELECT field2 FROM Table1 ORDER BY field2", new CommandOptions
            {
                ResultMode = QueryResultMode.Cursor,
                FetchSize = 1
            });

            var cursorId = opened.CursorId!;

            // COUNT 0 should not blow up; it should behave like COUNT 500 (or your default)
            var page = cli.SendCommand($"FETCH {cursorId} COUNT 0");

            Assert.AreEqual("FETCH", page.CommandName);
            Assert.AreEqual(CommandResultKind.CursorPage, page.Kind);
            Assert.IsNotNull(page.Data);
            Assert.AreEqual(1, page.Data.Rows.Count, "Should fetch remaining rows.");
            Assert.IsTrue(page.HasMore);
        }

        [TestMethod]
        public void Fetch_InvalidSyntax_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();

            var admin = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            admin.SendCommand("CREATE DATABASE TEST1");
            admin.SendCommand("CREATE LOGIN USER1 password");
            admin.SendCommand("GRANT DATABASE test1 user1");

            var cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);

            var result = cli.SendCommand("FETCH COUNT 10"); // missing cursor id

            Assert.AreEqual("FETCH", result.CommandName);
            Assert.AreEqual(CommandResultKind.Error, result.Kind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }




        [TestCleanup]
        public void Cleanup()
        {
            server?.Stop();
        }
    }
}


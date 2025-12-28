using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public class CreateDatabaseCommandTest
    {
        private Server.Server server;

        [TestMethod]
        public void CreateSingleDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var result = cli.SendCommand("CREATE DATABASE TEST1");

            Assert.AreEqual("CREATE DATABASE", result.CommandName);
            Assert.AreEqual("TEST1", server.Databases.CreateDbContext().Databases.Single(t => t.Name.ToUpper() == "TEST1").Name);
        }

        [TestMethod]
        public void CreateManyDatabasesCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE DATABASE TEST2");
            var result = cli.SendCommand("CREATE DATABASE TEST3");

            Assert.AreEqual("CREATE DATABASE", result.CommandName);
            Assert.AreEqual(3, server.Databases.CreateDbContext().Databases.Count());
        }

        [TestMethod]
        public void CreateExistingDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            cli.SendCommand("CREATE DATABASE TEST3");
            var error = cli.SendCommand("CREATE DATABASE TEST3");

            Assert.AreEqual("CREATE DATABASE", error.CommandName);
            Assert.AreEqual("Database already exists: TEST3", error.ErrorMessage);
        }

        [TestMethod]
        public void CreateInvalidCharacterNamedDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var error = cli.SendCommand("CREATE DATABASE TEST/4");

            Assert.AreEqual("CREATE DATABASE", error.CommandName);
            Assert.AreEqual("Invalid character '/' in database name", error.ErrorMessage);
        }

        /// <summary>
        /// FIXED: 4 tokens is now VALID syntax (the 4th token is the optional type),
        /// so to test "invalid command syntax" we must supply 5 tokens.
        /// </summary>
        [TestMethod]
        public void CreateDatabaseCommandInvalidSyntax_TooManyTokens()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var error = cli.SendCommand("CREATE DATABASE TEST NotAllowed EXTRA");

            Assert.AreEqual("CREATE DATABASE", error.CommandName);
            Assert.IsTrue(
                error.ErrorMessage.StartsWith("Invalid 'CREATE DATABASE' Command:", System.StringComparison.OrdinalIgnoreCase),
                $"Unexpected error: {error.ErrorMessage}");
        }

        [TestMethod]
        public void CreateDatabaseCommand_WithAccdbType_CreatesAccdb()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var result = cli.SendCommand("CREATE DATABASE TESTACC accdb");

            Assert.AreEqual("CREATE DATABASE", result.CommandName);

            var db = server.Databases.CreateDbContext().Databases.Single(t => t.Name.ToUpper() == "TESTACC");
            Assert.IsTrue(db.FilePath.EndsWith(".accdb", System.StringComparison.OrdinalIgnoreCase), db.FilePath);
        }

        [TestMethod]
        public void CreateDatabaseCommand_WithMdbType_CreatesMdbOrFailsIfNotSupported()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var result = cli.SendCommand("CREATE DATABASE TESTMDB mdb");

            Assert.AreEqual("CREATE DATABASE", result.CommandName);

            // Two valid outcomes depending on provider capability:
            // 1) MDB supported -> row exists and ends with .mdb
            // 2) MDB not supported -> command returns an error message
            if (string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                var db = server.Databases.CreateDbContext().Databases.Single(t => t.Name.ToUpper() == "TESTMDB");
                Assert.IsTrue(db.FilePath.EndsWith(".mdb", System.StringComparison.OrdinalIgnoreCase), db.FilePath);
            }
            else
            {
                Assert.AreEqual("MDB creation is not supported with the current provider setup.", result.ErrorMessage);
            }
        }

        [TestMethod]
        public void CreateDatabaseCommand_InvalidTypeToken_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var error = cli.SendCommand("CREATE DATABASE TESTBADTYPE NotAllowed");

            Assert.AreEqual("CREATE DATABASE", error.CommandName);
            Assert.AreEqual("Type must be 'mdb', 'accdb', or 'auto'. (Parameter 'type')", error.ErrorMessage);
        }

        [TestCleanup]
        public void Cleanup()
        {
            server?.Stop();
        }
    }
}

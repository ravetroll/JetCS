using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public class AlterDatabaseCommandTest
    {
        private Server.Server server;

        [TestMethod]
        public void AlterDatabaseRenameCommand_RenamesDatabaseAndUpdatesMetadata()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            // Arrange: create database first
            cli.SendCommand("CREATE DATABASE TESTOLD");

            var dbBefore = server.Databases.CreateDbContext().Databases
                .Single(t => t.Name.ToUpper() == "TESTOLD");

            // Act
            var result = cli.SendCommand("ALTER DATABASE TESTOLD RENAME TESTNEW");

            // Assert
            Assert.AreEqual("ALTER DATABASE", result.CommandName);
            Assert.IsTrue(string.IsNullOrWhiteSpace(result.ErrorMessage), result.ErrorMessage);

            using var ctx = server.Databases.CreateDbContext();
            Assert.AreEqual(0, ctx.Databases.Count(t => t.Name.ToUpper() == "TESTOLD"));
            var dbAfter = ctx.Databases.Single(t => t.Name.ToUpper() == "TESTNEW");

            Assert.IsTrue(File.Exists(dbAfter.FilePath), $"Expected file to exist: {dbAfter.FilePath}");
            Assert.AreEqual(
                Path.GetFileNameWithoutExtension(dbAfter.FilePath).ToUpperInvariant(),
                "TESTNEW");

            // Helpful sanity: extension should be preserved
            Assert.AreEqual(Path.GetExtension(dbBefore.FilePath), Path.GetExtension(dbAfter.FilePath));
        }

        [TestMethod]
        public void AlterDatabaseRenameCommand_WhenOldDoesNotExist_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var error = cli.SendCommand("ALTER DATABASE DOESNOTEXIST RENAME NEWNAME");

            Assert.AreEqual("ALTER DATABASE", error.CommandName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error.ErrorMessage));
            Assert.IsTrue(
                error.ErrorMessage.StartsWith("Error executing 'ALTER DATABASE' Command:", StringComparison.OrdinalIgnoreCase),
                $"Unexpected error: {error.ErrorMessage}");
        }

        [TestMethod]
        public void AlterDatabaseRenameCommand_WhenNewAlreadyExists_ReturnsError_AndDoesNotChangeAnything()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE DATABASE TEST2");

            var ctxBefore = server.Databases.CreateDbContext();
            var test1Path = ctxBefore.Databases.Single(t => t.Name.ToUpper() == "TEST1").FilePath;
            var test2Path = ctxBefore.Databases.Single(t => t.Name.ToUpper() == "TEST2").FilePath;

            var error = cli.SendCommand("ALTER DATABASE TEST1 RENAME TEST2");

            Assert.AreEqual("ALTER DATABASE", error.CommandName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error.ErrorMessage));

            // Ensure nothing got renamed
            using var ctxAfter = server.Databases.CreateDbContext();
            Assert.AreEqual(1, ctxAfter.Databases.Count(t => t.Name.ToUpper() == "TEST1"));
            Assert.AreEqual(1, ctxAfter.Databases.Count(t => t.Name.ToUpper() == "TEST2"));

            Assert.AreEqual(test1Path, ctxAfter.Databases.Single(t => t.Name.ToUpper() == "TEST1").FilePath, ignoreCase: true);
            Assert.AreEqual(test2Path, ctxAfter.Databases.Single(t => t.Name.ToUpper() == "TEST2").FilePath, ignoreCase: true);

            Assert.IsTrue(File.Exists(test1Path), $"Expected file to still exist: {test1Path}");
            Assert.IsTrue(File.Exists(test2Path), $"Expected file to still exist: {test2Path}");
        }

        [TestMethod]
        public void AlterDatabaseCommand_InvalidSyntax_TooFewTokens()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var error = cli.SendCommand("ALTER DATABASE TEST1 RENAME"); // 4 tokens

            Assert.AreEqual("ALTER DATABASE", error.CommandName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error.ErrorMessage));
            Assert.IsTrue(
                error.ErrorMessage.StartsWith("Invalid 'ALTER DATABASE' Command:", StringComparison.OrdinalIgnoreCase),
                $"Unexpected error: {error.ErrorMessage}");
        }

        [TestMethod]
        public void AlterDatabaseCommand_InvalidSyntax_TooManyTokens()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var error = cli.SendCommand("ALTER DATABASE TEST1 RENAME TEST2 EXTRA"); // 6 tokens

            Assert.AreEqual("ALTER DATABASE", error.CommandName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error.ErrorMessage));
            Assert.IsTrue(
                error.ErrorMessage.StartsWith("Invalid 'ALTER DATABASE' Command:", StringComparison.OrdinalIgnoreCase),
                $"Unexpected error: {error.ErrorMessage}");
        }

        [TestMethod]
        public void AlterDatabaseCommand_InvalidActionToken_ReturnsError()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);

            var error = cli.SendCommand("ALTER DATABASE TEST1 MOVETO TEST2");

            Assert.AreEqual("ALTER DATABASE", error.CommandName);
            Assert.AreEqual("Invalid action 'MOVETO' in 'ALTER DATABASE' Command:ALTER DATABASE TEST1 MOVETO TEST2", error.ErrorMessage);
        }

        [TestCleanup]
        public void Cleanup()
        {
            server?.Stop();
        }
    }
}

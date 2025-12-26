using JetCS.Common;
using JetCS.Persistence;
using JetCS.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JetCS.ServerTest.Commands
{
    [TestClass]
    public class DropDatabaseCommandTest
    {
        
        Server.Server server;

        [TestInitialize]
        public void Initialize()
        {
            
        }

       

        [TestMethod]
        public void DropSingleDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            var result =cli.SendCommand("CREATE DATABASE TEST1");
            result = cli.SendCommand("CREATE DATABASE TEST2");
            result = cli.SendCommand("DROP DATABASE TEST2");
            Assert.AreEqual("DROP DATABASE", result.CommandName);
            Assert.AreEqual( 1, server.Databases.CreateDbContext().Databases.Count());
            
        }

        [TestMethod]
        public void DropManyDatabasesCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");            
            cli.SendCommand("CREATE DATABASE TEST2");
            cli.SendCommand("CREATE DATABASE TEST3");
            cli.SendCommand("CREATE DATABASE TEST4");
            var result =cli.SendCommand("DROP DATABASE TEST3");
            result = cli.SendCommand("DROP DATABASE TEST2");
            Assert.AreEqual("DROP DATABASE", result.CommandName);
            Assert.AreEqual(2, server.Databases.CreateDbContext().Databases.Count());
            
        }

        [TestMethod]
        public void DropNonExistingDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);            
            cli.SendCommand("CREATE DATABASE TEST3");           
            var result = cli.SendCommand("DROP DATABASE TEST4");
            Assert.AreEqual("DROP DATABASE", result.CommandName);
            Assert.AreEqual("Database TEST4 does not exist", result.ErrorMessage);
           
        }

        
        [TestMethod]
        public void CreateDatabaseCommandInvalid()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);  
            var result = cli.SendCommand("DROP DATABASE TEST NotAllowed");
            Assert.AreEqual("DROP DATABASE", result.CommandName);
            Assert.AreEqual("Invalid 'DROP DATABASE' Command:DROP DATABASE TEST NotAllowed", result.ErrorMessage);
        }

        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();
            
           
        }
    }
}
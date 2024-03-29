using JetCS.Common;
using JetCS.Persistence;
using JetCS.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JetCS.ServerTest.Commands
{
    [TestClass]
    public class CreateDatabaseCommandTest
    {
        
        Server.Server server;

        [TestInitialize]
        public void Initialize()
        {
            
        }

       

        [TestMethod]
        public void CreateSingleDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            var result =cli.SendCommand("CREATE DATABASE TEST1");
            Assert.AreEqual("CREATE DATABASE", result.CommandName);
            Assert.AreEqual( "TEST1", server.Databases.DbContext.Databases.Single(t => t.Name.ToUpper() == "TEST1").Name);
            
        }

        [TestMethod]
        public void CreateManyDatabasesCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");            
            cli.SendCommand("CREATE DATABASE TEST2");
            var result =cli.SendCommand("CREATE DATABASE TEST3");
            Assert.AreEqual("CREATE DATABASE", result.CommandName);
            Assert.AreEqual(3, server.Databases.DbContext.Databases.Count());
            
        }

        [TestMethod]
        public void CreateExistingDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);            
            cli.SendCommand("CREATE DATABASE TEST3");           
            var error = cli.SendCommand("CREATE DATABASE TEST3");
            Assert.AreEqual("CREATE DATABASE", error.CommandName);
            Assert.AreEqual("Database TEST3 already exists", error.ErrorMessage);
           
        }

        [TestMethod]
        public void CreateInvalidCharacterNamedDatabaseCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);            
            var error = cli.SendCommand("CREATE DATABASE TEST/4");
            Assert.AreEqual("CREATE DATABASE", error.CommandName);
            Assert.AreEqual("Invalid character '/' in database name", error.ErrorMessage);
           
        }
        [TestMethod]
        public void CreateDatabaseCommandInvalid()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);  
            var error = cli.SendCommand("CREATE DATABASE TEST NotAllowed");
            Assert.AreEqual("CREATE DATABASE", error.CommandName);
            Assert.AreEqual("Invalid 'CREATE DATABASE' Command:CREATE DATABASE TEST NotAllowed", error.ErrorMessage);
        }

        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();
            
           
        }
    }
}
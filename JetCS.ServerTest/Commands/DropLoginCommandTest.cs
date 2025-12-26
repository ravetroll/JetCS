using JetCS.Common;
using JetCS.Persistence;
using JetCS.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JetCS.ServerTest.Commands
{
    [TestClass]
    public class DropLoginCommandTest
    {
        
        Server.Server server;

        [TestInitialize]
        public void Initialize()
        {
            
        }

       

        [TestMethod]
        public void DropSingleLoginCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            var result =cli.SendCommand("CREATE LOGIN user1 password");
            Assert.AreEqual("user1", server.Databases.CreateDbContext().Logins.Single(t => t.LoginName.ToUpper() == "USER1").LoginName);
            result = cli.SendCommand("DROP LOGIN user1");
            Assert.AreEqual("DROP LOGIN", result.CommandName);
            Assert.AreEqual(0,server.Databases.CreateDbContext().Logins.Count(t => t.LoginName.ToUpper() == "USER1"));
            
        }

        [TestMethod]
        public void DropManyLoginsCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            var result = cli.SendCommand("CREATE LOGIN user1 password");
            result = cli.SendCommand("CREATE LOGIN user2 password");
            result = cli.SendCommand("CREATE LOGIN user3 password");
            result = cli.SendCommand("DROP LOGIN user2");
            result = cli.SendCommand("DROP LOGIN user3");
            Assert.AreEqual("DROP LOGIN", result.CommandName);
            Assert.AreEqual(2, server.Databases.CreateDbContext().Logins.Count());  // there is already the admin one.
            
        }

        [TestMethod]
        public void CreateNonExistingUserCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);            
            cli.SendCommand("CREATE LOGIN user3 pws");           
            var error = cli.SendCommand("DROP LOGIN user4");
            Assert.AreEqual("DROP LOGIN", error.CommandName);
            Assert.AreEqual("Login user4 not found", error.ErrorMessage);
           
        }

        
        [TestMethod]
        public void CreateLoginCommandInvalid()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);  
            var error = cli.SendCommand("DROP LOGIN TEST TEST");
            Assert.AreEqual("DROP LOGIN", error.CommandName);
            Assert.AreEqual("Invalid 'DROP LOGIN' Command:DROP LOGIN TEST TEST", error.ErrorMessage);
        }

        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();
            
           
        }
    }
}
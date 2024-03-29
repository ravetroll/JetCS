using JetCS.Common;
using JetCS.Persistence;
using JetCS.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JetCS.ServerTest.Commands
{
    [TestClass]
    public class CreateLoginCommandTest
    {
        
        Server.Server server;

        [TestInitialize]
        public void Initialize()
        {
            
        }

       

        [TestMethod]
        public void CreateSingleLoginCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            var result =cli.SendCommand("CREATE LOGIN user1 password");
            Assert.AreEqual("CREATE LOGIN", result.CommandName);
            Assert.AreEqual("user1",server.Databases.DbContext.Logins.Single(t => t.LoginName.ToUpper() == "USER1").LoginName);
            
        }

        [TestMethod]
        public void CreateManyLoginsCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            var result = cli.SendCommand("CREATE LOGIN user1 password");
            result = cli.SendCommand("CREATE LOGIN user2 password");
            result = cli.SendCommand("CREATE LOGIN user3 password");
            
            Assert.AreEqual("CREATE LOGIN", result.CommandName);
            Assert.AreEqual(4, server.Databases.DbContext.Logins.Count());  // there is already the admin one.
            
        }

        [TestMethod]
        public void CreateExistingUserCommand()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);            
            cli.SendCommand("CREATE LOGIN user3 pws");           
            var error = cli.SendCommand("CREATE LOGIN user3 pws");
            Assert.AreEqual("CREATE LOGIN", error.CommandName);
            Assert.AreEqual("Login 'user3' already exists", error.ErrorMessage);
           
        }

        
        [TestMethod]
        public void CreateLoginCommandInvalid()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);  
            var error = cli.SendCommand("CREATE LOGIN TEST");
            Assert.AreEqual("CREATE LOGIN", error.CommandName);
            Assert.AreEqual("Invalid 'CREATE LOGIN' Command:CREATE LOGIN TEST", error.ErrorMessage);
        }

        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();
            
           
        }
    }
}
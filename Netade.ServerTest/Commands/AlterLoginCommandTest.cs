using Netade.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public class AlterLoginCommandTest
    {
        Server.Server server;

        [TestInitialize]
        public void Initialize()
        {

        }
        [TestMethod]
        public void NonExistentLoginTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password ADMIN");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user", "password", server.CompressedMode);
            var result = cli.SendCommand("ALTER LOGIN user1 password1 ADMIN");
            Assert.AreEqual("ALTER LOGIN", result.CommandName);
            Assert.AreEqual("Invalid Login Name user", result.ErrorMessage);
        }

        [TestMethod]
        public void IncorrectLoginPasswordTest()
        {
            CommandResult result;   
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            result= cli.SendCommand("CREATE DATABASE TEST1");
            result=cli.SendCommand("CREATE LOGIN USER1 password ADMIN");
            result =cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password1", server.CompressedMode);
            result = cli.SendCommand("ALTER LOGIN user1 password1 ADMIN");
            Assert.AreEqual("ALTER LOGIN", result.CommandName);
            Assert.AreEqual("Invalid Password", result.ErrorMessage);
        }

        [TestMethod]
        public void CorrectLoginPasswordTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN uSER1 password ADMIN");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            var result = cli.SendCommand("ALTER LOGIN admin password1 ADMIN");
            Assert.AreEqual("ALTER LOGIN", result.CommandName);
            Assert.AreEqual(1, result.RecordCount);
        }

        [TestMethod]
        public void CannotRemoveOwnAdminStatus()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN uSER1 password ADMIN");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            var alter =cli.SendCommand("ALTER LOGIN user1 password");
            Assert.AreEqual("ALTER LOGIN", alter.CommandName);
            var result = cli.SendCommand("CREATE DATABASE TEST2");            
            Assert.AreEqual(1, result.RecordCount);
        }
        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();


        }
    }
}

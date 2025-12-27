using EntityFrameworkCore.Jet.Scaffolding.Internal;
using Netade.Common.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.ServerTest.Commands
{
    [TestClass]
    public class GrantDatabaseCommandTest
    {
        Server.Server server;

        [TestInitialize]
        public void Initialize()
        {

        }

        [TestMethod]
        public void SelectValueTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1","USER1","password", server.CompressedMode);
            var result = cli.SendCommand("SELECT 10,20");
            Assert.AreEqual("Permission denied on test1", result.ErrorMessage);
            cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("GRANT DATABASE TEST1 USER1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "USER1", "password", server.CompressedMode);
            result = cli.SendCommand("SELECT 10,20");

            Assert.AreEqual(null, result.ErrorMessage);



        }

        [TestMethod]
        public void NonExistentDatabaseTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");            
            cli = ServerSetup.BuildNetadeClient("TEST2", "127.0.0.1", server.CompressedMode);
            var result = cli.SendCommand("GRANT DATABASE TEST2 USER1");
            Assert.AreEqual("GRANT DATABASE", result.CommandName);
            Assert.AreEqual("Invalid Database 'TEST2'", result.ErrorMessage);
        }

        [TestMethod]
        public void NonExistentLoginTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1","user","password", server.CompressedMode);
            var result = cli.SendCommand("GRANT DATABASE TEST1 USER2");
            Assert.AreEqual("GRANT DATABASE", result.CommandName);
            Assert.AreEqual("Invalid Login Name user", result.ErrorMessage);
        }

       

        [TestMethod]
        public void CorrectLoginPasswordTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            var result = cli.SendCommand("GRANT DATABASE TEST1 USER1");
            Assert.AreEqual("GRANT DATABASE", result.CommandName);
            Assert.AreEqual(1,server.Databases.CreateDbContext().DatabaseLogins.Count(t=>t.Database.Name == "TEST1" && t.Login.LoginName == "USER1"));
        }

        

       




        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();


        }
    }
}

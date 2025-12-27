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
    public class SelectCommandTest
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
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", server.CompressedMode);
            var result = cli.SendCommand("SELECT 10,20");
            Assert.AreEqual("SELECT", result.CommandName);
            Assert.AreEqual(result.Result.Rows[0].ItemArray[0], 10);
        }

        [TestMethod]
        public void NonExistentDatabaseTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");            
            cli = ServerSetup.BuildNetadeClient("TEST2", "127.0.0.1", server.CompressedMode);
            var result = cli.SendCommand("SELECT 10,20");
            Assert.AreEqual("SELECT", result.CommandName);
            Assert.AreEqual("Database test2 does not exist", result.ErrorMessage);
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
            var result = cli.SendCommand("SELECT 10,20");
            Assert.AreEqual("SELECT", result.CommandName);
            Assert.AreEqual("Invalid Login Name user", result.ErrorMessage);
        }

        [TestMethod]
        public void IncorrectLoginPasswordTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password1", server.CompressedMode);
            var result = cli.SendCommand("SELECT 10,20");
            Assert.AreEqual("SELECT", result.CommandName);
            Assert.AreEqual("Invalid Password", result.ErrorMessage);
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
            var result = cli.SendCommand("SELECT 10,20");
            Assert.AreEqual("SELECT", result.CommandName);
            Assert.AreEqual(result.Result.Rows[0].ItemArray[0], 10);
        }

        [TestMethod]
        public void SelectASingleRow()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar, field2 int)");
            cli.SendCommand("INSERT INTO Table1 VALUES ('value1', 40)");
            var result = cli.SendCommand("SELECT *, Now() AS timenow FROM Table1");
            Assert.AreEqual("SELECT", result.CommandName);
            Assert.AreEqual(1,result.Result.Rows.Count);
        }

        [TestMethod]
        public void SelectNoRows()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar, field2 int)");            
            var result = cli.SendCommand("SELECT * FROM Table1");
            Assert.AreEqual("SELECT", result.CommandName);
            Assert.AreEqual(0, result.Result.Rows.Count);
        }





        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();


        }
    }
}

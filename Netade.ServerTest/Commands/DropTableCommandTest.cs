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
    public class DropTableCommandTest
    {
        Server.Server server;

        [TestInitialize]
        public void Initialize()
        {

        }

        

        [TestMethod]
        public void NonExistentDatabaseTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildNetadeClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");            
            cli = ServerSetup.BuildNetadeClient("TEST2", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar, field2 int)");
            cli.SendCommand("INSERT INTO Table1 VALUES ('value1', 40)");
            var result = cli.SendCommand("DROP TABLE Table1");
            Assert.AreEqual("DROP TABLE", result.CommandName);
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
            cli = ServerSetup.BuildNetadeClient("TEST1", "127.0.0.1", "user", "password", server.CompressedMode);
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar, field2 int)");
            cli.SendCommand("INSERT INTO Table1 VALUES ('value1', 40)");
            var result = cli.SendCommand("DROP TABLE Table1");
            Assert.AreEqual("DROP TABLE", result.CommandName);
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
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar, field2 int)");
            cli.SendCommand("INSERT INTO Table1 VALUES ('value1', 40)");
            var result = cli.SendCommand("DROP Table table1");
            Assert.AreEqual("DROP TABLE", result.CommandName);
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
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar, field2 int)");
            cli.SendCommand("INSERT INTO Table1 VALUES ('value1', 40)");            
            var result = cli.SendCommand("CREATE PROCEDURE Test AS DELETE * FROM Table1");
            result = cli.SendCommand("DROP table table1");
            Assert.AreEqual("DROP TABLE", result.CommandName);
            Assert.AreEqual(null, result.ErrorMessage);
        }

        





        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();


        }
    }
}

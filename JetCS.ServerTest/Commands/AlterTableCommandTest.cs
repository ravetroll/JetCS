using EntityFrameworkCore.Jet.Scaffolding.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.ServerTest.Commands
{
    [TestClass]
    public class AlterTableCommandTest
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
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");            
            cli = ServerSetup.BuildJetCSClient("TEST2", "127.0.0.1", server.CompressedMode);
            var result = cli.SendCommand("ALTER TABLE Table1 ADD COLUMN field1 varchar");
            Assert.AreEqual("ALTER TABLE", result.CommandName);
            Assert.AreEqual("Database test2 does not exist", result.ErrorMessage);
        }

        [TestMethod]
        public void NonExistentLoginTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildJetCSClient("TEST1", "127.0.0.1","user","password", server.CompressedMode);
            var result = cli.SendCommand("ALTER TABLE Table1 ADD COLUMN field1 varchar");
            Assert.AreEqual("ALTER TABLE", result.CommandName);
            Assert.AreEqual("Invalid Login Name user", result.ErrorMessage);
        }

        [TestMethod]
        public void IncorrectLoginPasswordTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildJetCSClient("TEST1", "127.0.0.1", "user1", "password1", server.CompressedMode);
            var result = cli.SendCommand("ALTER TABLE Table1 ADD COLUMN field1 varchar");
            Assert.AreEqual("ALTER TABLE", result.CommandName);
            Assert.AreEqual("Invalid Password", result.ErrorMessage);
        }

        [TestMethod]
        public void CorrectLoginPasswordTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildJetCSClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar)");
            var result = cli.SendCommand("ALTER TABLE Table1 ADD COLUMN field2 varchar");
            Assert.AreEqual("ALTER TABLE", result.CommandName);
            Assert.AreEqual(null, result.ErrorMessage);
        }

        [TestMethod]
        public void ColumnAddedTest()
        {
            server = ServerSetup.BuildAndStartServer();
            var cli = ServerSetup.BuildJetCSClient("db", "127.0.0.1", server.CompressedMode);
            cli.SendCommand("CREATE DATABASE TEST1");
            cli.SendCommand("CREATE LOGIN USER1 password");
            cli.SendCommand("GRANT DATABASE test1 user1");
            cli = ServerSetup.BuildJetCSClient("TEST1", "127.0.0.1", "user1", "password", server.CompressedMode);
            cli.SendCommand("CREATE TABLE Table1 (field1 varchar)");
            var result = cli.SendCommand("ALTER TABLE Table1 ADD COLUMN field2 varchar");
            Assert.AreEqual("ALTER TABLE", result.CommandName);
            cli.SendCommand("INSERT INTO Table1 VALUES ('value1', 'value2')");
            result = cli.SendCommand("SELECT * FROM Table1");
            
            Assert.AreEqual(2, result.Result.Columns.Count);
        }



        [TestCleanup]
        public void Cleanup()
        {
            server.Stop();


        }
    }
}

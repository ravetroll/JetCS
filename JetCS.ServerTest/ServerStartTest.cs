using JetCS.Common;
using JetCS.Persistence;
using JetCS.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JetCS.ServerTest
{
    [TestClass]
    public class ServerStartTest
    {

        JetCS.Server.Server server;

        [TestInitialize]
        public void Initialize()
        {
            
            
           
        }

        [TestMethod]
        public void StartStop()
        {
            server = ServerSetup.BuildServer();
            server.Start();
            Assert.IsTrue(server.IsRunning);
            server.Stop();
            Assert.IsFalse(server.IsRunning);

        }

        [TestMethod]
        public void InvalidCommand()
        {
            var server = ServerSetup.BuildAndStartServer();            
            ConnectionStringBuilder connStrBuild = new ConnectionStringBuilder("db", "127.0.0.1");
            string connStr = connStrBuild.ToString();
            var cli = new JetCSClient(connStr, server.CompressedMode);
            var result = cli.SendCommand("INVALID COMMAND");
            Assert.IsTrue(result.ErrorMessage.StartsWith("Unrecognised Command"));
            server.Stop();

        }

        [TestCleanup]
        public void Cleanup()
        {
            
            if (server != null) server.Stop();


        }
    }
}
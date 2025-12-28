using Netade.Common;
using Netade.Persistence;
using Netade.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Netade.ServerTest
{
    [TestClass]
    public class ServerStartTest
    {

        Netade.Server.Server server;

        [TestInitialize]
        public void Initialize()
        {
            
            
           
        }

        [TestMethod]
        public void StartStop()
        {
            var host = new TestHostControl();
            server = ServerSetup.BuildServer();
            server.Start(host);
            Assert.IsTrue(server.IsRunning);
            Assert.AreEqual(0, host.StopCalls);
            server.Stop();
            Assert.IsFalse(server.IsRunning);
           

        }

        [TestMethod]
        public void InvalidCommand()
        {
            var server = ServerSetup.BuildAndStartServer();            
            ConnectionStringBuilder connStrBuild = new ConnectionStringBuilder("db", "127.0.0.1");
            string connStr = connStrBuild.ToString();
            var cli = new NetadeClient(connStr, server.CompressedMode);
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
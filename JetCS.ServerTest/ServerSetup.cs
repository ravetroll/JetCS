using JetCS.Common;
using JetCS.Common.Serialization;
using JetCS.Domain;
using JetCS.Persistence;
using JetCS.Server;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.ServerTest
{
    public static class ServerSetup
    {
       
        public static JetCS.Server.Server BuildServer()
        {


            Config config;
            IServiceProvider serviceProvider;

            config = new Config()
            {
                DatabasePath = Directory.GetCurrentDirectory() + "\\Data",
                ListenPort = 1549,
                Provider = "Microsoft.ACE.OLEDB.16.0",
                CompressedMode = false,
            };
            // Deletes All Databases
            ClearData(config.DatabasePath);
            // Creates DI container
            serviceProvider = new ServiceCollection()
                .AddOptions()
                .AddScoped(sp => { return config; })
                .Configure<Config>(t => t = config)
                .AddDbContext<JetCSDbContext>(options => options.UseJetOleDb($"Provider={config.Provider}; Data Source={Directory.GetCurrentDirectory()}\\JetCS.mdb;"))
                .AddScoped<Databases>()
                .AddScoped<CommandDispatcher>()
                .AddScoped<SeedData>()
                .BuildServiceProvider();
            JetCS.Server.Server server = new(config, serviceProvider);
            server.Reset();
            return server;
        }

        public static JetCS.Server.Server BuildAndStartServer()
        {
            var server = BuildServer();
            server.Start();
            return server;
        }

        private static void ClearData(string path)
        {
            System.IO.DirectoryInfo di = new DirectoryInfo(path);
            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }

        public static JetCSClient BuildJetCSClient(string database, string server, bool compressedMode)
        {
            ConnectionStringBuilder connStrBuild = new ConnectionStringBuilder(database, server);
            string connStr = connStrBuild.ToString();
            var cli = new JetCSClient(connStr, compressedMode);
            return cli;
        }

        public static JetCSClient BuildJetCSClient(string database, string server,string login,string password, bool compressedMode)
        {
            ConnectionStringBuilder connStrBuild = new ConnectionStringBuilder(database, server,login,password);
            string connStr = connStrBuild.ToString();
            var cli = new JetCSClient(connStr, compressedMode);
            return cli;
        }
    }
}

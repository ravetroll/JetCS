using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Netade.Common;
using Netade.Common.Serialization;
using Netade.Domain;
using Netade.Persistence;
using Netade.Server;
using Netade.Server.Internal.Extensions;
using Netade.Server.Services;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.ServerTest
{
    public static class ServerSetup
    {
       
        public static Netade.Server.Server BuildServer()
        {
            string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            var builder = new ConfigurationBuilder()
                    .SetBasePath(baseDir)
                    .AddJsonFile("config.test.json", optional: false);

            IConfiguration configRoot = builder.Build();

            Config config = configRoot.GetSection("Config").Get<Config>() ?? new Config()
            {
                DatabasePath = baseDir + "\\Data"
            };

           
            IServiceProvider serviceProvider;
            
            // Deletes All Databases
            ClearData(config.DatabasePath);
            // Creates DI container
            serviceProvider = new ServiceCollection()
                .AddOptions()
                .AddCommands()
                .AddScoped(sp => { return config; })
                .Configure<Config>(t => t = config)
                .AddDbContextFactory<NetadeDbContext>(options => options.UseJetOleDb($"Provider={config.Provider}; Data Source={baseDir}\\Netade.mdb;"))
                .AddSingleton<Databases>()
                .AddScoped<CommandDispatcher>()
                .AddScoped<SeedData>()
                .AddSingleton<Netade.Server.Server>()
                .AddSingleton<CommandFactory>()
                .AddSingleton<ProviderDetectionService>()
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddSerilog(new Serilog.LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console()
                        .CreateLogger());
                })
                .BuildServiceProvider();
            Netade.Server.Server server = serviceProvider.GetRequiredService<Netade.Server.Server>();
            server.Reset();
            return server;
        }

        public static Netade.Server.Server BuildAndStartServer()
        {
            var server = BuildServer();
            var host = new TestHostControl();
            server.Start(host);
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

        public static NetadeClient BuildNetadeClient(string database, string server, bool compressedMode)
        {
            ConnectionStringBuilder connStrBuild = new ConnectionStringBuilder(database, server);
            string connStr = connStrBuild.ToString();
            var cli = new NetadeClient(connStr, compressedMode);
            return cli;
        }

        public static NetadeClient BuildNetadeClient(string database, string server,string login,string password, bool compressedMode)
        {
            ConnectionStringBuilder connStrBuild = new ConnectionStringBuilder(database, server,login,password);
            string connStr = connStrBuild.ToString();
            var cli = new NetadeClient(connStr, compressedMode);
            return cli;
        }
    }
}

using JetCS.Common;
using JetCS.Common.Serialization;
using JetCS.Domain;
using JetCS.Persistence;
using JetCS.Server;
using JetCS.Server.Internal.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
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
                .AddDbContextFactory<JetCSDbContext>(options => options.UseJetOleDb($"Provider={config.Provider}; Data Source={baseDir}\\JetCS.mdb;"))
                .AddSingleton<Databases>()
                .AddScoped<CommandDispatcher>()
                .AddScoped<SeedData>()
                .AddSingleton<JetCS.Server.Server>()
                .AddSingleton<CommandFactory>()
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddSerilog(new Serilog.LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console()
                        .CreateLogger());
                })
                .BuildServiceProvider();
            JetCS.Server.Server server = serviceProvider.GetRequiredService<JetCS.Server.Server>();
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

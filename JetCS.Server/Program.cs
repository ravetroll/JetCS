// See https://aka.ms/new-console-template for more information
using JetCS.Server;
using Topshelf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JetCS.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using System.Configuration;
using Topshelf.Configurators;
using JetCS.Server.Internal.Database;
using JetCS.Common.Serialization;
using JetCS.Server.Internal.Extensions;





Config config = null;
ServiceProvider serviceProvider = null;
Microsoft.Extensions.Logging.ILogger logger = null;
Serilog.ILogger serilogLogger = null;
TopshelfExitCode rc = TopshelfExitCode.Ok;
try
{
    // NB that directory my be obtained via Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)
    // Not Directory.GetCurrentDirectory()
    // Topshelf does not work if using that.

    string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
    
    // Get Config

    var builder = new ConfigurationBuilder()
                    .SetBasePath(baseDir)
                    .AddJsonFile("config.json", optional: false);
   
    IConfiguration configRoot = builder.Build();   

    config = configRoot.GetSection("Config").Get<Config>() ?? new Config()
    {
        DatabasePath = baseDir + "\\Data"        
    };

    // Get Logger 

    serilogLogger = new LoggerConfiguration().ReadFrom.Configuration(configRoot).MinimumLevel.Information().CreateLogger();


    //  Build DI
    serviceProvider = new ServiceCollection()
        .AddCommands()
        .AddOptions()
        .AddSingleton<Config>(sp => { return config; })
        .AddDbContextFactory<JetCSDbContext>(options => options.UseJetOleDb($"Provider={config.Provider}; Data Source={baseDir}\\JetCS.mdb;"))
        .AddSingleton<Databases>()
        .AddSingleton<CommandDispatcher>()
        .AddSingleton<SeedData>()
        .AddSingleton<Server>()        
        .AddSingleton<CommandFactory>()
        .AddLogging(loggingBuilder => 
        { 
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(serilogLogger, dispose: true);
        })
        .BuildServiceProvider(true);


    logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
    logger.LogInformation("Service Provider built");

    // Detect if the OleDB Provider is valid and try some alernatives if not
    var changedProvider = ProviderDetection.ApplyValidProvider(ref config);
    if (changedProvider != null)
        logger.LogWarning(changedProvider);


    // Start Service
    rc = HostFactory.Run(hostConfig =>
    {
        
        hostConfig.OnException(ex =>
        {
            Console.WriteLine("hostConfig Exception");
        });
        hostConfig.UseSerilog(serilogLogger);
        hostConfig.SetStartTimeout(TimeSpan.FromSeconds(10));
        hostConfig.SetStopTimeout(TimeSpan.FromSeconds(10));
        hostConfig.UseAssemblyInfoForServiceInfo();
        hostConfig.Service<Server>(serviceConfig =>
        {

            serviceConfig.ConstructUsing(() => serviceProvider.GetRequiredService<Server>());
            
            serviceConfig.WhenStarted(server => server.Start());
            serviceConfig.WhenStopped(server => server.Stop());

        });

        hostConfig.RunAsLocalSystem();        
        hostConfig.SetDisplayName("JetCS Database Server");
        hostConfig.SetDescription("JetCS Database Server");
       
        
    });
    logger.LogInformation($"Service Ended with exit code {(int)rc}");



}
catch(Exception ex)
{
    if (logger != null)
    logger.LogError(ex, "Error in Program");
    
}
Environment.ExitCode = (int)rc;

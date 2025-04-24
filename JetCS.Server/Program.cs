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





Config config = null;
ServiceProvider serviceProvider = null;
Serilog.ILogger logger = null;
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

    logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configRoot)
        .CreateLogger();

    // Detect if the OleDB Provider is valid and try some alernatives if not
    var changedProvider = ProviderDetection.ApplyValidProvider(ref config);
    if (changedProvider != null)
        logger.Warning(changedProvider);


    //  Build DI
    serviceProvider = new ServiceCollection()
        .AddOptions()
        .AddSingleton<Config>(sp => { return config; })        
        .AddDbContext<JetCSDbContext>(options => options.UseJetOleDb($"Provider={config.Provider}; Data Source={Directory.GetCurrentDirectory()}\\JetCS.mdb;"))
        .AddScoped<Databases>()
        .AddScoped<CommandDispatcher>()
        .AddScoped<SeedData>()
        .AddSingleton<Server>()
        .AddSerilog(logger)
        .BuildServiceProvider();
    
    // Start Servicel
    rc = HostFactory.Run(hostConfig =>
    {
        
        hostConfig.OnException(ex =>
        {
            Console.WriteLine("hostConfig Exception");
        });
        hostConfig.UseSerilog(logger);
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




}
catch(Exception ex)
{
    if (logger != null)
    logger.Error(ex, "Error in Program");
    
}

var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
//logger.Information(exitCode.ToString());
Environment.ExitCode = exitCode;

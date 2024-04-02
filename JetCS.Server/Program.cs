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



Config config = null;
ServiceProvider serviceProvider = null;
//Serilog.Core.Logger logger = null;
TopshelfExitCode rc = TopshelfExitCode.Ok;
try
{

    // NB that directory my be obtained via Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)
    // Not Directory.GetCurrentDirectory()
    // Topshelf does not work if using that.
    var builder = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location ))
                    .AddJsonFile("config.json", optional: false);
   
        IConfiguration configRoot = builder.Build();
        Console.WriteLine(String.Join(" ",configRoot.AsEnumerable().Select(t=>t.Key + ";" + t.Value?.ToString())));
    

        Serilog.ILogger configuration = new LoggerConfiguration()
           .ReadFrom.Configuration(configRoot)
           .CreateLogger();

    

        config = configRoot.GetSection("Config").Get<Config>() ?? new Config()
        {
            DatabasePath = Directory.GetCurrentDirectory() + "\\Data"
        };

    

        serviceProvider = new ServiceCollection()
            .AddOptions()
            .AddScoped<Config>(sp => { return config; })
            .Configure<Config>(t => configRoot.GetSection("Config").Get<Config>())
            .AddDbContext<JetCSDbContext>(options => options.UseJetOleDb($"Provider={config.Provider}; Data Source={Directory.GetCurrentDirectory()}\\JetCS.mdb;"))
            .AddScoped<Databases>()
            .AddScoped<CommandDispatcher>()
            .AddScoped<SeedData>()            
            .AddSerilog(configuration)
            .BuildServiceProvider();
    
   
    rc = HostFactory.Run(hostConfig =>
    {
        
        hostConfig.OnException(ex =>
        {
            Console.WriteLine("hostConfig Exception");
        });
        hostConfig.UseSerilog(configuration);
        hostConfig.SetStartTimeout(TimeSpan.FromSeconds(10));
        hostConfig.SetStopTimeout(TimeSpan.FromSeconds(10));
        hostConfig.UseAssemblyInfoForServiceInfo();
        hostConfig.Service<Server>(serviceConfig =>
        {

            serviceConfig.ConstructUsing(() => new Server(config, serviceProvider));
            
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
    //logger.Error(ex, "Error in Program");
    Console.WriteLine(ex.Message);
}







var exitCode = (int)Convert.ChangeType(rc, rc.GetTypeCode());
//logger.Information(exitCode.ToString());
Environment.ExitCode = exitCode;

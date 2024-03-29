// See https://aka.ms/new-console-template for more information
using JetCS.Server;
using Topshelf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using JetCS.Persistence;
using Microsoft.EntityFrameworkCore;
using static System.Runtime.InteropServices.JavaScript.JSType;

Config config = null;
ServiceProvider serviceProvider = null;
try
{
    var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", optional: false);

    IConfiguration configRoot = builder.Build();

    config = configRoot.GetSection("Config").Get<Config>() ?? new Config()
    {
        DatabasePath = Directory.GetCurrentDirectory() + "\\Data"
    };
    serviceProvider = new ServiceCollection()
        .AddOptions()
        .AddScoped<Config>(sp => { return config; })
        .Configure<Config>(t=> configRoot.GetSection("Config").Get<Config>())        
        .AddDbContext<JetCSDbContext>(options => options.UseJetOleDb($"Provider={config.Provider}; Data Source={Directory.GetCurrentDirectory()}\\JetCS.mdb;"))
        .AddScoped<Databases>()
        .AddScoped<CommandDispatcher>()
        .AddScoped<SeedData>()
        .BuildServiceProvider();
}
catch(Exception ex)
{
    Console.WriteLine("Failed to get Config:" + ex.ToString());
}





HostFactory.Run(hostConfig =>
{

 
    

    
    hostConfig.Service<Server>(serviceConfig =>
    {
        serviceConfig.ConstructUsing(() => new Server(config,serviceProvider));
        serviceConfig.WhenStarted(server => server.Start());
        serviceConfig.WhenStopped(server => server.Stop());
        
    });

    hostConfig.RunAsLocalSystem();
    hostConfig.StartAutomatically();
    hostConfig.SetServiceName("JetCSServer");
    hostConfig.SetDisplayName("JetCS Database Server");
    hostConfig.SetDescription("JetCS Database Server");
});

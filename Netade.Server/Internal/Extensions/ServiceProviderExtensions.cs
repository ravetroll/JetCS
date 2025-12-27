using Netade.Server.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Internal.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static IServiceCollection AddCommands(this IServiceCollection services)
        {
            var interfaceType = typeof(ICommand);

            var implementations = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t => interfaceType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });

            foreach (var impl in implementations)
            {
                services.Add(new ServiceDescriptor(impl, impl, ServiceLifetime.Transient));
                
            }
            //foreach (var service in services)
            //{
            //    Console.WriteLine($"{service.ServiceType} => {service.ImplementationType}");
            //}
            return services;
        }
    }
}

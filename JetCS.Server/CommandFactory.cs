using JetCS.Server.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Server
{
    public class CommandFactory
    {
        private readonly IServiceProvider _provider;

        public CommandFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public object Create(Type type)
        {
            return _provider.GetRequiredService(type);
            
        }

       
    }
}

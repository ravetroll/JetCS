using JetCS.Common.Messaging;
using JetCS.Persistence;
using JetCS.Server.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JetCS.Server
{
    public class CommandDispatcher
    {
        
        private readonly Dictionary<string[],ICommand> commands = new Dictionary<string[], ICommand>();
        private readonly Databases dbs;
        private readonly CommandFactory fact;

        //private static ReaderWriterLock rwl = new ReaderWriterLock();

        public CommandDispatcher(Databases databases, CommandFactory fact)
        {
            dbs = databases;
            this.fact = fact;
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(ICommand).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                {
                    // Use Activator.CreateInstance with null check to avoid CS8600

                    if (fact.Create(type) is ICommand t)
                    {
                        commands.Add(t.Identifiers, t);
                    }


                }
            }

        }


        public async Task<CommandResult> DispatchAsync(Command command) {
            
            var matchingCommand = commands.FirstOrDefault(t => t.Key.Select(r => new Regex(r).IsMatch(command.CommandText.ToUpper())).Any(s => s == true)).Value;

            if (matchingCommand != null)
            {
                CommandResult? result = null;
                
                result = await matchingCommand.ExecuteAsync(command);
                
                return result;

            } 
            else
            {
                return new CommandResult() { ErrorMessage = "Unrecognised Command.  These Commands are recognised: " + String.Join(", ", commands.Select(t => t.Key)) };
            }
            
        }

        


    }
}

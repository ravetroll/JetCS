using JetCS.Common.Messaging;
using JetCS.Persistence;
using JetCS.Server.Commands;
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

        public CommandDispatcher() {
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(ICommand).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                {
                    ICommand t = (ICommand)Activator.CreateInstance(type);                   
                    commands.Add(t.Identifiers, t);
                }
            }

           
        }


        public CommandResult Dispatch(Command command, Databases databases) {
            
            var matchingCommand = commands.FirstOrDefault(t => t.Key.Select(r => new Regex(r).IsMatch(command.CommandText.ToUpper())).Any(s => s == true)).Value;

            if (matchingCommand != null)
            {
                return matchingCommand.Execute(command, databases);
            } 
            else
            {
                return new CommandResult() { ErrorMessage = "Unrecognised Command.  These Commands are recognised: " + String.Join(", ", commands.Select(t => t.Key)) };
            }
            
        }

        


    }
}

using JetCS.Common.Messaging;
using JetCS.Persistence;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Server.Commands
{
    public class SelectCommand : CommandBase, ICommand
    {

        CommandDispatcher dsp;
      
        public string Name => "SELECT";

        public string Description => $"SQL {Name} Statement";

       
        public string[] Identifiers => [$"{Name} "];

        public CommandResult Execute(Command cmd, Databases databases)
        {
            return ExecuteQueryResult(Name, cmd, databases);
        }
    }
}

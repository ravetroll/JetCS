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
    public class SelectIntoCommand : CommandBase, ICommand
    {
        public SelectIntoCommand(Databases dbs) : base(dbs)
        {
        }
        public string Name => "SELECT INTO";

        public string Description => $"SQL {Name} Statement";

        
        public string[] Identifiers => ["\\bSELECT\\s+.*\\s+INTO\\s+[\\w\\d]+\\s*"];
        public async Task<CommandResult> ExecuteAsync(Command cmd)
        {
            return await ExecuteNonQueryResultAsync(Name, cmd);
        }
    }
}

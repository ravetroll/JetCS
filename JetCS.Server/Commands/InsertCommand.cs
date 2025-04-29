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
    public class InsertCommand : CommandBase, ICommand
    {
        public InsertCommand(Databases dbs) : base(dbs)
        {
        }
        public string Name => "INSERT";

        public string Description => $"SQL {Name} Statement";

       
        public string[] Identifiers => [$"^{Name}"];
        public async Task<CommandResult> ExecuteAsync(Command cmd)
        {
            return await ExecuteNonQueryResultAsync(Name, cmd);
        }
    }
}

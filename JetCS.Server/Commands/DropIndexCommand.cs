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
    public class DropIndexCommand : CommandBase, ICommand
    {
        public DropIndexCommand(Databases databases) : base(databases)
        {
        }
        public string Name => "DROP INDEX";

        public string Description => $"SQL {Name} Statement";

       
        public string[] Identifiers => [$"^{Name}"];
        public async Task<CommandResult> ExecuteAsync(Command cmd)
        {
            return await ExecuteNonQueryResultAsync(Name, cmd);
        }
    }
}

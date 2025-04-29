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
    public class CreateIndexCommand : CommandBase, ICommand
    {
        public CreateIndexCommand(Databases databases) : base(databases)
        {
        }
        public string Name => "CREATE INDEX";

        public string Description => $"SQL {Name} Statement";

        
        public string[] Identifiers => [$"^{Name}", "^CREATE UNIQUE INDEX"];
        public async Task<CommandResult> ExecuteAsync(Command cmd)
        {
            return await ExecuteNonQueryResultAsync(Name, cmd);
        }
    }
}

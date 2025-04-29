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
    public class CreateProcedureCommand : CommandBase, ICommand
    {
        public CreateProcedureCommand(Databases databases) : base(databases)
        {
        }
        public string Name => "CREATE PROCEDURE";

        public string Description => $"SQL {Name} Statement";

        
        public string[] Identifiers => [$"^{Name}","^PROCEDURE"];
        public async Task<CommandResult> ExecuteAsync(Command cmd)
        {
            return await ExecuteNonQueryResultAsync(Name, cmd);
        }
    }
}

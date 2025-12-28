using Netade.Common.Messaging;
using Netade.Persistence;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Commands
{
    public class DropTableCommand : CommandBase, ICommand
    {
        public DropTableCommand(Databases databases) : base(databases)
        {
        }
        public string Name => "DROP TABLE";

        public string Description => $"SQL {Name} Statement";

        
        public string[] Identifiers => [$"^{Name}"];
        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {
            return await ExecuteNonQueryResultAsync(Name, cmd, cancellationToken);
        }
    }
}

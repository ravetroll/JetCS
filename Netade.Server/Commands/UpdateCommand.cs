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
    public class UpdateCommand : CommandBase, ICommand
    {
        public UpdateCommand(Databases dbs) : base(dbs)
        {
        }
        public string Name => "UPDATE";

        public string Description => $"SQL {Name} Statement";

        
        public string[] Identifiers => [$"^{Name}"];
        public async Task<CommandResult> ExecuteAsync(Command cmd)
        {
            return await ExecuteNonQueryResultAsync(Name, cmd);
        }
    }
}

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
    public class SelectCommand : CommandBase, ICommand
    {

        public SelectCommand(Databases dbs) : base(dbs)
        {
        }
        public override bool DataChange => false;

        public string Name => "SELECT";

        public string Description => $"SQL {Name} Statement";

       
        public string[] Identifiers => ["^(?!.*INTO).*SELECT.*$"];

        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {
            return await ExecuteQueryResultAsync(Name, cmd, cancellationToken);
        }
    }
}

using Netade.Common.Messaging;
using Netade.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Commands
{
    public interface ICommand
    {
        string Name { get; }
        string Description { get; }        
        string[] Identifiers { get; }
        bool DataChange { get; } 
        Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken);

        
     
    }
}

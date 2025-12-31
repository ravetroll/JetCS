using EntityFrameworkCore.Jet.Data;
using Netade.Common.Messaging;
using Netade.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Netade.Domain;
using Netade.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Netade.Server.Commands
{
    public class DropLoginCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public DropLoginCommand(Databases databases):base(databases)
        {
            this.databases = databases;
        }
        public string Name => "DROP LOGIN";

        public string Description => $"Netade {Name} Statement";

        
        public string[] Identifiers => [$"^{Name}"];
        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {
            
            CommandResult commandResult = new(Name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");
            }

            //  Authentication and Authorization
            var auth = await databases.LoginWithoutDatabaseAsync(csb.Login, csb.Password, cancellationToken);
            if (!auth.Authenticated)
            {
                return commandResult.SetErrorMessage(auth.StatusMessage);
            }

            if (!auth.IsAdmin)
            {
                return commandResult.SetErrorMessage($"This command required admin priviledges");
            }
            string[] commandString = cmd.CommandText.Split(" ");
            if (commandString.Length != 3)
            {
                return commandResult.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");
            }

            try
            {
                await using var _ = await databases.EnterSystemWriteAsync(cancellationToken).ConfigureAwait(false);
                var dbcontext = databases.CreateDbContext();
                Login? login = await dbcontext.Logins.FirstOrDefaultAsync(t => t.LoginName.ToLower() == commandString[2].ToLower());
                if (login != null)
                {
                    dbcontext.Logins.Remove(login);
                    await dbcontext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    commandResult.ErrorMessage = $"Login {commandString[2]} not found";
                }
            }
            finally
            {
                
            }
            
           
               
               
            return commandResult;

        }


    }
}

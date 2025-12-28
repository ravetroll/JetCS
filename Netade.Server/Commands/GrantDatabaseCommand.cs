using Netade.Common.Messaging;
using Netade.Persistence;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using Netade.Common;
using System.Net;
using System.Xml.Linq;
using System.Diagnostics.Eventing.Reader;
using Netade.Domain;
using Microsoft.EntityFrameworkCore;

namespace Netade.Server.Commands
{
    public class GrantDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public GrantDatabaseCommand(Databases databases): base(databases)
        {
            this.databases = databases;
        }
       
        public string Name => "GRANT DATABASE";

        public string Description => $"Netade {Name} Statement; ";

        
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
            if (commandString.Length != 4)
            {
                return commandResult.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");
            }
            try
            {
                await using var _ = await databases.EnterWriteAsync("", cancellationToken).ConfigureAwait(false);
                var dbcontext = databases.CreateDbContext();
                var databaseAlreadyAssigned = await dbcontext.DatabaseLogins.FirstOrDefaultAsync(t => t.Database.Name.ToUpper() == commandString[2].ToUpper() && t.Login.LoginName.ToUpper() == commandString[3].ToUpper());
                if (databaseAlreadyAssigned == null)
                {
                    Database? dbAssign = await dbcontext.Databases.FirstOrDefaultAsync(t => t.Name.ToUpper() == commandString[2].ToUpper());
                    Login? loginAssign = await dbcontext.Logins.FirstOrDefaultAsync(t => t.LoginName.ToUpper() == commandString[3].ToUpper());
                    if (loginAssign == null)
                    {
                        commandResult.ErrorMessage = $"Invalid Login '{commandString[3]}'";
                    }
                    if (dbAssign == null)
                    {
                        commandResult.ErrorMessage = $"Invalid Database '{commandString[2]}'";
                    }
                    if (loginAssign != null && dbAssign != null)
                    {
                        DatabaseLogin dbl = new DatabaseLogin() { Database = dbAssign, Login = loginAssign };
                        dbcontext.DatabaseLogins.Add(dbl);
                        await dbcontext.SaveChangesAsync();
                        commandResult.RecordCount = 1;
                    }

                }
            }
            finally
            {
                
            }
               
            return commandResult;

        }

        
    }
}

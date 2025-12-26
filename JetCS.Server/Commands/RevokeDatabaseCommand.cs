using JetCS.Common.Messaging;
using JetCS.Persistence;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

using JetCS.Common;
using System.Net;
using System.Xml.Linq;
using System.Diagnostics.Eventing.Reader;
using JetCS.Domain;
using Microsoft.EntityFrameworkCore;

namespace JetCS.Server.Commands
{
    public class RevokeDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public RevokeDatabaseCommand(Databases databases) : base(databases)
        {
            this.databases = databases;
        }
   
            
     
        public string Name => "REVOKE DATABASE";

        public string Description => $"JetCS {Name} Statement";

        
        public string[] Identifiers => [$"^{Name}"];
        public async Task<CommandResult> ExecuteAsync(Command cmd)
        {
                       
            CommandResult commandResult = new(Name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
           
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");
            }

            //  Authentication and Authorization
            var auth = await databases.LoginWithoutDatabaseAsync(csb.Login, csb.Password);
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
                databases.EnterWriteLock("");
                var dbcontext = databases.CreateDbContext();
                var databaseAlreadyAssigned = await dbcontext.DatabaseLogins.FirstOrDefaultAsync(t => t.Database.Name.ToUpper() == commandString[2].ToUpper() && t.Login.LoginName.ToUpper() == commandString[3].ToUpper());
                if (databaseAlreadyAssigned != null)
                {
                    dbcontext.DatabaseLogins.Remove(databaseAlreadyAssigned);
                    await dbcontext.SaveChangesAsync();
                    commandResult.RecordCount = 1;
                }
                else
                {
                    commandResult.ErrorMessage = $"Can not remove {commandString[3]} from {commandString[2]}";
                }
            }
            finally
            {
                databases.ExitWriteLock("");
            }
            return commandResult;

        }

        
    }
}

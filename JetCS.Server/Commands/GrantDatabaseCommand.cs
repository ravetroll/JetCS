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

namespace JetCS.Server.Commands
{
    public class GrantDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public GrantDatabaseCommand()
        {
            
        }
        public string Name => "GRANT DATABASE";

        public string Description => $"JetCS {Name} Statement; ";

        
        public string[] Identifiers => [$"{Name} "];
        public CommandResult Execute(Command cmd, Databases databases)
        {
                       
            CommandResult commandResult = new(Name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");
            }

            //  Authentication and Authorization
            var auth = databases.LoginWithoutDatabase(csb.Login, csb.Password);
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
            var databaseAlreadyAssigned = databases.DbContext.DatabaseLogins.FirstOrDefault(t=> t.Database.Name.ToUpper() == commandString[2].ToUpper() && t.Login.LoginName.ToUpper() == commandString[3].ToUpper());
            if (databaseAlreadyAssigned == null) { 
                Database? dbAssign = databases.DbContext.Databases.FirstOrDefault(t => t.Name.ToUpper() == commandString[2].ToUpper());
                Login? loginAssign = databases.DbContext.Logins.FirstOrDefault(t => t.LoginName.ToUpper() == commandString[3].ToUpper());
                if (loginAssign == null) {
                    commandResult.ErrorMessage = $"Invalid Login '{commandString[3]}'";
                }
                if (dbAssign == null)
                {
                    commandResult.ErrorMessage = $"Invalid Database '{commandString[2]}'";
                }
                if (loginAssign != null && dbAssign != null) { 
                    DatabaseLogin dbl = new DatabaseLogin() {  Database = dbAssign, Login = loginAssign };
                    databases.DbContext.DatabaseLogins.Add(dbl);
                    databases.DbContext.SaveChanges();
                    commandResult.RecordCount = 1;
                }

            }
               
            return commandResult;

        }

        
    }
}

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
    public class RevokeDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public RevokeDatabaseCommand()
        {
            
        }
        public string Name => "REVOKE DATABASE";

        public string Description => $"JetCS {Name} Statement";

        
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
            var databaseAlreadyAssigned = databases.DbContext.DatabaseLogins.FirstOrDefault(t => t.Database.Name.ToUpper() == commandString[2].ToUpper() && t.Login.LoginName.ToUpper() == commandString[3].ToUpper());
            if (databaseAlreadyAssigned != null)
            {
                databases.DbContext.DatabaseLogins.Remove(databaseAlreadyAssigned);
                databases.DbContext.SaveChanges();
                commandResult.RecordCount = 1;
            }
            else
            {
                commandResult.ErrorMessage = $"Can not remove {commandString[3]} from {commandString[2]}";
            }
            return commandResult;

        }

        
    }
}

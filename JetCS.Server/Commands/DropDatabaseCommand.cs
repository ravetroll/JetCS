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
using EntityFrameworkCore.Jet;
using EntityFrameworkCore.Jet.Data;
using JetCS.Common;
using System.Net;
using System.Xml.Linq;

namespace JetCS.Server.Commands
{
    public class DropDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public DropDatabaseCommand()
        {
            
        }
        public string Name => "DROP DATABASE";

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
            if (commandString.Length != 3)
            {
                return commandResult.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");
            }
            // Connection string for the Jet MDB database
            string connectionString = databases.GetDatabaseConnectionString(commandString[2]);
            if (connectionString != "")
            {

                databases.DeleteDatabase(commandString[2]);
                commandResult.RecordCount = 1;
            }
            else
            {
                commandResult.ErrorMessage = $"Database {commandString[2]} does not exist";
            }
            
                
               
            return commandResult;

        }

        
    }
}

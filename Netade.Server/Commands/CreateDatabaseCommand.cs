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

namespace Netade.Server.Commands
{
    public class CreateDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public CreateDatabaseCommand(Databases databases): base(databases)
        {
            this.databases = databases;
        }
        public string Name => "CREATE DATABASE";

        public string Description => $"Netade {Name} Statement";

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
            if (commandString.Length != 3)
            {
                return commandResult.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");
            }
            if (commandString.Last().IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                return commandResult.SetErrorMessage($"Invalid character '{commandString[2].Substring(commandString.Last().IndexOfAny(System.IO.Path.GetInvalidFileNameChars()), 1)}' in database name");
            }
            
            // Connection string for the Jet MDB database
            string connectionString = databases.CreateDatabaseConnectionString(commandString.Last());
            if (connectionString != "")
            {

                databases.CreateDatabase(commandString[2],connectionString);
                commandResult.RecordCount = 1;
            }
            else
            {
                commandResult.ErrorMessage = $"Database {commandString[2]} already exists";
            }
            
            
                    
                   
            
            return commandResult;

        }

        
    }
}

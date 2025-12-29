using Netade.Common;
using Netade.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Commands
{
    public class AlterDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public AlterDatabaseCommand(Databases databases) : base(databases)
        {
            this.databases = databases;
        }

        public string Name => "ALTER DATABASE";

        public string Description => $"Netade {Name} Statement; {Name} <oldname> RENAME <newname>";

        public string[] Identifiers => [$"^{Name}"];

        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {
            CommandResult commandResult = new(Name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");
            }

            var auth = await databases.LoginWithoutDatabaseAsync(csb.Login, csb.Password, cancellationToken);
            if (!auth.Authenticated)
            {
                return commandResult.SetErrorMessage(auth.StatusMessage);
            }

            if (!auth.IsAdmin)
            {
                return commandResult.SetErrorMessage($"This command required admin priviledges");
            }

            string[] commandString = cmd.CommandText.Split(" ",StringSplitOptions.RemoveEmptyEntries);
            if (commandString.Length != 5)
            {
                return commandResult.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");
            }

            try
            {
               
                string oldDatabaseName = commandString[2];
                string action = commandString[3].ToUpperInvariant();
                string newDatabaseName = commandString[4];
                if (action != "RENAME")
                {
                    return commandResult.SetErrorMessage($"Invalid action '{action}' in '{Name}' Command:{cmd.CommandText}");
                }
                await databases.RenameDatabaseAsync(oldDatabaseName, newDatabaseName, cancellationToken);
                commandResult.RecordCount = 1;
                return commandResult;
            }
            catch (Exception ex)
            {
                return commandResult.SetErrorMessage($"Error executing '{Name}' Command:{cmd.CommandText}. {ex.Message}");
            }
        }
    }
}

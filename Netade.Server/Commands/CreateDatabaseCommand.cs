using Netade.Common.Messaging;
using Netade.Common;

namespace Netade.Server.Commands
{
    public class CreateDatabaseCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public CreateDatabaseCommand(Databases databases) : base(databases)
        {
            this.databases = databases;
        }

        public string Name => "CREATE DATABASE";
        public string Description => $"Netade {Name} Statement";
        public string[] Identifiers => [$"^{Name}"];

        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {
            CommandResult commandResult = new(Name);

            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");

            // Authentication and Authorization
            var auth = await databases.LoginWithoutDatabaseAsync(csb.Login, csb.Password, cancellationToken);
            if (!auth.Authenticated)
                return commandResult.SetErrorMessage(auth.StatusMessage);

            if (!auth.IsAdmin)
                return commandResult.SetErrorMessage("This command required admin priviledges");

            // Expected:
            //   CREATE DATABASE <name> [type]
            // where type is optional: mdb | accdb | .mdb | .accdb | auto
            var parts = cmd.CommandText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length is < 3 or > 4)
                return commandResult.SetErrorMessage(
                    $"Invalid '{Name}' Command:{cmd.CommandText} (expected: CREATE DATABASE <name> [mdb|accdb|auto])");

            var dbName = parts[2];

            if (dbName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                var badIndex = dbName.IndexOfAny(Path.GetInvalidFileNameChars());
                return commandResult.SetErrorMessage(
                    $"Invalid character '{dbName.Substring(badIndex, 1)}' in database name");
            }

            // Optional 4th token
            var type = parts.Length == 4 ? parts[3] : "auto";

            try
            {
                // Databases.CreateDatabase(name, type) is the updated API you added
                await databases.CreateDatabaseAsync(dbName, type, cancellationToken);
                
                return commandResult;
            }
            catch (InvalidOperationException ex)
            {
                // e.g. "Database already exists" or "Neither ACCDB nor MDB creation is available..."
                return commandResult.SetErrorMessage(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                // e.g. requested type not supported by provider
                return commandResult.SetErrorMessage(ex.Message);
            }
            catch (ArgumentException ex)
            {
                // e.g. invalid type token
                return commandResult.SetErrorMessage(ex.Message);
            }
        }
    }
}

using Netade.Common;
using Netade.Common.Messaging;
using Netade.Server.Internal.Cursors;
using Netade.Server.Services.Interfaces;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.Server.Commands
{
    public sealed class FetchCommand : CommandBase, ICommand
    {
        private readonly ICursorRegistryService cursorRegistry;

        public FetchCommand(Databases databases, ICursorRegistryService cursorRegistry)
            : base(databases)
        {
            this.cursorRegistry = cursorRegistry;
        }
        public string Description => $"Netade {Name} Statement";
        public string Name => "FETCH";
        public string[] Identifiers => [$"^{Name}"];
        public override bool DataChange => false;

        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {

            var commandResult = new CommandResult(Name);

            var csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");

            var auth = await dbs.LoginWithDatabaseAsync(csb.Database, csb.Login, csb.Password, cancellationToken);
            if (!auth.Authenticated || !auth.Authorized)
                return commandResult.SetErrorMessage(auth.StatusMessage);

            // FETCH <cursorId> [COUNT <n>]
            var parts = cmd.CommandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!(parts.Length == 2 || parts.Length == 4))
                return commandResult.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");

            if (parts.Length < 2)
                return commandResult.SetErrorMessage("FETCH requires a cursor id.");

            // NOTE: CursorRegistryService uses cursorId formatted as "N" (32 hex chars, no dashes).
            // But we can accept either format from clients and normalize to "N".
            if (!Guid.TryParse(parts[1], out var cursorGuid))
                return commandResult.SetErrorMessage("Invalid cursor id.");

            var cursorId = cursorGuid.ToString("N");

            var count = 100;
            if (parts.Length == 4)
            {
                if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                    return commandResult.SetErrorMessage("Invalid COUNT value.");
                if (!parts[2].Equals("COUNT", StringComparison.OrdinalIgnoreCase))
                    return commandResult.SetErrorMessage("Expected COUNT keyword.");
                count = Math.Clamp(count, 1, 5000);
            }

            
            try
            {
                var page = await cursorRegistry.FetchAsync(cursorId, csb.Login, count, cancellationToken).ConfigureAwait(false);

                return new CommandResult
                {
                    CommandName = Name,
                    Kind = CommandResultKind.CursorPage,
                    CursorId = cursorId,
                    HasMore = page.HasMore,
                    Data = page.Page
                };
            }
            catch (KeyNotFoundException)
            {
                return commandResult.SetErrorMessage("Cursor not found (maybe already closed).");
            }
            catch (ObjectDisposedException ex)
            {
                return commandResult.SetErrorMessage(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return commandResult.SetErrorMessage(ex.Message);
            }
            catch (Exception ex)
            {
                return commandResult.SetErrorMessage(ex.Message);
            }
        }
    }
}

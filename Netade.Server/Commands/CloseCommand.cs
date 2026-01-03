using Netade.Common;
using Netade.Common.Messaging;
using Netade.Server.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.Server.Commands
{
    public sealed class CloseCommand : CommandBase, ICommand
    {
        private readonly ICursorRegistryService cursorRegistry;

        public CloseCommand(Databases databases, ICursorRegistryService cursorRegistry)
            : base(databases)
        {
            this.cursorRegistry = cursorRegistry;
        }
        public string Description => $"Netade {Name} Statement";
        public string Name => "CLOSE";
        public string[] Identifiers => [$"^{Name}"];
        public override bool DataChange => false;

        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {
            var result = new CommandResult(Name);

            var csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
                return result.SetErrorMessage($"Connection string {csb} format is incorrect");

            var auth = await dbs.LoginWithDatabaseAsync(csb.Database, csb.Login, csb.Password, cancellationToken);
            if (!auth.Authenticated || !auth.Authorized)
                return result.SetErrorMessage(auth.StatusMessage);

            // CLOSE <cursorId>
            var parts = cmd.CommandText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return result.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");

            if (!Guid.TryParse(parts[1], out var cursorGuid))
                return result.SetErrorMessage("Invalid cursor id.");

            // CursorRegistryService stores ids as "N" format
            var cursorId = cursorGuid.ToString("N");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var closed = await cursorRegistry.CloseAsync(cursorId, csb.Login).ConfigureAwait(false);
                if (!closed)
                    return result.SetErrorMessage("Cursor not found (maybe already closed).");

                return new CommandResult
                {
                    CommandName = Name,
                    Kind = CommandResultKind.Ack,
                    CursorId = cursorId
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                // Registry or cursor already disposed/closed
                result.ErrorMessage = "Cursor is already closed.";
                result.Kind = CommandResultKind.Ack;
                result.CursorId = cursorId;
                return result;
            }
            catch (Exception ex)
            {
                
                return result.SetErrorMessage(ex.Message);
            }
        }
    }
}

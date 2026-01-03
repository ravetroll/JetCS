using Netade.Common;
using Netade.Common.Helpers;
using Netade.Common.Messaging;
using Netade.Server.Internal.Cursors; // ICursorRegistryService
using Netade.Server.Services.Interfaces;
using System.Data;

namespace Netade.Server.Commands
{
    public class SelectCommand : CommandBase, ICommand
    {
        private readonly ICursorRegistryService cursorRegistry;

        public SelectCommand(Databases dbs, ICursorRegistryService cursorRegistry)
            : base(dbs)
        {
            this.cursorRegistry = cursorRegistry;
        }

        public override bool DataChange => false;

        public string Name => "SELECT";
        public string Description => $"SQL {Name} Statement";
        public string[] Identifiers => ["^(?!.*INTO).*SELECT.*$"];

        public async Task<CommandResult> ExecuteAsync(Command cmd, CancellationToken cancellationToken)
        {
            var result = new CommandResult(Name);

            var csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
                return result.SetErrorMessage($"Connection string {csb} format is incorrect");

            var auth = await dbs.LoginWithDatabaseAsync(csb.Database, csb.Login, csb.Password, cancellationToken);
            if (!auth.Authenticated || !auth.Authorized)
                return result.SetErrorMessage(auth.StatusMessage);

            var connectionString = dbs.GetDatabaseConnectionString(csb.Database);
            if (string.IsNullOrWhiteSpace(connectionString))
                return result.SetErrorMessage($"Database {csb.Database} does not exist");

            try
            {
                if (cmd.Options.ResultMode == QueryResultMode.Snapshot)
                {
                    var rowset = await ExecuteSnapshotAsync(connectionString, cmd.CommandText, cancellationToken)
                        .ConfigureAwait(false);

                    result.Kind = CommandResultKind.Snapshot;
                    result.Data = rowset;
                    result.HasMore = false;
                    return result;
                }

                // Cursor mode: open cursor and return first page
                var opened = await cursorRegistry.OpenCursorAsync(
                    databaseName: csb.Database,
                    login: csb.Login,
                    connectionString: connectionString,
                    sql: cmd.CommandText,
                    fetchSize: cmd.Options.FetchSize,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                result.Kind = CommandResultKind.CursorOpened;
                result.CursorId = opened.CursorId;
                result.Data = opened.FirstPage;
                result.HasMore = opened.HasMore;
                return result;
            }
            catch (Exception ex)
            {
                return result.SetErrorMessage(ex.Message);
            }
        }
    }
}

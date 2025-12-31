using Netade.Common;
using Netade.Common.Helpers;
using Netade.Common.Messaging;
using Netade.Server.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Netade.Server.Commands
{
    public abstract class CommandBase
    {
        protected readonly Databases dbs;

        public CommandBase(Databases dbs)
        {
            this.dbs = dbs;
           
        }
        public virtual bool DataChange => true;
        public async Task<CommandResult> ExecuteNonQueryResultAsync(string name, Command cmd, CancellationToken cancellationToken)
        {


            CommandResult commandResult = new(name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            // Connection string for the Netade database
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");                
            }

            //  Authentication and Authorization
            var auth = await dbs.LoginWithDatabaseAsync(csb.Database, csb.Login, csb.Password, cancellationToken);
            if (!auth.Authenticated) {
                return commandResult.SetErrorMessage(auth.StatusMessage);        
            }
            if(!auth.Authorized)
            {
                return commandResult.SetErrorMessage(auth.StatusMessage);
            }
            
            // Connection string for the Jet MDB database
            string connectionString = dbs.GetDatabaseConnectionString(csb.Database);
            if (connectionString == "")
            {
                return commandResult.SetErrorMessage($"Database {csb.Database} does not exist");                
            }
            try
            {
                // Enter write lock
                await using var _ = await dbs.EnterDatabaseWriteAsync(csb.Database, cancellationToken).ConfigureAwait(false);
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                    using (OleDbCommand command = new OleDbCommand(cmd.CommandText, connection))
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                    }
                }
            }
            
            catch (Exception ex)
            {
                commandResult.ErrorMessage = ex.Message;
            }
            
            return commandResult;
        }

        

        protected static async Task<Rowset> ExecuteSnapshotAsync(
    string connectionString,
    string sql,
    CancellationToken cancellationToken)
        {
            using var connection = new OleDbConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using var command = new OleDbCommand(sql, connection);

            using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken).ConfigureAwait(false);

            var rowset = new Rowset();

            if (reader is null)
                return rowset;

            var schema = await reader.GetSchemaTableAsync(cancellationToken).ConfigureAwait(false);
            if (schema != null)
            {
                foreach (DataRow row in schema.Rows)
                {
                    rowset.Columns.Add(new ColumnDef
                    {
                        Name = row.Field<string>("ColumnName") ?? "",
                        TypeName = (row["DataType"] as Type)?.FullName ?? "System.Object"
                    });
                }
            }

            int fieldCount = reader.FieldCount;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var r = new System.Text.Json.Nodes.JsonNode?[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                    r[i] = RowsetBuilder.ToJsonNode(reader.GetValue(i));
                rowset.Rows.Add(r);
            }

            rowset.RecordCount = rowset.Rows.Count;
            return rowset;
        }



    }
}

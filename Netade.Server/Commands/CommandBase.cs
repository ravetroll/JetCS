using Netade.Common;
using Netade.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Netade.Server.Commands
{
    public abstract class CommandBase
    {
        private readonly Databases dbs;

        public CommandBase(Databases dbs)
        {
            this.dbs = dbs;
        }
        public bool DataChange { get; } = true;        
        public async Task<CommandResult> ExecuteNonQueryResultAsync(string name, Command cmd)
        {


            CommandResult commandResult = new(name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            // Connection string for the Netade database
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");                
            }

            //  Authentication and Authorization
            var auth = await dbs.LoginWithDatabaseAsync(csb.Database, csb.Login, csb.Password);
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
                dbs.EnterWriteLock(csb.Database);
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    connection.Open();

                    using (OleDbCommand command = new OleDbCommand(cmd.CommandText, connection))
                    {
                        commandResult.RecordCount = await command.ExecuteNonQueryAsync();

                    }
                }
            }
            
            catch (Exception ex)
            {
                commandResult.ErrorMessage = ex.Message;
            }
            finally
            {
                dbs.ExitWriteLock(csb.Database);
            }
            return commandResult;
        }

        public async Task<CommandResult> ExecuteQueryResultAsync(string name, Command cmd)
        {


            CommandResult commandResult = new(name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            // Connection string for the Netade database
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");
            }

            //  Authentication and Authorization
            var auth = await dbs.LoginWithDatabaseAsync(csb.Database, csb.Login, csb.Password);
            if (!auth.Authenticated)
            {
                return commandResult.SetErrorMessage(auth.StatusMessage);
            }
            if (!auth.Authorized)
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
                dbs.EnterReadLock(csb.Database);
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    connection.Open();

                    using (OleDbCommand command = new OleDbCommand(cmd.CommandText, connection))
                    {

                       
                        DataTable dt = null;
                        using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess))
                        {

                            DataTable schemaTable = await reader.GetSchemaTableAsync();
                            dt = new DataTable();
                            foreach (DataRow row in schemaTable.Rows)
                                dt.Columns.Add(row.Field<string>("ColumnName"), row.Field<Type>("DataType"));


                            while (await reader.ReadAsync())
                            {
                                DataRow dr = dt.Rows.Add();
                                foreach (DataColumn col in dt.Columns)
                                    dr[col.ColumnName] = reader[col.ColumnName];
                            }
                        }
                        commandResult.Result = dt;

                    }
                }
            }
            catch (Exception ex)
            {
                commandResult.ErrorMessage = ex.Message;
            }
            finally
            {
                dbs.ExitReadLock(csb.Database);
            }

                        
            return commandResult;
        }
    }
}

using JetCS.Common;
using JetCS.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace JetCS.Server.Commands
{
    public abstract class CommandBase
    {
        public CommandResult ExecuteNonQueryResult(string name, Command cmd, Databases dbs)
        {


            CommandResult commandResult = new(name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            // Connection string for the JetCS database
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");                
            }

            //  Authentication and Authorization
            var auth =dbs.LoginWithDatabase(csb.Database, csb.Login, csb.Password);
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
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    connection.Open();

                    using (OleDbCommand command = new OleDbCommand(cmd.CommandText, connection))
                    {
                        commandResult.RecordCount = command.ExecuteNonQuery();

                    }
                }
            } catch (Exception ex)
            {
                commandResult.ErrorMessage = ex.Message;
            }
            return commandResult;
        }

        public CommandResult ExecuteQueryResult(string name, Command cmd, Databases dbs)
        {


            CommandResult commandResult = new(name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            // Connection string for the JetCS database
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");
            }

            //  Authentication and Authorization
            var auth = dbs.LoginWithDatabase(csb.Database, csb.Login, csb.Password);
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
                using (OleDbConnection connection = new OleDbConnection(connectionString))
                {
                    connection.Open();

                    using (OleDbCommand command = new OleDbCommand(cmd.CommandText, connection))
                    {

                        using (OleDbDataAdapter adapter = new OleDbDataAdapter(command))
                        {
                            DataTable result = new DataTable();
                            commandResult.RecordCount = adapter.Fill(result);
                            // Adds a blank row so the columns can be serialized or we end up with an empty serialization string
                            if (result.Rows.Count == 0) { result.Rows.Add(); }
                            commandResult.Result = result;

                        }

                    }
                }
            }
            catch (Exception ex)
            {
                commandResult.ErrorMessage = ex.Message;
            }
                        
            return commandResult;
        }
    }
}

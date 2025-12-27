using System.Data;
using System.Data.OleDb;

namespace Netade.Common.Helpers
{
    public class AccessSchemaHelperOleDb
    {
        private string connectionString;

        public AccessSchemaHelperOleDb(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public DataTable GetTables()
        {
            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                conn.Open();

                DataTable schema = conn.GetOleDbSchemaTable(
                    OleDbSchemaGuid.Tables,
                    new object[] { null, null, null, "TABLE" }
                );

                // Filter out system tables
                DataTable result = new DataTable();
                result.Columns.Add("TABLE_NAME", typeof(string));
                result.Columns.Add("TABLE_TYPE", typeof(string));

                foreach (DataRow row in schema.Rows)
                {
                    string tableName = row["TABLE_NAME"].ToString();
                    if (!tableName.StartsWith("MSys") && !tableName.StartsWith("~"))
                    {
                        DataRow newRow = result.NewRow();
                        newRow["TABLE_NAME"] = tableName;
                        newRow["TABLE_TYPE"] = "TABLE";
                        result.Rows.Add(newRow);
                    }
                }

                return result;
            }
        }

        public DataTable GetColumns(string tableName)
        {
            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                conn.Open();

                DataTable schema = conn.GetOleDbSchemaTable(
                    OleDbSchemaGuid.Columns,
                    new object[] { null, null, tableName, null }
                );

                // Format to match ODBC expectations
                DataTable result = new DataTable();
                result.Columns.Add("TABLE_NAME", typeof(string));
                result.Columns.Add("COLUMN_NAME", typeof(string));
                result.Columns.Add("ORDINAL_POSITION", typeof(int));
                result.Columns.Add("DATA_TYPE", typeof(string));
                result.Columns.Add("CHARACTER_MAXIMUM_LENGTH", typeof(int));
                result.Columns.Add("IS_NULLABLE", typeof(string));

                foreach (DataRow row in schema.Rows)
                {
                    DataRow newRow = result.NewRow();
                    newRow["TABLE_NAME"] = row["TABLE_NAME"];
                    newRow["COLUMN_NAME"] = row["COLUMN_NAME"];
                    newRow["ORDINAL_POSITION"] = row["ORDINAL_POSITION"];
                    newRow["DATA_TYPE"] = MapOleDbType((int)row["DATA_TYPE"]);
                    newRow["CHARACTER_MAXIMUM_LENGTH"] = row["CHARACTER_MAXIMUM_LENGTH"];
                    newRow["IS_NULLABLE"] = (bool)row["IS_NULLABLE"] ? "YES" : "NO";
                    result.Rows.Add(newRow);
                }

                return result;
            }
        }

        public DataTable GetPrimaryKeys(string tableName)
        {
            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                conn.Open();

                DataTable schema = conn.GetOleDbSchemaTable(
                    OleDbSchemaGuid.Primary_Keys,
                    new object[] { null, null, tableName }
                );

                return schema;
            }
        }

        public DataTable GetIndexes(string tableName)
        {
            using (OleDbConnection conn = new OleDbConnection(connectionString))
            {
                conn.Open();

                DataTable schema = conn.GetOleDbSchemaTable(
                    OleDbSchemaGuid.Indexes,
                    new object[] { null, null, null, null, tableName }
                );

                return schema;
            }
        }

        private string MapOleDbType(int oleDbType)
        {
            // Map OLE DB type codes to SQL type names
            switch (oleDbType)
            {
                case 2: // DBTYPE_I2
                case 3: // DBTYPE_I4
                    return "INTEGER";
                case 4: // DBTYPE_R4
                case 5: // DBTYPE_R8
                    return "DOUBLE";
                case 6: // DBTYPE_CY
                    return "CURRENCY";
                case 7: // DBTYPE_DATE
                    return "DATETIME";
                case 11: // DBTYPE_BOOL
                    return "BIT";
                case 130: // DBTYPE_WSTR
                    return "VARCHAR";
                case 201: // DBTYPE_LONGVARCHAR
                    return "LONGTEXT";
                default:
                    return "VARCHAR";
            }
        }
    }
}

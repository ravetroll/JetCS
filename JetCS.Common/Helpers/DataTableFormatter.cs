using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Common.Helpers
{
    public static class DataTableFormatter
    {
        public static string DataTableToString(DataTable table)
        {
            StringBuilder builder = new StringBuilder();
            foreach (DataRow row in table.Rows)
            {
                foreach (DataColumn col in table.Columns)
                {
                    builder.Append(row[col].ToString() + "\t");
                }
                builder.AppendLine();
            }
            return builder.ToString();
        }
    }
}

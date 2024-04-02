using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Jet.Data;

namespace JetCS.Common.Messaging
{
    public class Command
    {
        public Command()
        {
            CommandText = "NO COMMAND";
            ConnectionString = string.Empty;
        }    

        public Command(string connString, string commandText)
        {

            CommandText = commandText;
            ConnectionString = connString;

        }

        
        public  string ConnectionString {  get; set; }
        public string CommandText { get; set; }



    }
}

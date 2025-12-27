using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Jet.Data;

namespace Netade.Common.Messaging
{
    public class Command
    {
        public Command()
        {
            BuildCommand("NO CONNECTION", "NO COMMAND",null);
        }    

        public Command(string connString, string commandText)
        {

            BuildCommand(connString, commandText, null);
        }

        private void BuildCommand(string connString, string commandText, string? errorMessage)
        {
            CommandText = commandText;
            ConnectionString = connString;
            ErrorMessage = null;
        }

        public string? ErrorMessage { get; set; }
        public  string ConnectionString {  get; set; }
        public string CommandText { get; set; }



    }
}

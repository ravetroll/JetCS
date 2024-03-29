using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Common.Messaging
{
    public class CommandResult
    {
        
        public CommandResult() { }

        public CommandResult(string commandName) { CommandName = commandName; }
        public DataTable Result { get; set; }
        public int RecordCount { get; set; }
        public string ErrorMessage { get; set; }

        public string CommandName { get; set; }

        public CommandResult SetErrorMessage(string msg)
        {
            this.ErrorMessage = msg;
            return this;
        }
    }
}

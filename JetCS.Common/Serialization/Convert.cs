using JetCS.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace JetCS.Common.Serialization
{
    public static class Convert
    {
       

        public static string SerializeCommand(Command command)
        {
            
            return JsonConvert.SerializeObject(command);
            
        }

        public static Command DeSerializeCommand(string command)
        {
            
            return JsonConvert.DeserializeObject<Command>(command);
        }

        public static string SerializeCommandResult(CommandResult command)
        {

           
            return JsonConvert.SerializeObject(command);
        }

        public static CommandResult DeSerializeCommandResult(string command)
        {
            
            return JsonConvert.DeserializeObject<CommandResult>(command);
        }


    }
}

using Netade.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Netade.Common.Serialization
{
    public static class ConvertCommandAndResult
    {
       

      
        public static string SerializeCommand(Command command)
        {
            
            return JsonConvert.SerializeObject(command,JsonSettings.Settings);
            
        }

        public static Command DeSerializeCommand(string command)
        {
            
            return JsonConvert.DeserializeObject<Command>(command,JsonSettings.Settings);
        }

        public  static string SerializeCommandResult(CommandResult command)
        {

           
            return JsonConvert.SerializeObject(command, JsonSettings.Settings);
        }

        public  static CommandResult DeSerializeCommandResult(string command)
        {
            
            return JsonConvert.DeserializeObject<CommandResult>(command, JsonSettings.Settings);
        }


    }
}

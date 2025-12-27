using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Internal.Security
{
    internal class Infractions
    {
        int Ok { get; set; }
        int ConnectionStringInvalid { get; set; }
        int AuthenticationFailed { get; set; }
        int AuthorizationFailed { get; set; }
        int DatabaseNotFound { get; set; }
        int NonQueryException { get; set; }
        int QueryException { get; set; }
        int MaxClientsExceeded { get; set; }
        int MaxCommandSizeExceeded { get; set; }
        int MaxCommandResultSizeExceeded { get; set; }
        int CommandTimeoutExceeded { get; set; }
        int CommandResultTimeoutExceeded { get; set; }

       
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Common.Messaging
{
    public  class Auth
    {
        public string LoginName {  get; set; }
        public bool Authenticated { get; set; } = false;
        public List<string> DatabaseNames { get; set; }
        public bool Authorized { get; set; } = false;
        public bool IsAdmin { get; set; }
        public string StatusMessage { get; set; }

        public bool HasDatabase(string name) { return DatabaseNames.Any(t=>t.ToLower().Equals(name.ToLower())); }

    }
}

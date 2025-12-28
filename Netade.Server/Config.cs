using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server
{
    public class Config
    {
        public string DatabasePath { get; set; }
        public string Provider { get; set; } = "Microsoft.ACE.OLEDB.16.0";
        public int ListenPort { get; set; } = 1549;
        public bool CompressedMode { get; set; } = true;
        public bool SingleClient { get; set; } = false;

        public Limits Limits { get; set; } = new Limits()
        {
            MaxClients = 10,
            MaxCommandSizeBytes = 1024 * 1024 * 10, // 10 MB
            MaxCommandResultSizeBytes = 1024 * 1024 * 10, // 10 MB
            CommandResultTimeout = 10000, // 10 seconds
            CommandTimeout = 10000 // 10 seconds
        };
    }

    public class Limits
    {
        public int MaxClients { get; set; }
        public int MaxCommandSizeBytes { get; set; }
        public int MaxCommandResultSizeBytes { get; set; }
        public int CommandResultTimeout { get; set; }
        public int CommandTimeout { get; set; }
    }

    
}

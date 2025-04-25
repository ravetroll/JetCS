using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Server
{
    public class Config
    {
        public string DatabasePath { get; set; }
        public string Provider { get; set; }
        public int ListenPort { get; set; }
        public bool CompressedMode { get; set; }

        public Limits Limits { get; set; } = new Limits()
        {
            MaxClients = 10,
            MaxCommandSizeBytes = 1024 * 1024 * 10 // 10 MB
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

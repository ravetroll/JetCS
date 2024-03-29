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
    }
}

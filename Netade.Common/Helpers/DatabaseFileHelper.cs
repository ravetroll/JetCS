using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Common.Helpers
{
    public static class DatabaseFileHelper
    {
        public static string GetNameFromPath(FileInfo t)
        {
            return t.Name.Substring(0, t.Name.Length - (t.Extension.Length));
        }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Common.Serialization
{
    public static class JsonSettings
    {
        public static JsonSerializerSettings Settings { get; }

        static JsonSettings()
        {
            Settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented, // Optional pretty-print
                NullValueHandling = NullValueHandling.Include, // Or Ignore if you prefer
                Converters = { new DataTableJsonConverter() },
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
        }
    }
}

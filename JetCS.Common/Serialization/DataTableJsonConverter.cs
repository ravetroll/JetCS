using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Common.Serialization
{
    public class DataTableJsonConverter : JsonConverter<DataTable>
    {
        //  Unused but should really be the way of handling the DataTable in the CommandResult
        public override DataTable ReadJson(JsonReader reader, Type objectType, DataTable existingValue, bool hasExistingValue, JsonSerializer serializer)
        {

            if (hasExistingValue)
            {
                DataTable t = JObject.Load(reader).ToObject<DataTable>();


                if (t.Rows.Count == 1)
                {
                    var isEmptyRow = true;
                    for (int i = 0; i < t.Columns.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(t.Rows[0][i].ToString())) isEmptyRow = false;
                        break;
                    }
                    if (isEmptyRow) t.Rows.RemoveAt(0);
                }
                return t;
            }
            else return new DataTable();
        }

        //JsonConvert.DefaultSettings = () => new JsonSerializerSettings
        //{
        //    Converters = new List<JsonConverter> { new DataTableJsonConverter() }
        //};


        public override void WriteJson(JsonWriter writer, DataTable t, JsonSerializer serializer)
        {
            
            if (t.Rows.Count == 0) t.Rows.Add();
            //  When referenced with the commented code above this seems to result in a recursive call (defaults to 64 Depth most likely)
            


            writer.WriteRaw(JsonConvert.SerializeObject(t));
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;
    }
}

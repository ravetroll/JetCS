using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Common.Serialization
{
    public class DataTableJsonConverter : JsonConverter<DataTable>
    {
        //  Unused but should really be the way of handling the DataTable in the CommandResult
        public override DataTable ReadJson(JsonReader reader, Type objectType, DataTable existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
           
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            var table = new DataTable();

            JObject root = JObject.Load(reader);

            // 1. Read columns
            var columns = root["columns"];
            if (columns != null)
            {
                foreach (var col in columns)
                {
                    string columnName = col["name"]?.ToString();
                    string typeName = col["type"]?.ToString();
                    Type dataType = ResolveType(typeName);

                    if (columnName != null && dataType != null)
                    {
                        table.Columns.Add(columnName, dataType);
                    }
                }
            }

            // 2. Read rows
            var rows = root["rows"];
            if (rows != null)
            {
                foreach (var rowObj in rows)
                {
                    var row = table.NewRow();

                    foreach (DataColumn column in table.Columns)
                    {
                        var field = rowObj[column.ColumnName];
                        if (field != null && field["value"] != null)
                        {
                            var valueToken = field["value"];
                            if (valueToken.Type == JTokenType.Null)
                            {
                                row[column] = DBNull.Value;
                            }
                            else
                            {
                                row[column] = valueToken.ToObject(column.DataType);
                            }
                        }
                    }

                    table.Rows.Add(row);
                }
            }

            return table;
        }

        private Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeof(string); // Default fallback

            return typeName switch
            {
                "String" => typeof(string),
                "Byte" => typeof(byte),
                "Int16" => typeof(short),
                "Int32" => typeof(int),
                "Int64" => typeof(long),
                "Single" => typeof(float),
                "Double" => typeof(double),
                "Decimal" => typeof(decimal),
                "Boolean" => typeof(bool),
                "DateTime" => typeof(DateTime),
                "Guid" => typeof(Guid),
                "Byte[]" => typeof(byte[]), // For OLE Objects, Attachments
                _ => typeof(string) // Fallback if unknown type
            };
        }

     


        public override void WriteJson(JsonWriter writer, DataTable value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();

            // 1. Write the schema
            writer.WritePropertyName("columns");
            writer.WriteStartArray();

            foreach (DataColumn column in value.Columns)
            {
                writer.WriteStartObject();

                writer.WritePropertyName("name");
                serializer.Serialize(writer, column.ColumnName);

                writer.WritePropertyName("type");
                serializer.Serialize(writer, column.DataType.Name);

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            // 2. Write the rows
            writer.WritePropertyName("rows");
            writer.WriteStartArray();

            foreach (DataRow row in value.Rows)
            {
                writer.WriteStartObject();

                foreach (DataColumn column in value.Columns)
                {
                    writer.WritePropertyName(column.ColumnName);

                    writer.WriteStartObject();

                    writer.WritePropertyName("value");
                    serializer.Serialize(writer, row[column]);

                    writer.WritePropertyName("type");
                    serializer.Serialize(writer, column.DataType.Name);

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        public override bool CanRead => true;

        public override bool CanWrite => true;
    }
}

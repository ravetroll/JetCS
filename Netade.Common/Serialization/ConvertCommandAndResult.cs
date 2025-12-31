using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Netade.Common.Messaging;

namespace Netade.Common.Serialization
{
    public static class ConvertCommandAndResult
    {
        private static readonly JsonSerializerOptions Options = CreateOptions();

        public static string SerializeCommand(Command command)
            => JsonSerializer.Serialize(command, Options);

        public static Command DeSerializeCommand(string json)
            => JsonSerializer.Deserialize<Command>(json, Options)
               ?? throw new JsonException("Failed to deserialize Command (null result).");

        public static string SerializeCommandResult(CommandResult result)
            => JsonSerializer.Serialize(result, Options);

        public static CommandResult DeSerializeCommandResult(string json)
            => JsonSerializer.Deserialize<CommandResult>(json, Options)
               ?? throw new JsonException("Failed to deserialize CommandResult (null result).");

        private static JsonSerializerOptions CreateOptions()
        {
            var o = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            return o;
        }
    }
}

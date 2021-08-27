using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Myriad.Serialization
{
    public class JsonStringConverter: JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = JsonSerializer.Deserialize<string>(ref reader);
            var inner = JsonSerializer.Deserialize(str!, typeToConvert, options);
            return inner;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var inner = JsonSerializer.Serialize(value, options);
            writer.WriteStringValue(inner);
        }
    }
}
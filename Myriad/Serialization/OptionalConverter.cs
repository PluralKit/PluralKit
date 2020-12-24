using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Myriad.Utils;

namespace Myriad.Serialization
{
    public class OptionalConverter: JsonConverter<IOptional>
    {
        public override IOptional? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var innerType = typeToConvert.GetGenericArguments()[0];
            var inner = JsonSerializer.Deserialize(ref reader, innerType, options);

            // TODO: rewrite to JsonConverterFactory to cut down on reflection
            return (IOptional?) Activator.CreateInstance(
                typeof(Optional<>).MakeGenericType(innerType),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] {inner}, 
                null);
        }

        public override void Write(Utf8JsonWriter writer, IOptional value, JsonSerializerOptions options)
        {
            var innerType = value.GetType().GetGenericArguments()[0];
            JsonSerializer.Serialize(writer, value.GetValue(), innerType, options);
        }

        public override bool CanConvert(Type typeToConvert)
        {
            if (!typeToConvert.IsGenericType)
                return false;

            if (typeToConvert.GetGenericTypeDefinition() != typeof(Optional<>))
                return false;

            return true;
        }
    }
}
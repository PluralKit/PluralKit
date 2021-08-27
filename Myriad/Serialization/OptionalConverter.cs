using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Myriad.Utils;

namespace Myriad.Serialization
{
    public class OptionalConverterFactory: JsonConverterFactory
    {
        public class Inner<T>: JsonConverter<Optional<T>>
        {
            public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var inner = JsonSerializer.Deserialize<T>(ref reader, options);
                return new(inner!);
            }

            public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value.HasValue ? value.GetValue() : default, typeof(T), options);
            }
        }

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            var innerType = typeToConvert.GetGenericArguments()[0];
            return (JsonConverter?)Activator.CreateInstance(
                typeof(Inner<>).MakeGenericType(innerType),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                null,
                null);
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
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Myriad.Serialization
{
    public class JsonSnakeCaseStringEnumConverter: JsonConverterFactory
    {
        private readonly JsonStringEnumConverter _inner = new(new JsonSnakeCaseNamingPolicy());

        public override bool CanConvert(Type typeToConvert) =>
            _inner.CanConvert(typeToConvert);

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            _inner.CreateConverter(typeToConvert, options);
    }
}
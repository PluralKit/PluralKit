using System.Text.Json;
using System.Text.Json.Serialization;

namespace Myriad.Serialization
{
    public static class JsonSerializerOptionsExtensions
    {
        public static JsonSerializerOptions ConfigureForMyriad(this JsonSerializerOptions opts)
        {
            opts.PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy();
            opts.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            opts.IncludeFields = true;

            opts.Converters.Add(new PermissionSetJsonConverter());
            opts.Converters.Add(new ShardInfoJsonConverter());
            opts.Converters.Add(new OptionalConverterFactory());

            return opts;
        }
    }
}
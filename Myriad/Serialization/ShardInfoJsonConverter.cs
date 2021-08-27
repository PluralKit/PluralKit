using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Myriad.Gateway;

namespace Myriad.Serialization
{
    public class ShardInfoJsonConverter: JsonConverter<ShardInfo>
    {
        public override ShardInfo? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var arr = JsonSerializer.Deserialize<int[]>(ref reader);
            if (arr?.Length != 2)
                throw new JsonException("Expected shard info as array of length 2");

            return new ShardInfo(arr[0], arr[1]);
        }

        public override void Write(Utf8JsonWriter writer, ShardInfo value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteNumberValue(value.ShardId);
            writer.WriteNumberValue(value.NumShards);
            writer.WriteEndArray();
        }
    }
}
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Myriad.Types;

namespace Myriad.Serialization
{
    public class PermissionSetJsonConverter: JsonConverter<PermissionSet>
    {
        public override PermissionSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString();
            if (str == null) return default;

            return (PermissionSet)ulong.Parse(str);
        }

        public override void Write(Utf8JsonWriter writer, PermissionSet value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(((ulong)value).ToString());
        }
    }
}
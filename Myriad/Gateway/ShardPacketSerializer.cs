using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace Myriad.Gateway
{
    public class ShardPacketSerializer
    {
        private const int BufferSize = 64 * 1024;

        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public ShardPacketSerializer(JsonSerializerOptions jsonSerializerOptions)
        {
            _jsonSerializerOptions = jsonSerializerOptions;
        }

        public async ValueTask<(WebSocketMessageType type, GatewayPacket? packet)> ReadPacket(ClientWebSocket socket)
        {
            using var buf = MemoryPool<byte>.Shared.Rent(BufferSize);

            var res = await socket.ReceiveAsync(buf.Memory, default);
            if (res.MessageType == WebSocketMessageType.Close)
                return (res.MessageType, null);

            if (res.EndOfMessage)
                // Entire packet fits within one buffer, deserialize directly
                return DeserializeSingleBuffer(buf, res);

            // Otherwise copy to stream buffer and deserialize from there
            return await DeserializeMultipleBuffer(socket, buf, res);
        }

        public async Task WritePacket(ClientWebSocket socket, GatewayPacket packet)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(packet, _jsonSerializerOptions);
            await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, default);
        }

        private async Task<(WebSocketMessageType type, GatewayPacket packet)> DeserializeMultipleBuffer(ClientWebSocket socket, IMemoryOwner<byte> buf, ValueWebSocketReceiveResult res)
        {
            await using var stream = new MemoryStream(BufferSize * 4);
            stream.Write(buf.Memory.Span.Slice(0, res.Count));

            while (!res.EndOfMessage)
            {
                res = await socket.ReceiveAsync(buf.Memory, default);
                stream.Write(buf.Memory.Span.Slice(0, res.Count));
            }

            return DeserializeObject(res, stream.GetBuffer().AsSpan(0, (int)stream.Length));
        }

        private (WebSocketMessageType type, GatewayPacket packet) DeserializeSingleBuffer(
            IMemoryOwner<byte> buf, ValueWebSocketReceiveResult res)
        {
            var span = buf.Memory.Span.Slice(0, res.Count);
            return DeserializeObject(res, span);
        }

        private (WebSocketMessageType type, GatewayPacket packet) DeserializeObject(ValueWebSocketReceiveResult res, Span<byte> span)
        {
            var packet = JsonSerializer.Deserialize<GatewayPacket>(span, _jsonSerializerOptions)!;
            return (res.MessageType, packet);
        }
    }
}
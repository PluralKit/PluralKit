using System;
using System.Buffers;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace Myriad.Gateway
{
    public class ShardConnection: IAsyncDisposable
    {
        private readonly MemoryStream _bufStream = new();

        private readonly ClientWebSocket _client = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly ILogger _logger;
        private readonly Task _worker;

        public ShardConnection(Uri uri, ILogger logger, JsonSerializerOptions jsonSerializerOptions)
        {
            _logger = logger;
            _jsonSerializerOptions = jsonSerializerOptions;

            _worker = Worker(uri);
        }

        public Func<GatewayPacket, Task>? OnReceive { get; set; }

        public WebSocketState State => _client.State;

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _worker;

            _client.Dispose();
            await _bufStream.DisposeAsync();
            _cts.Dispose();
        }

        private async Task Worker(Uri uri)
        {
            var realUrl = new UriBuilder(uri)
            {
                Query = "v=8&encoding=json"
            }.Uri;
            _logger.Debug("Connecting to gateway WebSocket at {GatewayUrl}", realUrl);
            await _client.ConnectAsync(realUrl, default);

            while (!_cts.IsCancellationRequested && _client.State == WebSocketState.Open)
                try
                {
                    await HandleReceive();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error in WebSocket receive worker");
                }
        }

        private async Task HandleReceive()
        {
            _bufStream.SetLength(0);
            var result = await ReadData(_bufStream);
            var data = _bufStream.GetBuffer().AsMemory(0, (int) _bufStream.Position);

            if (result.MessageType == WebSocketMessageType.Text)
                await HandleReceiveData(data);
            else if (result.MessageType == WebSocketMessageType.Close)
                _logger.Information("WebSocket closed by server: {StatusCode} {Reason}", _client.CloseStatus,
                    _client.CloseStatusDescription);
        }

        private async Task HandleReceiveData(Memory<byte> data)
        {
            var packet = JsonSerializer.Deserialize<GatewayPacket>(data.Span, _jsonSerializerOptions)!;

            try
            {
                if (OnReceive != null)
                    await OnReceive.Invoke(packet);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error in gateway handler for {OpcodeType}", packet.Opcode);
            }
        }

        private async Task<ValueWebSocketReceiveResult> ReadData(MemoryStream stream)
        {
            using var buf = MemoryPool<byte>.Shared.Rent();
            ValueWebSocketReceiveResult result;
            do
            {
                result = await _client.ReceiveAsync(buf.Memory, _cts.Token);
                stream.Write(buf.Memory.Span.Slice(0, result.Count));
            } while (!result.EndOfMessage);

            return result;
        }

        public async Task Send(GatewayPacket packet)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(packet, _jsonSerializerOptions);
            await _client.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, default);
        }

        public async Task Disconnect(WebSocketCloseStatus status, string? description)
        {
            await _client.CloseAsync(status, description, default);
            _cts.Cancel();
        }
    }
}
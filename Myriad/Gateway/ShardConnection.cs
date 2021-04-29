using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace Myriad.Gateway
{
    public class ShardConnection: IAsyncDisposable
    {
        private ClientWebSocket? _client;
        private readonly ILogger _logger;
        private readonly ShardPacketSerializer _serializer;
        
        public WebSocketState State => _client?.State ?? WebSocketState.Closed;
        public WebSocketCloseStatus? CloseStatus => _client?.CloseStatus;
        public string? CloseStatusDescription => _client?.CloseStatusDescription;
        
        public ShardConnection(JsonSerializerOptions jsonSerializerOptions, ILogger logger)
        {
            _logger = logger.ForContext<ShardConnection>();
            _serializer = new(jsonSerializerOptions);
        }

        public async Task Connect(string url, CancellationToken ct)
        {
            _client?.Dispose();
            _client = new ClientWebSocket();
            
            await _client.ConnectAsync(GetConnectionUri(url), ct);
        }

        public async Task Disconnect(WebSocketCloseStatus closeStatus, string? reason)
        {
            await CloseInner(closeStatus, reason);
        }

        public async Task Send(GatewayPacket packet)
        {
            if (_client == null || _client.State != WebSocketState.Open)
                return;

            try
            {
                await _serializer.WritePacket(_client, packet);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error sending WebSocket message");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CloseInner(WebSocketCloseStatus.NormalClosure, null);
            _client?.Dispose();
        }

        public async Task<GatewayPacket?> Read()
        {
            if (_client == null || _client.State != WebSocketState.Open)
                return null;
            
            try
            {
                var (_, packet) = await _serializer.ReadPacket(_client);
                return packet;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error reading from WebSocket");
            }

            return null;
        }
        
        private Uri GetConnectionUri(string baseUri) => new UriBuilder(baseUri)
        {
            Query = "v=8&encoding=json"
        }.Uri;

        private async Task CloseInner(WebSocketCloseStatus closeStatus, string? description)
        {
            if (_client == null)
                return;

            if (_client.State != WebSocketState.Connecting && _client.State != WebSocketState.Open)
                return;

            // Close with timeout, mostly to work around https://github.com/dotnet/runtime/issues/51590
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await _client.CloseAsync(closeStatus, description, cts.Token);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error closing WebSocket connection");
            }
        }
    }
}
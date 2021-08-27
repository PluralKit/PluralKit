using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;

using Myriad.Gateway.State;
using Myriad.Types;

using Serilog;

namespace Myriad.Gateway
{
    public class ShardStateManager
    {
        private readonly HeartbeatWorker _heartbeatWorker = new();
        private readonly ILogger _logger;

        private readonly ShardInfo _info;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private ShardState _state = ShardState.Disconnected;

        private DateTimeOffset? _lastHeartbeatSent;
        private TimeSpan? _latency;
        private bool _hasReceivedHeartbeatAck;

        private string? _sessionId;
        private int? _lastSeq;

        public ShardState State => _state;
        public TimeSpan? Latency => _latency;
        public User? User { get; private set; }
        public ApplicationPartial? Application { get; private set; }

        public Func<Task> SendIdentify { get; init; }
        public Func<(string SessionId, int? LastSeq), Task> SendResume { get; init; }
        public Func<int?, Task> SendHeartbeat { get; init; }
        public Func<WebSocketCloseStatus, TimeSpan, Task> Reconnect { get; init; }
        public Func<Task> Connect { get; init; }
        public Func<IGatewayEvent, Task> HandleEvent { get; init; }

        public event Action<TimeSpan> OnHeartbeatReceived;

        public ShardStateManager(ShardInfo info, JsonSerializerOptions jsonSerializerOptions, ILogger logger)
        {
            _info = info;
            _jsonSerializerOptions = jsonSerializerOptions;
            _logger = logger.ForContext<ShardStateManager>();
        }

        public Task HandleConnectionOpened()
        {
            _state = ShardState.Handshaking;
            return Task.CompletedTask;
        }

        public async Task HandleConnectionClosed()
        {
            _latency = null;
            await _heartbeatWorker.Stop();
        }

        public async Task HandlePacketReceived(GatewayPacket packet)
        {
            switch (packet.Opcode)
            {
                case GatewayOpcode.Hello:
                    var hello = DeserializePayload<GatewayHello>(packet);
                    await HandleHello(hello);
                    break;

                case GatewayOpcode.Heartbeat:
                    await HandleHeartbeatRequest();
                    break;

                case GatewayOpcode.HeartbeatAck:
                    await HandleHeartbeatAck();
                    break;

                case GatewayOpcode.Reconnect:
                    {
                        await HandleReconnect();
                        break;
                    }

                case GatewayOpcode.InvalidSession:
                    {
                        var canResume = DeserializePayload<bool>(packet);
                        await HandleInvalidSession(canResume);
                        break;
                    }

                case GatewayOpcode.Dispatch:
                    _lastSeq = packet.Sequence;

                    var evt = DeserializeEvent(packet.EventType!, (JsonElement)packet.Payload!);
                    if (evt != null)
                    {
                        if (evt is ReadyEvent ready)
                            await HandleReady(ready);

                        if (evt is ResumedEvent)
                            await HandleResumed();

                        await HandleEvent(evt);
                    }
                    break;
            }
        }

        private async Task HandleHello(GatewayHello hello)
        {
            var interval = TimeSpan.FromMilliseconds(hello.HeartbeatInterval);

            _hasReceivedHeartbeatAck = true;
            await _heartbeatWorker.Start(interval, HandleHeartbeatTimer);
            await IdentifyOrResume();
        }

        private async Task IdentifyOrResume()
        {
            _state = ShardState.Identifying;

            if (_sessionId != null)
            {
                _logger.Information("Shard {ShardId}: Received Hello, attempting to resume (seq {LastSeq})",
                    _info.ShardId, _lastSeq);
                await SendResume((_sessionId!, _lastSeq));
            }
            else
            {
                _logger.Information("Shard {ShardId}: Received Hello, identifying",
                    _info.ShardId);

                await SendIdentify();
            }
        }

        private Task HandleHeartbeatAck()
        {
            _hasReceivedHeartbeatAck = true;
            _latency = DateTimeOffset.UtcNow - _lastHeartbeatSent;
            OnHeartbeatReceived?.Invoke(_latency!.Value);
            _logger.Debug("Shard {ShardId}: Received Heartbeat (latency {Latency:N2} ms)",
                _info.ShardId, _latency?.TotalMilliseconds);
            return Task.CompletedTask;
        }

        private async Task HandleInvalidSession(bool canResume)
        {
            if (!canResume)
            {
                _sessionId = null;
                _lastSeq = null;
            }

            _logger.Information("Shard {ShardId}: Received Invalid Session (can resume? {CanResume})",
                _info.ShardId, canResume);

            var delay = TimeSpan.FromMilliseconds(new Random().Next(1000, 5000));
            await DoReconnect(WebSocketCloseStatus.NormalClosure, delay);
        }

        private async Task HandleReconnect()
        {
            _logger.Information("Shard {ShardId}: Received Reconnect", _info.ShardId);
            // close code 1000 kills the session, so can't reconnect
            // we use 1005 (no error specified) instead
            await DoReconnect(WebSocketCloseStatus.Empty, TimeSpan.FromSeconds(1));
        }

        private Task HandleReady(ReadyEvent ready)
        {
            _logger.Information("Shard {ShardId}: Received Ready", _info.ShardId);

            _sessionId = ready.SessionId;
            _state = ShardState.Connected;
            User = ready.User;
            Application = ready.Application;
            return Task.CompletedTask;
        }

        private Task HandleResumed()
        {
            _logger.Information("Shard {ShardId}: Received Resume", _info.ShardId);

            _state = ShardState.Connected;
            return Task.CompletedTask;
        }

        private async Task HandleHeartbeatRequest()
        {
            await SendHeartbeatInternal();
        }

        private async Task SendHeartbeatInternal()
        {
            await SendHeartbeat(_lastSeq);
            _lastHeartbeatSent = DateTimeOffset.UtcNow;
        }

        private async Task HandleHeartbeatTimer()
        {
            if (!_hasReceivedHeartbeatAck)
            {
                _logger.Warning("Shard {ShardId}: Heartbeat worker timed out", _info.ShardId);
                await DoReconnect(WebSocketCloseStatus.ProtocolError, TimeSpan.Zero);
                return;
            }

            await SendHeartbeatInternal();
        }

        private async Task DoReconnect(WebSocketCloseStatus closeStatus, TimeSpan delay)
        {
            _state = ShardState.Reconnecting;
            await Reconnect(closeStatus, delay);
        }

        private T DeserializePayload<T>(GatewayPacket packet)
        {
            var packetPayload = (JsonElement)packet.Payload!;
            return JsonSerializer.Deserialize<T>(packetPayload.GetRawText(), _jsonSerializerOptions)!;
        }

        private IGatewayEvent? DeserializeEvent(string eventType, JsonElement payload)
        {
            if (!IGatewayEvent.EventTypes.TryGetValue(eventType, out var clrType))
            {
                _logger.Debug("Shard {ShardId}: Received unknown event type {EventType}", _info.ShardId, eventType);
                return null;
            }

            try
            {
                _logger.Verbose("Shard {ShardId}: Deserializing {EventType} to {ClrType}", _info.ShardId, eventType, clrType);
                return JsonSerializer.Deserialize(payload.GetRawText(), clrType, _jsonSerializerOptions)
                    as IGatewayEvent;
            }
            catch (JsonException e)
            {
                _logger.Error(e, "Shard {ShardId}: Error deserializing event {EventType} to {ClrType}", _info.ShardId, eventType, clrType);
                return null;
            }
        }
    }
}
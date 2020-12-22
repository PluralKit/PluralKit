using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;

using Myriad.Serialization;
using Myriad.Types;

using Serilog;

namespace Myriad.Gateway
{
    public class Shard: IAsyncDisposable
    {
        private const string LibraryName = "Newcord Test";

        private readonly JsonSerializerOptions _jsonSerializerOptions =
            new JsonSerializerOptions().ConfigureForNewcord();

        private readonly ILogger _logger;
        private readonly Uri _uri;
        
        private ShardConnection? _conn;
        private TimeSpan? _currentHeartbeatInterval;
        private bool _hasReceivedAck;
        private DateTimeOffset? _lastHeartbeatSent;
        private Task _worker;
        
        public ShardInfo? ShardInfo { get; private set; }
        public GatewaySettings Settings { get; }
        public ShardSessionInfo SessionInfo { get; private set; }
        public ShardState State { get; private set; }
        public TimeSpan? Latency { get; private set; }
        public User? User { get; private set; }

        public Func<IGatewayEvent, Task>? OnEventReceived { get; set; }
        
        public Shard(ILogger logger, Uri uri, GatewaySettings settings, ShardInfo? info = null,
                     ShardSessionInfo? sessionInfo = null)
        {
            _logger = logger;
            _uri = uri;

            Settings = settings;
            ShardInfo = info;
            SessionInfo = sessionInfo ?? new ShardSessionInfo();
        }

        public async ValueTask DisposeAsync()
        {
            if (_conn != null)
                await _conn.DisposeAsync();
        }

        public Task Start()
        {
            _worker = MainLoop();
            return Task.CompletedTask;
        }

        public async Task UpdateStatus(GatewayStatusUpdate payload)
        {
            if (_conn != null && _conn.State == WebSocketState.Open)
                await _conn!.Send(new GatewayPacket {Opcode = GatewayOpcode.PresenceUpdate, Payload = payload});
        }

        private async Task MainLoop()
        {
            while (true)
                try
                {
                    _logger.Information("Connecting...");

                    State = ShardState.Connecting;
                    await Connect();

                    _logger.Information("Connected. Entering main loop...");

                    // Tick returns false if we need to stop and reconnect
                    while (await Tick(_conn!))
                        await Task.Delay(TimeSpan.FromMilliseconds(1000));

                    _logger.Information("Connection closed, reconnecting...");
                    State = ShardState.Closed;
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error in shard state handler");
                }
        }

        private async Task<bool> Tick(ShardConnection conn)
        {
            if (conn.State != WebSocketState.Connecting && conn.State != WebSocketState.Open)
                return false;

            if (!await TickHeartbeat(conn))
                // TickHeartbeat returns false if we're disconnecting
                return false;

            return true;
        }

        private async Task<bool> TickHeartbeat(ShardConnection conn)
        {
            // If we don't need to heartbeat, do nothing
            if (_lastHeartbeatSent == null || _currentHeartbeatInterval == null)
                return true;

            if (DateTimeOffset.UtcNow - _lastHeartbeatSent < _currentHeartbeatInterval)
                return true;

            // If we haven't received the ack in time, close w/ error
            if (!_hasReceivedAck)
            {
                _logger.Warning(
                    "Did not receive heartbeat Ack from gateway within interval ({HeartbeatInterval})",
                    _currentHeartbeatInterval);
                State = ShardState.Closing;
                await conn.Disconnect(WebSocketCloseStatus.ProtocolError, "Did not receive ACK in time");
                return false;
            }

            // Otherwise just send it :)
            await SendHeartbeat(conn);
            _hasReceivedAck = false;
            return true;
        }

        private async Task SendHeartbeat(ShardConnection conn)
        {
            _logger.Debug("Sending heartbeat");

            await conn.Send(new GatewayPacket {Opcode = GatewayOpcode.Heartbeat, Payload = SessionInfo.LastSequence});
            _lastHeartbeatSent = DateTimeOffset.UtcNow;
        }

        private async Task Connect()
        {
            if (_conn != null)
                await _conn.DisposeAsync();

            _currentHeartbeatInterval = null;

            _conn = new ShardConnection(_uri, _logger, _jsonSerializerOptions) {OnReceive = OnReceive};
        }

        private async Task OnReceive(GatewayPacket packet)
        {
            switch (packet.Opcode)
            {
                case GatewayOpcode.Hello:
                {
                    await HandleHello((JsonElement) packet.Payload!);
                    break;
                }
                case GatewayOpcode.Heartbeat:
                {
                    _logger.Debug("Received heartbeat request from shard, sending Ack");
                    await _conn!.Send(new GatewayPacket {Opcode = GatewayOpcode.HeartbeatAck});
                    break;
                }
                case GatewayOpcode.HeartbeatAck:
                {
                    Latency = DateTimeOffset.UtcNow - _lastHeartbeatSent;
                    _logger.Debug("Received heartbeat Ack (latency {Latency})", Latency);

                    _hasReceivedAck = true;
                    break;
                }
                case GatewayOpcode.Reconnect:
                {
                    _logger.Information("Received Reconnect, closing and reconnecting");
                    await _conn!.Disconnect(WebSocketCloseStatus.Empty, null);
                    break;
                }
                case GatewayOpcode.InvalidSession:
                {
                    var canResume = ((JsonElement) packet.Payload!).GetBoolean();

                    // Clear session info before DCing
                    if (!canResume)
                        SessionInfo = SessionInfo with { Session = null };

                    var delay = TimeSpan.FromMilliseconds(new Random().Next(1000, 5000));

                    _logger.Information(
                        "Received Invalid Session (can resume? {CanResume}), reconnecting after {ReconnectDelay}",
                        canResume, delay);
                    await _conn!.Disconnect(WebSocketCloseStatus.Empty, null);

                    // Will reconnect after exiting this "loop"
                    await Task.Delay(delay);
                    break;
                }
                case GatewayOpcode.Dispatch:
                {
                    SessionInfo = SessionInfo with { LastSequence = packet.Sequence };
                    var evt = DeserializeEvent(packet.EventType!, (JsonElement) packet.Payload!)!;

                    if (evt is ReadyEvent rdy)
                    {
                        if (State == ShardState.Connecting)
                            await HandleReady(rdy);
                        else
                            _logger.Warning("Received Ready event in unexpected state {ShardState}, ignoring?", State);
                    }
                    else if (evt is ResumedEvent)
                    {
                        if (State == ShardState.Connecting)
                            await HandleResumed();
                        else
                            _logger.Warning("Received Resumed event in unexpected state {ShardState}, ignoring?",
                                State);
                    }

                    await HandleEvent(evt);
                    break;
                }
                default:
                {
                    _logger.Debug("Received unknown gateway opcode {Opcode}", packet.Opcode);
                    break;
                }
            }
        }

        private async Task HandleEvent(IGatewayEvent evt)
        {
            if (OnEventReceived != null)
                await OnEventReceived.Invoke(evt);
        }


        private IGatewayEvent? DeserializeEvent(string eventType, JsonElement data)
        {
            if (!IGatewayEvent.EventTypes.TryGetValue(eventType, out var clrType))
            {
                _logger.Information("Received unknown event type {EventType}", eventType);
                return null;
            }

            try
            {
                _logger.Verbose("Deserializing {EventType} to {ClrType}", eventType, clrType);
                return JsonSerializer.Deserialize(data.GetRawText(), clrType, _jsonSerializerOptions)
                    as IGatewayEvent;
            }
            catch (JsonException e)
            {
                _logger.Error(e, "Error deserializing event {EventType} to {ClrType}", eventType, clrType);
                return null;
            }
        }

        private Task HandleReady(ReadyEvent ready)
        {
            ShardInfo = ready.Shard;
            SessionInfo = SessionInfo with { Session = ready.SessionId };
            User = ready.User;
            State = ShardState.Open;

            return Task.CompletedTask;
        }

        private Task HandleResumed()
        {
            State = ShardState.Open;
            return Task.CompletedTask;
        }

        private async Task HandleHello(JsonElement json)
        {
            var hello = JsonSerializer.Deserialize<GatewayHello>(json.GetRawText(), _jsonSerializerOptions)!;
            _logger.Debug("Received Hello with interval {Interval} ms", hello.HeartbeatInterval);
            _currentHeartbeatInterval = TimeSpan.FromMilliseconds(hello.HeartbeatInterval);

            await SendHeartbeat(_conn!);

            await SendIdentifyOrResume();
        }

        private async Task SendIdentifyOrResume()
        {
            if (SessionInfo.Session != null && SessionInfo.LastSequence != null)
                await SendResume(SessionInfo.Session, SessionInfo.LastSequence!.Value);
            else
                await SendIdentify();
        }

        private async Task SendIdentify()
        {
            _logger.Information("Sending gateway Identify for shard {@ShardInfo}", SessionInfo);
            await _conn!.Send(new GatewayPacket
            {
                Opcode = GatewayOpcode.Identify,
                Payload = new GatewayIdentify
                {
                    Token = Settings.Token,
                    Properties = new GatewayIdentify.ConnectionProperties
                    {
                        Browser = LibraryName, Device = LibraryName, Os = Environment.OSVersion.ToString()
                    },
                    Intents = Settings.Intents,
                    Shard = ShardInfo
                }
            });
        }

        private async Task SendResume(string session, int lastSequence)
        {
            _logger.Information("Sending gateway Resume for session {@SessionInfo}", ShardInfo,
                SessionInfo);
            await _conn!.Send(new GatewayPacket
            {
                Opcode = GatewayOpcode.Resume, Payload = new GatewayResume(Settings.Token, session, lastSequence)
            });
        }
        
        public enum ShardState
        {
            Closed,
            Connecting,
            Open,
            Closing
        }
    }
}
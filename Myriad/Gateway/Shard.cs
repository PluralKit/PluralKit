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
        private const string LibraryName = "Myriad (for PluralKit)";

        private readonly JsonSerializerOptions _jsonSerializerOptions =
            new JsonSerializerOptions().ConfigureForMyriad();

        private readonly ILogger _logger;
        private readonly Uri _uri;
        
        private ShardConnection? _conn;
        private TimeSpan? _currentHeartbeatInterval;
        private bool _hasReceivedAck;
        private DateTimeOffset? _lastHeartbeatSent;
        private Task _worker;
        
        public ShardInfo ShardInfo { get; private set; }
        public int ShardId => ShardInfo.ShardId;
        public GatewaySettings Settings { get; }
        public ShardSessionInfo SessionInfo { get; private set; }
        public ShardState State { get; private set; }
        public TimeSpan? Latency { get; private set; }
        public User? User { get; private set; }
        public ApplicationPartial? Application { get; private set; }

        public Func<IGatewayEvent, Task>? OnEventReceived { get; set; }
        public event Action<TimeSpan>? HeartbeatReceived;
        public event Action? SocketOpened;
        public event Action? Resumed;
        public event Action? Ready;
        public event Action<WebSocketCloseStatus, string?>? SocketClosed;
        
        public Shard(ILogger logger, Uri uri, GatewaySettings settings, ShardInfo info,
                     ShardSessionInfo? sessionInfo = null)
        {
            _logger = logger.ForContext<Shard>();
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
                    _logger.Information("Shard {ShardId}: Connecting...", ShardId);

                    State = ShardState.Connecting;
                    await Connect();

                    _logger.Information("Shard {ShardId}: Connected. Entering main loop...", ShardId);

                    // Tick returns false if we need to stop and reconnect
                    while (await Tick(_conn!))
                        await Task.Delay(TimeSpan.FromMilliseconds(1000));

                    _logger.Information("Shard {ShardId}: Connection closed, reconnecting...", ShardId);
                    State = ShardState.Closed;
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Shard {ShardId}: Error in shard state handler", ShardId);
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
                    "Shard {ShardId}: Did not receive heartbeat Ack from gateway within interval ({HeartbeatInterval})",
                    ShardId, _currentHeartbeatInterval);
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
            _logger.Debug("Shard {ShardId}: Sending heartbeat with seq.no. {LastSequence}", 
                ShardId, SessionInfo.LastSequence);

            await conn.Send(new GatewayPacket {Opcode = GatewayOpcode.Heartbeat, Payload = SessionInfo.LastSequence});
            _lastHeartbeatSent = DateTimeOffset.UtcNow;
        }

        private async Task Connect()
        {
            if (_conn != null)
                await _conn.DisposeAsync();

            _currentHeartbeatInterval = null;

            _conn = new ShardConnection(_uri, _logger, _jsonSerializerOptions)
            {
                OnReceive = OnReceive,
                OnOpen = () => SocketOpened?.Invoke(),
                OnClose = (closeStatus, message) => SocketClosed?.Invoke(closeStatus, message)
            };
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
                    _logger.Debug("Shard {ShardId}: Received heartbeat request from shard, sending Ack", ShardId);
                    await _conn!.Send(new GatewayPacket {Opcode = GatewayOpcode.HeartbeatAck});
                    break;
                }
                case GatewayOpcode.HeartbeatAck:
                {
                    Latency = DateTimeOffset.UtcNow - _lastHeartbeatSent;
                    _logger.Debug("Shard {ShardId}: Received heartbeat Ack with latency {Latency}", ShardId, Latency);
                    if (Latency != null)
                        HeartbeatReceived?.Invoke(Latency!.Value);

                    _hasReceivedAck = true;
                    break;
                }
                case GatewayOpcode.Reconnect:
                {
                    _logger.Information("Shard {ShardId}: Received Reconnect, closing and reconnecting", ShardId);
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
                        "Shard {ShardId}: Received Invalid Session (can resume? {CanResume}), reconnecting after {ReconnectDelay}",
                        ShardId, canResume, delay);
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
                            _logger.Warning("Shard {ShardId}: Received Ready event in unexpected state {ShardState}, ignoring?", 
                                ShardId, State);
                    }
                    else if (evt is ResumedEvent)
                    {
                        if (State == ShardState.Connecting)
                            await HandleResumed();
                        else
                            _logger.Warning("Shard {ShardId}: Received Resumed event in unexpected state {ShardState}, ignoring?", 
                                ShardId, State);
                    }

                    await HandleEvent(evt);
                    break;
                }
                default:
                {
                    _logger.Debug("Shard {ShardId}: Received unknown gateway opcode {Opcode}", ShardId, packet.Opcode);
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
                _logger.Information("Shard {ShardId}: Received unknown event type {EventType}", ShardId, eventType);
                return null;
            }

            try
            {
                _logger.Verbose("Shard {ShardId}: Deserializing {EventType} to {ClrType}", ShardId, eventType, clrType);
                return JsonSerializer.Deserialize(data.GetRawText(), clrType, _jsonSerializerOptions)
                    as IGatewayEvent;
            }
            catch (JsonException e)
            {
                _logger.Error(e, "Shard {ShardId}: Error deserializing event {EventType} to {ClrType}", ShardId, eventType, clrType);
                return null;
            }
        }

        private Task HandleReady(ReadyEvent ready)
        {
            // TODO: when is ready.Shard ever null?
            ShardInfo = ready.Shard ?? new ShardInfo(0, 0);
            SessionInfo = SessionInfo with { Session = ready.SessionId };
            User = ready.User;
            Application = ready.Application;
            State = ShardState.Open;
            
            Ready?.Invoke();
            return Task.CompletedTask;
        }

        private Task HandleResumed()
        {
            State = ShardState.Open;
            Resumed?.Invoke();
            return Task.CompletedTask;
        }

        private async Task HandleHello(JsonElement json)
        {
            var hello = JsonSerializer.Deserialize<GatewayHello>(json.GetRawText(), _jsonSerializerOptions)!;
            _logger.Debug("Shard {ShardId}: Received Hello with interval {Interval} ms", ShardId, hello.HeartbeatInterval);
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
            _logger.Information("Shard {ShardId}: Sending gateway Identify for shard {@ShardInfo}", ShardId, ShardInfo);
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
            _logger.Information("Shard {ShardId}: Sending gateway Resume for session {@SessionInfo}",
                ShardId, SessionInfo);
            await _conn!.Send(new GatewayPacket
            {
                Opcode = GatewayOpcode.Resume, 
                Payload = new GatewayResume(Settings.Token, session, lastSequence)
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
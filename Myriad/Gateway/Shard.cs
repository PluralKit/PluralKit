using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Tasks;

using Myriad.Gateway.Limit;
using Myriad.Gateway.State;
using Myriad.Serialization;
using Myriad.Types;

using Serilog;
using Serilog.Context;

namespace Myriad.Gateway
{
    public class Shard
    {
        private const string LibraryName = "Myriad (for PluralKit)";

        private readonly GatewaySettings _settings;
        private readonly ShardInfo _info;
        private readonly IGatewayRatelimiter _ratelimiter;
        private readonly string _url;
        private readonly ILogger _logger;
        private readonly ShardStateManager _stateManager;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly ShardConnection _conn;

        public int ShardId => _info.ShardId;
        public ShardState State => _stateManager.State;
        public TimeSpan? Latency => _stateManager.Latency;
        public User? User => _stateManager.User;
        public ApplicationPartial? Application => _stateManager.Application;

        // TODO: I wanna get rid of these or move them at some point
        public event Func<IGatewayEvent, Task>? OnEventReceived;
        public event Action<TimeSpan>? HeartbeatReceived;
        public event Action? SocketOpened;
        public event Action? Resumed;
        public event Action? Ready;
        public event Action<WebSocketCloseStatus?, string?>? SocketClosed;

        private TimeSpan _reconnectDelay = TimeSpan.Zero;
        private Task? _worker;

        public Shard(GatewaySettings settings, ShardInfo info, IGatewayRatelimiter ratelimiter, string url, ILogger logger)
        {
            _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();

            _settings = settings;
            _info = info;
            _ratelimiter = ratelimiter;
            _url = url;
            _logger = logger.ForContext<Shard>().ForContext("ShardId", info.ShardId);
            _stateManager = new ShardStateManager(info, _jsonSerializerOptions, logger)
            {
                HandleEvent = HandleEvent,
                SendHeartbeat = SendHeartbeat,
                SendIdentify = SendIdentify,
                SendResume = SendResume,
                Connect = ConnectInner,
                Reconnect = Reconnect,
            };
            _stateManager.OnHeartbeatReceived += latency =>
            {
                HeartbeatReceived?.Invoke(latency);
            };

            _conn = new ShardConnection(_jsonSerializerOptions, _logger);
        }

        private async Task ShardLoop()
        {
            // may be superfluous but this adds shard id to ambient context which is nice
            using var _ = LogContext.PushProperty("ShardId", _info.ShardId);

            while (true)
            {
                try
                {
                    await ConnectInner();

                    await HandleConnectionOpened();

                    while (_conn.State == WebSocketState.Open)
                    {
                        var packet = await _conn.Read();
                        if (packet == null)
                            break;

                        await _stateManager.HandlePacketReceived(packet);
                    }

                    await HandleConnectionClosed(_conn.CloseStatus, _conn.CloseStatusDescription);

                    _logger.Information("Shard {ShardId}: Reconnecting after delay {ReconnectDelay}",
                        _info.ShardId, _reconnectDelay);

                    if (_reconnectDelay > TimeSpan.Zero)
                        await Task.Delay(_reconnectDelay);
                    _reconnectDelay = TimeSpan.Zero;
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Shard {ShardId}: Error in main shard loop, reconnecting in 5 seconds...", _info.ShardId);

                    // todo: exponential backoff here? this should never happen, ideally...
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }

        public async Task Start()
        {
            if (_worker == null)
                _worker = ShardLoop();

            // Ideally we'd stagger the startups so we don't smash the websocket but that's difficult with the
            // identify rate limiter so this is the best we can do rn, maybe?
            await Task.Delay(200);
        }

        public async Task UpdateStatus(GatewayStatusUpdate payload)
        {
            await _conn.Send(new GatewayPacket
            {
                Opcode = GatewayOpcode.PresenceUpdate,
                Payload = payload
            });
        }

        private async Task ConnectInner()
        {
            while (true)
            {
                await _ratelimiter.Identify(_info.ShardId);

                _logger.Information("Shard {ShardId}: Connecting to WebSocket", _info.ShardId);
                try
                {
                    await _conn.Connect(_url, default);
                    break;
                }
                catch (WebSocketException e)
                {
                    _logger.Error(e, "Shard {ShardId}: Error connecting to WebSocket, retrying in 5 seconds...", _info.ShardId);
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }

        private async Task DisconnectInner(WebSocketCloseStatus closeStatus)
        {
            await _conn.Disconnect(closeStatus, null);
        }

        private async Task SendIdentify()
        {
            await _conn.Send(new GatewayPacket
            {
                Opcode = GatewayOpcode.Identify,
                Payload = new GatewayIdentify
                {
                    Compress = false,
                    Intents = _settings.Intents,
                    Properties = new GatewayIdentify.ConnectionProperties
                    {
                        Browser = LibraryName,
                        Device = LibraryName,
                        Os = Environment.OSVersion.ToString()
                    },
                    Shard = _info,
                    Token = _settings.Token,
                    LargeThreshold = 50
                }
            });
        }

        private async Task SendResume((string SessionId, int? LastSeq) arg)
        {
            await _conn.Send(new GatewayPacket
            {
                Opcode = GatewayOpcode.Resume,
                Payload = new GatewayResume(_settings.Token, arg.SessionId, arg.LastSeq ?? 0)
            });
        }

        private async Task SendHeartbeat(int? lastSeq)
        {
            await _conn.Send(new GatewayPacket { Opcode = GatewayOpcode.Heartbeat, Payload = lastSeq });
        }

        private async Task Reconnect(WebSocketCloseStatus closeStatus, TimeSpan delay)
        {
            _reconnectDelay = delay;
            await DisconnectInner(closeStatus);
        }

        private async Task HandleEvent(IGatewayEvent arg)
        {
            if (arg is ReadyEvent)
                Ready?.Invoke();
            if (arg is ResumedEvent)
                Resumed?.Invoke();

            await (OnEventReceived?.Invoke(arg) ?? Task.CompletedTask);
        }

        private async Task HandleConnectionOpened()
        {
            _logger.Information("Shard {ShardId}: Connection opened", _info.ShardId);
            await _stateManager.HandleConnectionOpened();
            SocketOpened?.Invoke();
        }

        private async Task HandleConnectionClosed(WebSocketCloseStatus? closeStatus, string? description)
        {
            _logger.Information("Shard {ShardId}: Connection closed ({CloseStatus}/{Description})",
                _info.ShardId, closeStatus, description ?? "<null>");
            await _stateManager.HandleConnectionClosed();
            SocketClosed?.Invoke(closeStatus, description);
        }
    }
}
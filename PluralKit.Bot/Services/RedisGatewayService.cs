using System.Text.Json;

using Serilog;

using StackExchange.Redis;

using Myriad.Gateway;
using Myriad.Serialization;

namespace PluralKit.Bot;

public class RedisGatewayService
{
    private readonly BotConfig _config;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private ConnectionMultiplexer _redis;
    private ILogger _logger;

    public RedisGatewayService(BotConfig config, ILogger logger)
    {
        _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();
        _config = config;
        _logger = logger.ForContext<RedisGatewayService>();
    }

    public event Func<(int, IGatewayEvent), Task>? OnEventReceived;

    public async Task Start(int shardId)
    {
        if (_redis == null)
            _redis = await ConnectionMultiplexer.ConnectAsync(_config.RedisGatewayUrl);

        _logger.Debug("Subscribing to shard {ShardId} on redis", shardId);

        var channel = await _redis.GetSubscriber().SubscribeAsync($"evt-{shardId}");
        channel.OnMessage((evt) => Handle(shardId, evt));
    }

    public async Task Handle(int shardId, ChannelMessage message)
    {
        var packet = JsonSerializer.Deserialize<GatewayPacket>(message.Message, _jsonSerializerOptions);
        if (packet.Opcode != GatewayOpcode.Dispatch) return;
        var evt = DeserializeEvent(packet.EventType, (JsonElement)packet.Payload);
        if (evt == null) return;
        await OnEventReceived((shardId, evt));
    }

    private IGatewayEvent? DeserializeEvent(string eventType, JsonElement payload)
    {
        if (!IGatewayEvent.EventTypes.TryGetValue(eventType, out var clrType))
        {
            _logger.Debug("Received unknown event type {EventType}", eventType);
            return null;
        }

        try
        {
            _logger.Verbose("Deserializing {EventType} to {ClrType}", eventType, clrType);
            return JsonSerializer.Deserialize(payload.GetRawText(), clrType, _jsonSerializerOptions) as IGatewayEvent;
        }
        catch (JsonException e)
        {
            _logger.Error(e, "Error deserializing event {EventType} to {ClrType}", eventType, clrType);
            return null;
        }
    }
}
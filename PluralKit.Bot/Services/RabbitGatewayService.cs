using System.Text;
using System.Text.Json;

using Serilog;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Myriad.Gateway;
using Myriad.Serialization;

namespace PluralKit.Bot;

public class RabbitGatewayService
{
    private readonly BotConfig _config;
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private IConnection _rabbitConnection;
    private ILogger _logger;

    public RabbitGatewayService(BotConfig config, ILogger logger)
    {
        _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();
        _config = config;
        _logger = logger.ForContext<RabbitGatewayService>();
    }

    public event Func<(int, IGatewayEvent), Task>? OnEventReceived;

    public async Task Start()
    {
        _logger.Debug("Connecting to RabbitMQ for gateway events");
        ConnectionFactory factory = new ConnectionFactory()
        {
            Uri = new Uri(_config.RabbitUrl)
        };

        _rabbitConnection = await factory.CreateConnectionAsync();
        var channel = await _rabbitConnection.CreateChannelAsync();

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (ch, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body, 0, body.Length);
                var packet = JsonSerializer.Deserialize<GatewayPacket>(message, _jsonSerializerOptions);
                await channel.BasicAckAsync(ea.DeliveryTag, false);
                if (packet.Opcode != GatewayOpcode.Dispatch) return;
                var evt = DeserializeEvent(packet.EventType, (JsonElement)packet.Payload);
                if (evt == null) return;
                await OnEventReceived((0, evt));
            }
            catch (Exception e)
            {
                _logger.Error(e, "failed to process event");
            }
        };
        await channel.BasicConsumeAsync(_config.RabbitQueue, false, consumer);
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
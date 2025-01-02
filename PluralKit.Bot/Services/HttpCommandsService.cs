using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

using Autofac;

using Serilog;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Serialization;
using Myriad.Types;

namespace PluralKit.Bot;

public class HttpCommandsService
{
    private readonly ILogger _logger;
    private readonly BotConfig _config;
    private readonly Bot _bot;
    private readonly IDiscordCache _cache;

    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public HttpCommandsService(ILogger logger, BotConfig config, Bot bot, IDiscordCache cache)
    {
        _logger = logger.ForContext<HttpCommandsService>();
        _config = config;
        _bot = bot;
        _cache = cache;

        _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();
    }

    public async Task Run()
    {
        _logger.Information("Starting HTTP commands listener on :6000");
        var host = new WebHostBuilder()
            .UseKestrel(options =>
            {
                options.Listen(IPAddress.Any, 6000);
            })
            .Configure(app =>
            {
                app.Run(HandleRequest);
            })
            .Build();

        await host.RunAsync();
    }

    public async Task HandleRequest(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var plaintext = await reader.ReadToEndAsync();
        var packet = JsonSerializer.Deserialize<GatewayPacket>(plaintext, _jsonSerializerOptions);
        var evt = DeserializeEvent(packet.EventType!, (JsonElement)packet.Payload!);
        // spawn new thread to handle request
        // todo: we might actually need the shard id here?
        var _ = _bot.OnEventReceivedInner(0, evt);
        await context.Response.WriteAsync("ok");
    }

    // from ShardStateManager
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
            return JsonSerializer.Deserialize(payload.GetRawText(), clrType, _jsonSerializerOptions)
                as IGatewayEvent;
        }
        catch (JsonException e)
        {
            _logger.Error(e, "Error deserializing event {EventType} to {ClrType}", eventType, clrType);
            return null;
        }
    }
}
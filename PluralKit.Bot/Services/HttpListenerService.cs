using System.Text;
using System.Text.Json;

using Serilog;

using WatsonWebserver.Lite;
using WatsonWebserver.Core;

using Myriad.Gateway;
using Myriad.Serialization;

namespace PluralKit.Bot;

public class HttpListenerService
{
    private readonly ILogger _logger;
    private readonly RuntimeConfigService _runtimeConfig;
    private readonly Bot _bot;

    public HttpListenerService(ILogger logger, RuntimeConfigService runtimeConfig, Bot bot)
    {
        _logger = logger.ForContext<HttpListenerService>();
        _runtimeConfig = runtimeConfig;
        _bot = bot;
    }

    public void Start(string host)
    {
        var hosts = new[] { host };
        if (host == "allv4v6")
        {
            hosts = new[] { "[::]", "0.0.0.0" };
        }
        foreach (var h in hosts)
        {
            var server = new WebserverLite(new WebserverSettings(h, 5002), DefaultRoute);

            server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/runtime_config", RuntimeConfigGet);
            server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.POST, "/runtime_config/{key}", RuntimeConfigSet);
            server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.DELETE, "/runtime_config/{key}", RuntimeConfigDelete);

            server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.POST, "/events/{shard_id}", GatewayEvent);

            server.Start();
        }
    }

    private async Task DefaultRoute(HttpContextBase ctx)
        => await ctx.Response.Send("hellorld");

    private async Task RuntimeConfigGet(HttpContextBase ctx)
    {
        var config = _runtimeConfig.GetAll();
        ctx.Response.Headers.Add("content-type", "application/json");
        await ctx.Response.Send(JsonSerializer.Serialize(config));
    }

    private async Task RuntimeConfigSet(HttpContextBase ctx)
    {
        var key = ctx.Request.Url.Parameters["key"];
        var value = ReadStream(ctx.Request.Data, ctx.Request.ContentLength);
        await _runtimeConfig.Set(key, value);
        await RuntimeConfigGet(ctx);
    }

    private async Task RuntimeConfigDelete(HttpContextBase ctx)
    {
        var key = ctx.Request.Url.Parameters["key"];
        await _runtimeConfig.Delete(key);
        await RuntimeConfigGet(ctx);
    }

    private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions().ConfigureForMyriad();

    private async Task GatewayEvent(HttpContextBase ctx)
    {
        var shardIdString = ctx.Request.Url.Parameters["shard_id"];
        if (!int.TryParse(shardIdString, out var shardId)) return;

        var packet = JsonSerializer.Deserialize<GatewayPacket>(ReadStream(ctx.Request.Data, ctx.Request.ContentLength), _jsonSerializerOptions);
        var evt = DeserializeEvent(shardId, packet.EventType!, (JsonElement)packet.Payload!);
        if (evt != null)
        {
            await _bot.OnEventReceivedInner(shardId, evt);
        }
        await ctx.Response.Send("a");
    }

    private IGatewayEvent? DeserializeEvent(int shardId, string eventType, JsonElement payload)
    {
        if (!IGatewayEvent.EventTypes.TryGetValue(eventType, out var clrType))
        {
            _logger.Debug("Shard {ShardId}: Received unknown event type {EventType}", shardId, eventType);
            return null;
        }

        try
        {
            _logger.Verbose("Shard {ShardId}: Deserializing {EventType} to {ClrType}", shardId, eventType,
                clrType);
            return JsonSerializer.Deserialize(payload.GetRawText(), clrType, _jsonSerializerOptions)
                as IGatewayEvent;
        }
        catch (JsonException e)
        {
            _logger.Error(e, "Shard {ShardId}: Error deserializing event {EventType} to {ClrType}", shardId,
                eventType, clrType);
            return null;
        }
    }

    //temporary re-implementation of the ReadStream function found in WatsonWebserver.Lite, but with handling for closed connections
    //https://github.com/dotnet/WatsonWebserver/issues/171
    private static string ReadStream(Stream input, long contentLength)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");
        if (contentLength < 1) return "";

        byte[] buffer = new byte[65536];
        long bytesRemaining = contentLength;

        using (MemoryStream ms = new MemoryStream())
        {
            int read;

            while (bytesRemaining > 0)
            {
                read = input.Read(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    ms.Write(buffer, 0, read);
                    bytesRemaining -= read;
                }
                else
                {
                    throw new IOException("Connection closed before reading end of stream.");
                }
            }

            if (ms.Length < 1) return null;
            var str = Encoding.Default.GetString(ms.ToArray());
            return str;
        }
    }
}
using Serilog;

using Newtonsoft.Json;

using WatsonWebserver.Lite;
using WatsonWebserver.Core;

namespace PluralKit.Bot;

public class HttpListenerService
{
    private readonly ILogger _logger;
    private readonly RuntimeConfigService _runtimeConfig;

    public HttpListenerService(ILogger logger, RuntimeConfigService runtimeConfig)
    {
        _logger = logger.ForContext<HttpListenerService>();
        _runtimeConfig = runtimeConfig;
    }

    public void Start(string host)
    {
        var server = new WebserverLite(new WebserverSettings(host, 5002), DefaultRoute);

        server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/runtime_config", GetDynamicConfig);
        server.Routes.PreAuthentication.Parameter.Add(WatsonWebserver.Core.HttpMethod.POST, "/runtime_config/{key}", UpdateDynamicConfig);

        server.Start();
    }

    private async Task DefaultRoute(HttpContextBase ctx)
        => await ctx.Response.Send("hellorld");

    private async Task GetDynamicConfig(HttpContextBase ctx)
    {
        var config = _runtimeConfig.GetAll();
        ctx.Response.Headers.Add("content-type", "application/json");
        await ctx.Response.Send(JsonConvert.SerializeObject(config));
    }

    private async Task UpdateDynamicConfig(HttpContextBase ctx)
    {
        var key = ctx.Request.Url.Parameters["key"];
        var value = ctx.Request.DataAsString;
        await _runtimeConfig.Set(key, value);
        await GetDynamicConfig(ctx);
    }
}
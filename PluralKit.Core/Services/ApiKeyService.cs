using Autofac;

using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using Serilog;

namespace PluralKit.Core;

public class ApiKeyService
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly CoreConfig _cfg;
    private readonly ILifetimeScope _provider;

    public ApiKeyService(ILogger logger, ILifetimeScope provider, CoreConfig cfg)
    {
        _logger = logger;
        _cfg = cfg;
        _provider = provider;

        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("User-Agent", "PluralKitInternal");
    }

    public async Task<string?> CreateUserApiKey(SystemId systemId, string keyName, string[] keyScopes, bool check = false)
    {
        if (_cfg.InternalApiBaseUrl == null || _cfg.InternalApiToken == null)
            throw new Exception("internal API config not set!");

        if (!Uri.TryCreate(new Uri(_cfg.InternalApiBaseUrl), "/internal/apikey/user", out var uri))
            throw new Exception("internal API base invalid!?");

        var repo = _provider.Resolve<ModelRepository>();
        var system = await repo.GetSystem(systemId);
        if (system == null)
            return null;

        var reqData = new JObject();
        reqData.Add("check", check);
        reqData.Add("system", system.Id.Value);
        reqData.Add("name", keyName);
        reqData.Add("scopes", new JArray(keyScopes));

        var req = new HttpRequestMessage()
        {
            RequestUri = uri,
            Method = HttpMethod.Post,
            Content = new StringContent(JsonConvert.SerializeObject(reqData), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("X-Pluralkit-InternalAuth", _cfg.InternalApiToken);

        var res = await _client.SendAsync(req);
        var data = JsonConvert.DeserializeObject<JObject>(await res.Content.ReadAsStringAsync());

        if (data.ContainsKey("error"))
            throw new Exception($"API key validation failed: {(data.Value<string>("error"))}");

        if (data.Value<bool>("valid") != true)
            throw new Exception("API key validation failed: unknown error");

        if (!data.ContainsKey("token"))
            return null;

        return data.Value<string>("token");
    }
}
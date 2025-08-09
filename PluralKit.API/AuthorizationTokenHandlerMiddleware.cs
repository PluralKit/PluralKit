using Dapper;

using PluralKit.Core;

namespace PluralKit.API;

public class AuthorizationTokenHandlerMiddleware
{
    private readonly RequestDelegate _next;

    public AuthorizationTokenHandlerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx, IDatabase db, ApiConfig cfg)
    {
        if (cfg.TrustAuth
            && ctx.Request.Headers.TryGetValue("X-PluralKit-SystemId", out var sidHeaders)
            && sidHeaders.Count > 0
            && int.TryParse(sidHeaders[0], out var systemId))
            ctx.Items.Add("SystemId", new SystemId(systemId));

        if (cfg.TrustAuth
            && ctx.Request.Headers.TryGetValue("X-PluralKit-AppId", out var aidHeaders)
            && aidHeaders.Count > 0
            && int.TryParse(aidHeaders[0], out var appId))
            ctx.Items.Add("AppId", appId);

        await _next.Invoke(ctx);
    }
}
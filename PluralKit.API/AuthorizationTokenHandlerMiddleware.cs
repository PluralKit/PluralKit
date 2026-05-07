using System.Security.Cryptography;
using System.Text;

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
        var authorized = ctx.Request.Headers.TryGetValue("x-pluralkit-internalauth", out var values)
            && values.Count > 0
            && values[0] is string provided
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(provided),
                Encoding.UTF8.GetBytes(cfg.InternalAuthToken));

        if (!authorized)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsync("unauthorized");
            return;
        }

        if (ctx.Request.Headers.TryGetValue("X-PluralKit-SystemId", out var sidHeaders)
            && sidHeaders.Count > 0
            && int.TryParse(sidHeaders[0], out var systemId))
            ctx.Items.Add("SystemId", new SystemId(systemId));

        if (ctx.Request.Headers.TryGetValue("X-PluralKit-AppId", out var aidHeaders)
            && aidHeaders.Count > 0
            && int.TryParse(aidHeaders[0], out var appId))
            ctx.Items.Add("AppId", appId);

        await _next.Invoke(ctx);
    }
}
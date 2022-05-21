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

    public async Task Invoke(HttpContext ctx, IDatabase db)
    {
        ctx.Request.Headers.TryGetValue("authorization", out var authHeaders);
        if (authHeaders.Count > 0)
        {
            var systemId = await db.Execute(conn => conn.QuerySingleOrDefaultAsync<SystemId?>(
                "select id from systems where token = @token",
                new { token = authHeaders[0] }
            ));

            if (systemId != null)
                ctx.Items.Add("SystemId", systemId);
        }

        await _next.Invoke(ctx);
    }
}
using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

using Dapper;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PluralKit.Core;

namespace PluralKit.API
{
    public class SystemTokenAuthenticationHandler: AuthenticationHandler<SystemTokenAuthenticationHandler.Opts>
    {
        private readonly IDatabase _db;
        
        public SystemTokenAuthenticationHandler(IOptionsMonitor<Opts> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IDatabase db): base(options, logger, encoder, clock)
        {
            _db = db;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.NoResult();

            var token = Request.Headers["Authorization"].FirstOrDefault();
            var systemId = await _db.Execute(c => c.QuerySingleOrDefaultAsync<SystemId?>("select id from systems where token = @token", new { token }));
            if (systemId == null) return AuthenticateResult.Fail("Invalid system token");

            var claims = new[] {new Claim(PKClaims.SystemId, systemId.Value.Value.ToString())};
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            ticket.Properties.IsPersistent = false;
            ticket.Properties.AllowRefresh = false;
            return AuthenticateResult.Success(ticket);
        }

        public class Opts: AuthenticationSchemeOptions
        {
            
        }
    }
}
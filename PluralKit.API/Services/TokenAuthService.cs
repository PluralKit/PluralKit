using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Microsoft.AspNetCore.Http;

using PluralKit.Core;

namespace PluralKit.API
{
    public class TokenAuthService: IMiddleware
    {
        public PKSystem CurrentSystem { get; set; }

        private readonly IDatabase _db;

        public TokenAuthService(IDatabase db)
        {
            _db = db;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault();
            if (token != null)
            {
                CurrentSystem = await _db.Execute(c => c.QueryFirstOrDefaultAsync("select * from systems where token = @token", new { token }));
            }
            
            await next.Invoke(context);
            CurrentSystem = null;
        }

        public LookupContext ContextFor(PKSystem system) => 
            system.Id == CurrentSystem?.Id ? LookupContext.ByOwner : LookupContext.API;
            
        public LookupContext ContextFor(PKMember member) => 
            member.System == CurrentSystem?.Id ? LookupContext.ByOwner : LookupContext.API;
    }
}
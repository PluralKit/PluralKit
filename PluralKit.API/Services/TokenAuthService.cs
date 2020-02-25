using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

using PluralKit.Core;

namespace PluralKit.API
{
    public class TokenAuthService: IMiddleware
    {
        public PKSystem CurrentSystem { get; set; }

        private IDataStore _data;

        public TokenAuthService(IDataStore data)
        {
            _data = data;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault();
            if (token != null)
            {
                CurrentSystem = await _data.GetSystemByToken(token);
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
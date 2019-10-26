using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

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
    }
}
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace PluralKit.API
{
    public class TokenAuthService: IMiddleware
    {
        public PKSystem CurrentSystem { get; set; }

        private SystemStore _systems;

        public TokenAuthService(SystemStore systems)
        {
            _systems = systems;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault();
            if (token != null)
            {
                CurrentSystem = await _systems.GetByToken(token);
            }
            
            await next.Invoke(context);
            CurrentSystem = null;
        }
    }
}
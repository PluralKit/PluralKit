using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace PluralKit.API
{
    public class RequiresSystemAttribute: ActionFilterAttribute
    {

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var auth = context.HttpContext.RequestServices.GetRequiredService<TokenAuthService>();
            if (auth.CurrentSystem == null)
            {
                context.Result = new UnauthorizedObjectResult("Invalid or missing token in Authorization header.");
                return;
            }

            await base.OnActionExecutionAsync(context, next);
        }
    }
}
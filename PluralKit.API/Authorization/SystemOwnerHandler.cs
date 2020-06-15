using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using PluralKit.Core;

namespace PluralKit.API
{
    public class SystemOwnerHandler: AuthorizationHandler<OwnSystemRequirement, PKSystem>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                       OwnSystemRequirement requirement, PKSystem resource)
        {
            if (!context.User.Identity.IsAuthenticated) return Task.CompletedTask;
            if (resource.Id == context.User.CurrentSystem()) 
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using PluralKit.Core;

namespace PluralKit.API
{
    public class GroupOwnerHandler: AuthorizationHandler<OwnSystemRequirement, PKGroup> {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                       OwnSystemRequirement requirement, PKGroup resource)
        {
            if (!context.User.Identity.IsAuthenticated) return Task.CompletedTask;
            if (resource.System == context.User.CurrentSystem())
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}
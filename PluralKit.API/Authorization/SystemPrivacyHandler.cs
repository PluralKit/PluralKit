using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using PluralKit.Core;

namespace PluralKit.API
{
    public class SystemPrivacyHandler: AuthorizationHandler<PrivacyRequirement<PKSystem>, PKSystem>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                       PrivacyRequirement<PKSystem> requirement, PKSystem resource)
        {
            var level = requirement.Mapper(resource);
            var ctx = context.User.ContextFor(resource);
            if (level.CanAccess(ctx))
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}
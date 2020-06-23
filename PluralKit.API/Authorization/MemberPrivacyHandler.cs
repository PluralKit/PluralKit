using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

using PluralKit.Core;

namespace PluralKit.API
{
    public class MemberPrivacyHandler: AuthorizationHandler<PrivacyRequirement<PKMember>, PKMember>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                       PrivacyRequirement<PKMember> requirement, PKMember resource)
        {
            var level = requirement.Mapper(resource);
            var ctx = context.User.ContextFor(resource);
            if (level.CanAccess(ctx))
                context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}
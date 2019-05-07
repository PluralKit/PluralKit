using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace PluralKit.Bot {
    class MustHaveSystem : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var c = context as PKCommandContext;
            if (c == null) return PreconditionResult.FromError("Must be called on a PKCommandContext (should never happen!)");
            if (c.SenderSystem == null) return PreconditionResult.FromError(Errors.NoSystemError);
            return PreconditionResult.FromSuccess();
        }
    }

    class MustPassOwnMember : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            // OK when:
            // - Sender has a system
            // - Sender passes a member as a context parameter
            // - Sender owns said member 

            var c = context as PKCommandContext;
            if (c == null) 
            if (c.SenderSystem == null) return PreconditionResult.FromError(Errors.NoSystemError);
            if (c.GetContextEntity<PKMember>() == null) return PreconditionResult.FromError(Errors.MissingMemberError);
            if (c.GetContextEntity<PKMember>().System != c.SenderSystem.Id) return PreconditionResult.FromError(Errors.NotOwnMemberError);
            return PreconditionResult.FromSuccess();
        }
    }
}
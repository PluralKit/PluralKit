using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace PluralKit.Bot {
    class MustHaveSystem : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var c = context as PKCommandContext;
            if (c == null) return Task.FromResult(PreconditionResult.FromError("Must be called on a PKCommandContext (should never happen!)"))    ;
            if (c.SenderSystem == null) return Task.FromResult(PreconditionResult.FromError(Errors.NoSystemError));
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }

    class MustPassOwnMember : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            // OK when:
            // - Sender has a system
            // - Sender passes a member as a context parameter
            // - Sender owns said member 

            var c = context as PKCommandContext;
            if (c.SenderSystem == null) return Task.FromResult(PreconditionResult.FromError(Errors.NoSystemError));
            if (c.GetContextEntity<PKMember>() == null) return Task.FromResult(PreconditionResult.FromError(Errors.MissingMemberError));
            if (c.GetContextEntity<PKMember>().System != c.SenderSystem.Id) return Task.FromResult(PreconditionResult.FromError(Errors.NotOwnMemberError));
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
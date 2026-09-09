using Autofac;

using Myriad.Extensions;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class InteractionContextChecksExt
{
    public static InteractionContext CheckNoSystem(this InteractionContext ctx)
    {
        if (ctx.System != null)
            throw Errors.ExistingSystemError("/");
        return ctx;
    }
}
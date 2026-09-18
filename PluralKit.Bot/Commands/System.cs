using PluralKit.Core;

namespace PluralKit.Bot;

using Myriad.Builders;
using Myriad.Types;

public class System
{
    private readonly EmbedService _embeds;
    private readonly SystemLogic _logic;

    public System(EmbedService embeds, SystemLogic logic)
    {
        _embeds = embeds;
        _logic = logic;
    }

    public async Task Query(Context ctx, PKSystem system)
    {
        await _logic.Query(ctx, system);
    }

    public async Task New(Context ctx)
    {
        var systemName = ctx.RemainderOrNull();

        await _logic.New(ctx, systemName);
    }

    public async Task DisplayId(Context ctx, PKSystem target)
    {
        if (target == null)
            throw Errors.NoSystemError(ctx.DefaultPrefix);

        await ctx.Reply(target.DisplayHid(ctx.Config));
    }
}
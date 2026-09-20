using PluralKit.Core;

namespace PluralKit.Bot;

public class System
{
    private readonly SystemCommandService _systemCommand;

    public System(SystemCommandService systemCommand)
    {
        _systemCommand = systemCommand;
    }

    public async Task Query(Context ctx, PKSystem system)
    {
        await _systemCommand.Query(ctx, system);
    }

    public async Task New(Context ctx)
    {
        var systemName = ctx.RemainderOrNull();

        await _systemCommand.New(ctx, systemName);
    }

    public async Task DisplayId(Context ctx, PKSystem target)
    {
        if (target == null)
            throw Errors.NoSystemError(ctx.DefaultPrefix);

        await ctx.Reply(target.DisplayHid(ctx.Config));
    }
}
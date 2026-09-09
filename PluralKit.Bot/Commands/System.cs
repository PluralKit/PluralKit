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
        if (system == null) throw Errors.NoSystemError(ctx.DefaultPrefix);
        if (ctx.MatchFlag("show-embed", "se"))
        {
            await ctx.Reply(text: EmbedService.LEGACY_EMBED_WARNING, embed: await _embeds.CreateSystemEmbed(ctx, system, ctx.LookupContextFor(system.Id)));
            return;
        }

        await ctx.Reply(components: await _embeds.CreateSystemMessageComponents(ctx, system, ctx.LookupContextFor(system.Id)));
    }

    public async Task New(Context ctx)
    {
        ctx.CheckNoSystem();

        var systemName = ctx.RemainderOrNull();

        var res = await _logic.New(ctx.Author.Id, ctx.DefaultPrefix, systemName, legacyEmbed: ctx.MatchFlag("show-embed", "se"));

        await ctx.Reply(components: [res]);

    }

    public async Task DisplayId(Context ctx, PKSystem target)
    {
        if (target == null)
            throw Errors.NoSystemError(ctx.DefaultPrefix);

        await ctx.Reply(target.DisplayHid(ctx.Config));
    }
}
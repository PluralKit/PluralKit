using PluralKit.Core;

namespace PluralKit.Bot;

public class System
{
    private readonly EmbedService _embeds;

    public System(EmbedService embeds, ModelRepository repo)
    {
        _embeds = embeds;
    }

    public async Task Query(Context ctx, PKSystem system)
    {
        if (system == null) throw Errors.NoSystemError;

        await ctx.Reply(embed: await _embeds.CreateSystemEmbed(ctx, system, ctx.LookupContextFor(system.Id)));
    }

    public async Task New(Context ctx)
    {
        ctx.CheckNoSystem();

        var systemName = ctx.RemainderOrNull();
        if (systemName != null && systemName.Length > Limits.MaxSystemNameLength)
            throw Errors.StringTooLongError("System name", systemName.Length, Limits.MaxSystemNameLength);

        var system = await ctx.Repository.CreateSystem(systemName);
        await ctx.Repository.AddAccount(system.Id, ctx.Author.Id);

        // TODO: better message, perhaps embed like in groups?
        await ctx.Reply(
            $"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;system help` for more information about commands you can use now. Now that you have that set up, check out the getting started guide on setting up members and proxies: <https://pluralkit.me/start>");
    }

    public async Task DisplayId(Context ctx, PKSystem target)
    {
        if (target == null)
            throw Errors.NoSystemError;

        await ctx.Reply(target.DisplayHid(ctx.Config));
    }
}
using PluralKit.Core;

namespace PluralKit.Bot;

public class ApplicationCommandSystemEdit
{
    private readonly SystemEditLogic _logic;

    public ApplicationCommandSystemEdit(SystemEditLogic logic)
    {
        _logic = logic;
    }

    public async Task SetShowClear(InteractionContext ctx, Func<InteractionContext, string, Task> setFunc, Func<InteractionContext, Task> clearFunc, Func<InteractionContext, PKSystem, ReplyFormat, Task> showFunc, string field)
    {
        switch (ctx.Subcommand(true).Name)
        {
            case "set":
                await Set();
                break;
            case "clear":
                await Clear();
                break;
            default:
                await Show();
                break;
        }

        async Task Set()
        {
            ctx.CheckSystem();
            await setFunc(ctx, ctx.OptionValue(field).ToString());
        }
        async Task Clear()
        {
            ctx.CheckSystem();
            await clearFunc(ctx);
        }
        async Task Show()
        {
            await showFunc(ctx, await ctx.MatchSystem(), ctx.MatchFormat());
        }
    }
    public async Task GuildSetShowClear(InteractionContext ctx, Func<InteractionContext, string, ulong, Task> setFunc, Func<InteractionContext, ulong, Task> clearFunc, Func<InteractionContext, PKSystem, ReplyFormat, ulong, Task> showFunc, string field)
    {
        var serverIdStr = ctx.OptionValue("server-id")?.ToString();
        _ = ulong.TryParse(serverIdStr, out var serverId);

        switch (ctx.Subcommand(true).Name)
        {
            case "set":
                await Set();
                break;
            case "clear":
                await Clear();
                break;
            default:
                await Show();
                break;
        }

        async Task Set()
        {
            ctx.CheckSystem();
            await setFunc(ctx, ctx.OptionValue(field).ToString(), serverId);
        }
        async Task Clear()
        {
            ctx.CheckSystem();
            await clearFunc(ctx, serverId);
        }
        async Task Show()
        {
            await showFunc(ctx, await ctx.MatchSystem(), ctx.MatchFormat(), serverId);
        }
    }

    public async Task Name(InteractionContext ctx) =>
        await SetShowClear(ctx, _logic.SetName, _logic.ClearName, _logic.ShowName, "name");

    public async Task ServerName(InteractionContext ctx) =>
        await GuildSetShowClear(ctx, _logic.SetServerName, _logic.ClearServerName, _logic.ShowServerName, "servername");

    public async Task Tag(InteractionContext ctx) =>
        await SetShowClear(ctx, _logic.SetTag, _logic.ClearTag, _logic.ShowTag, "tag");

    public async Task Description(InteractionContext ctx) =>
        await SetShowClear(ctx, _logic.SetDescription, _logic.ClearDescription, _logic.ShowDescription, "description");

}
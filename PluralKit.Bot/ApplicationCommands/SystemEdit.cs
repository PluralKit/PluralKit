using PluralKit.Core;

namespace PluralKit.Bot;

public class ApplicationCommandSystemEdit
{
    private readonly SystemEditCommandService _systemEditCommand;

    public ApplicationCommandSystemEdit(SystemEditCommandService systemEditCommand)
    {
        _systemEditCommand = systemEditCommand;
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
        await SetShowClear(ctx, _systemEditCommand.SetName, _systemEditCommand.ClearName, _systemEditCommand.ShowName, "name");

    public async Task ServerName(InteractionContext ctx) =>
        await GuildSetShowClear(ctx, _systemEditCommand.SetServerName, _systemEditCommand.ClearServerName, _systemEditCommand.ShowServerName, "servername");

    public async Task Tag(InteractionContext ctx) =>
        await SetShowClear(ctx, _systemEditCommand.SetTag, _systemEditCommand.ClearTag, _systemEditCommand.ShowTag, "tag");

    public async Task Description(InteractionContext ctx) =>
        await SetShowClear(ctx, _systemEditCommand.SetDescription, _systemEditCommand.ClearDescription, _systemEditCommand.ShowDescription, "description");

}
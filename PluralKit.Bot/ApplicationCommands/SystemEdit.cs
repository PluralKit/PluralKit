namespace PluralKit.Bot;

public class ApplicationCommandSystemEdit
{
    private readonly SystemEditLogic _logic;

    public ApplicationCommandSystemEdit(SystemEditLogic logic)
    {
        _logic = logic;
    }

    public async Task Tag(InteractionContext ctx)
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
            await _logic.SetTag(ctx, ctx.OptionValue("tag").ToString());
        }
        async Task Clear()
        {
            ctx.CheckSystem();
            await _logic.ClearTag(ctx);
        }
        async Task Show()
        {
            await _logic.ShowTag(ctx, await ctx.MatchSystem());
        }
    }
}
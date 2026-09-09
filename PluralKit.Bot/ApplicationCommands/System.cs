namespace PluralKit.Bot;

public class ApplicationCommandSystem
{
    private readonly SystemLogic _logic;

    public ApplicationCommandSystem(SystemLogic logic)
    {
        _logic = logic;
    }

    public async Task New(InteractionContext ctx)
    {
        ctx.CheckNoSystem();

        var systemName = Convert.ToString(ctx.Event.Data!.Options[0].Options[0]?.Value);

        var res = await _logic.New(ctx.User.Id, "/", systemName, slashcommand: true);

        await ctx.Reply(components: [res]);

    }

}
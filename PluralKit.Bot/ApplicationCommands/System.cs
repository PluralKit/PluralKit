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

        var systemName = ctx.OptionValue("name")?.ToString();

        await _logic.New(ctx, systemName, slashcommand: true);

    }

}
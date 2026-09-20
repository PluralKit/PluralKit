namespace PluralKit.Bot;

public class ApplicationCommandSystem
{
    private readonly SystemCommandService _systemCommand;

    public ApplicationCommandSystem(SystemCommandService systemCommand)
    {
        _systemCommand = systemCommand;
    }

    public async Task New(InteractionContext ctx)
    {
        var systemName = ctx.OptionValue("name")?.ToString();

        await _systemCommand.New(ctx, systemName, slashcommand: true);
    }

    public async Task Query(InteractionContext ctx)
    {
        await _systemCommand.Query(ctx, await ctx.MatchSystem());
    }

}
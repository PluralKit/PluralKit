using ApplicationCommandType = Myriad.Types.ApplicationCommand.ApplicationCommandType;
using InteractionType = Myriad.Types.Interaction.InteractionType;

namespace PluralKit.Bot;

public partial class ApplicationCommandTree
{
    public Task TryHandleCommand(InteractionContext ctx)
    {
        if (ctx.Event.Data!.Name == ProxiedMessageQuery.Name)
            return ctx.Execute<ApplicationCommandProxiedMessage>(ProxiedMessageQuery, m => m.QueryMessage(ctx));
        else if (ctx.Event.Data!.Name == ProxiedMessageDelete.Name)
            return ctx.Execute<ApplicationCommandProxiedMessage>(ProxiedMessageDelete, m => m.DeleteMessage(ctx));
        else if (ctx.Event.Data!.Name == ProxiedMessagePing.Name)
            return ctx.Execute<ApplicationCommandProxiedMessage>(ProxiedMessageDelete, m => m.PingMessageAuthor(ctx));

        return null;
    }
}
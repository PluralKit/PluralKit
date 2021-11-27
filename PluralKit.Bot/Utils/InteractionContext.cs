using Autofac;

using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

namespace PluralKit.Bot;

public class InteractionContext
{
    private readonly ILifetimeScope _services;

    public InteractionContext(InteractionCreateEvent evt, ILifetimeScope services)
    {
        Event = evt;
        _services = services;
    }

    public InteractionCreateEvent Event { get; }

    public ulong ChannelId => Event.ChannelId;
    public ulong? MessageId => Event.Message?.Id;
    public GuildMember? Member => Event.Member;
    public User User => Event.Member?.User ?? Event.User;
    public string Token => Event.Token;
    public string? CustomId => Event.Data?.CustomId;

    public async Task Reply(string content)
    {
        await Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData { Content = content, Flags = Message.MessageFlags.Ephemeral });
    }

    public async Task Ignore()
    {
        await Respond(InteractionResponse.ResponseType.DeferredUpdateMessage,
            new InteractionApplicationCommandCallbackData
            {
                // Components = _evt.Message.Components
            });
    }

    public async Task Acknowledge()
    {
        await Respond(InteractionResponse.ResponseType.UpdateMessage,
            new InteractionApplicationCommandCallbackData { Components = Array.Empty<MessageComponent>() });
    }

    public async Task Respond(InteractionResponse.ResponseType type,
                              InteractionApplicationCommandCallbackData? data)
    {
        var rest = _services.Resolve<DiscordApiClient>();
        await rest.CreateInteractionResponse(Event.Id, Event.Token,
            new InteractionResponse { Type = type, Data = data });
    }
}
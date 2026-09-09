using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class InteractionContext: JointContext
{

    public InteractionContext(ILifetimeScope provider, InteractionCreateEvent evt, PKSystem system, SystemConfig config) : base(provider, system, config)
    {
        Event = evt;
        Member = Event.Member;
        Author = Event.Member?.User ?? Event.User;
    }

    public InteractionCreateEvent Event { get; }

    public ulong GuildId => Event.GuildId;
    public ulong ChannelId => Event.ChannelId;
    public ulong? MessageId => Event.Message?.Id;
    public string Token => Event.Token;
    public string? CustomId => Event.Data?.CustomId;

    public async Task Execute<T>(ApplicationCommand? command, Func<T, Task> handler)
    {
        try
        {
            using (_metrics.Measure.Timer.Time(BotMetrics.ApplicationCommandTime, new MetricTags("Application command", command?.Name ?? "null")))
                await handler(_provider.Resolve<T>());

            _metrics.Measure.Meter.Mark(BotMetrics.ApplicationCommandsRun);
        }
        catch (PKError e)
        {
            await Reply($"{Emojis.Error} {e.Message}");
        }
        catch (TimeoutException)
        {
            // Got a complaint the old error was a bit too patronizing. Hopefully this is better?
            await Reply($"{Emojis.Error} Operation timed out, sorry. Try again, perhaps?");
        }
    }

    public async override Task Reply(string text = null, Embed embed = null, AllowedMentions? mentions = null, MultipartFile[]? files = null)
    {
        await Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData
            {
                Content = text,
                Embeds = embed != null ? new[] { embed } : null,
                Flags = Message.MessageFlags.Ephemeral
            });
    }

    public async override Task Reply(MessageComponent[] components = null, AllowedMentions? mentions = null, MultipartFile[]? files = null)
    {
        await Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData
            {
                Components = components,
                Flags = Message.MessageFlags.Ephemeral | Message.MessageFlags.IsComponentsV2,
                AllowedMentions = mentions ?? new AllowedMentions()
            });
    }

    public async Task Defer()
    {
        await Respond(InteractionResponse.ResponseType.DeferredChannelMessageWithSource,
            new InteractionApplicationCommandCallbackData
            {
                Components = Array.Empty<MessageComponent>(),
                Flags = Message.MessageFlags.Ephemeral,
            });
    }

    public async Task Ignore()
    {
        await Respond(InteractionResponse.ResponseType.DeferredUpdateMessage,
            new InteractionApplicationCommandCallbackData
            {
                Components = Event.Message?.Components ?? Array.Empty<MessageComponent>()
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
        await Rest.CreateInteractionResponse(Event.Id, Event.Token,
            new InteractionResponse { Type = type, Data = data });
    }
}
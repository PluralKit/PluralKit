using App.Metrics;

using Autofac;
using Myriad.Gateway;
using Myriad.Rest.Types;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class InteractionContext: JointContext
{

    public InteractionContext(ILifetimeScope provider, InteractionCreateEvent evt, PKSystem system, SystemConfig config) : base(provider, system, config, ["/"], evt.Guild)
    {
        Event = evt;
        Member = Event.Member;
        Author = Event.Member?.User ?? Event.User;
    }

    public InteractionCreateEvent Event { get; }

    public ulong ChannelId => Event.ChannelId;
    public ulong? MessageId => Event.Message?.Id;
    public string Token => Event.Token;
    public string? CustomId => Event.Data?.CustomId;


    /// <summary>
    /// Finds the value of a given fillable parameter
    /// </summary>
    public object OptionValue(string name)
    {
        // This doesn't technically need to be recursive like this since right now
        // it can only go 4 layers deep (Command, SubCommandGroup, SubCommand, Options)
        // But I think it makes sense so we don't need to worry so much about finding which
        // layer we need to go to and it's possible those layers could be able to be nested
        // further in the future
        return OptionValueInner(name, Event.Data!.Options);
    }
    private object OptionValueInner(string name, ApplicationCommandInteractionDataOption[] container)
    {
        foreach (var opt in container)
        {
            // If opt.Options is null that means we've hit a parameter
            // (Options and Value are mutually exclusive. We could just as easily check if opt.Value is not null)
            // If it's the correct one return its value
            if (opt.Options == null && opt.Name == name)
                return opt.Value;
            // If Options isn't null that means we need to go a layer deeper to find the parameters
            // If it is we continue to the next one in the same layer
            if (opt.Options != null)
            {
                var recurse = OptionValueInner(name, opt.Options);
                if (recurse != null)
                    return recurse;
            }
        }
        return null;
    }

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

    public override LookupContext LookupContextFor(SystemId systemId) => DirectLookupContextFor(systemId);
}
using System;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using NodaTime;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Context
{
    private readonly ILifetimeScope _provider;

    private readonly IMetrics _metrics;
    private readonly CommandMessageService _commandMessageService;

    private Command? _currentCommand;

    public Context(ILifetimeScope provider, int shardId, Guild? guild, Channel channel, MessageCreateEvent message,
                                                    int commandParseOffset, PKSystem senderSystem, SystemConfig config)
    {
        Message = (Message)message;
        ShardId = shardId;
        Guild = guild;
        Channel = channel;
        System = senderSystem;
        Config = config;
        Cache = provider.Resolve<IDiscordCache>();
        Database = provider.Resolve<IDatabase>();
        Repository = provider.Resolve<ModelRepository>();
        _metrics = provider.Resolve<IMetrics>();
        _provider = provider;
        _commandMessageService = provider.Resolve<CommandMessageService>();
        CommandPrefix = message.Content?.Substring(0, commandParseOffset);
        Parameters = new Parameters(message.Content?.Substring(commandParseOffset));
        Rest = provider.Resolve<DiscordApiClient>();
        Cluster = provider.Resolve<Cluster>();
    }

    public readonly IDiscordCache Cache;
    public readonly DiscordApiClient Rest;

    public readonly Channel Channel;
    public User Author => Message.Author;
    public GuildMemberPartial Member => ((MessageCreateEvent)Message).Member;

    public readonly Message Message;
    public readonly Guild Guild;
    public readonly int ShardId;
    public readonly Cluster Cluster;

    public Task<PermissionSet> BotPermissions => Cache.PermissionsIn(Channel.Id);
    public Task<PermissionSet> UserPermissions => Cache.PermissionsFor((MessageCreateEvent)Message);


    public readonly PKSystem System;
    public readonly SystemConfig Config;
    public DateTimeZone Zone => Config?.Zone ?? DateTimeZone.Utc;

    public readonly string CommandPrefix;
    public readonly Parameters Parameters;

    internal readonly IDatabase Database;
    internal readonly ModelRepository Repository;

    public async Task<Message> Reply(string text = null, Embed embed = null, AllowedMentions? mentions = null)
    {
        var botPerms = await BotPermissions;

        if (!botPerms.HasFlag(PermissionSet.SendMessages))
            // Will be "swallowed" during the error handler anyway, this message is never shown.
            throw new PKError("PluralKit does not have permission to send messages in this channel.");

        if (embed != null && !botPerms.HasFlag(PermissionSet.EmbedLinks))
            throw new PKError("PluralKit does not have permission to send embeds in this channel. Please ensure I have the **Embed Links** permission enabled.");

        var msg = await Rest.CreateMessage(Channel.Id, new MessageRequest
        {
            Content = text,
            Embeds = embed != null ? new[] { embed } : null,
            // Default to an empty allowed mentions object instead of null (which means no mentions allowed)
            AllowedMentions = mentions ?? new AllowedMentions()
        });

        // if (embed != null)
        // {
        // Sensitive information that might want to be deleted by :x: reaction is typically in an embed format (member cards, for example)
        // but since we can, we just store all sent messages for possible deletion
        await _commandMessageService.RegisterMessage(msg.Id, msg.ChannelId, Author.Id);
        // }

        return msg;
    }

    public async Task Execute<T>(Command? commandDef, Func<T, Task> handler, bool deprecated = false)
    {
        _currentCommand = commandDef;

        if (deprecated && commandDef != null)
        {
            await Reply($"{Emojis.Warn} This command has been removed. please use `pk;{commandDef.Key}` instead.");
            return;
        }

        try
        {
            using (_metrics.Measure.Timer.Time(BotMetrics.CommandTime, new MetricTags("Command", commandDef?.Key ?? "null")))
                await handler(_provider.Resolve<T>());

            _metrics.Measure.Meter.Mark(BotMetrics.CommandsRun);
        }
        catch (PKSyntaxError e)
        {
            await Reply($"{Emojis.Error} {e.Message}\n**Command usage:**\n> pk;{commandDef?.Usage}");
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

    /// <summary>
    /// Same as LookupContextFor, but skips flags / config checks.
    /// </summary>
    public LookupContext DirectLookupContextFor(SystemId systemId)
        => System?.Id == systemId ? LookupContext.ByOwner : LookupContext.ByNonOwner;

    public LookupContext LookupContextFor(SystemId systemId)
    {
        var hasPrivateOverride = this.MatchFlag("private", "priv");
        var hasPublicOverride = this.MatchFlag("public", "pub");

        if (hasPrivateOverride && hasPublicOverride)
            throw new PKError("Cannot match both public and private flags at the same time.");

        if (System?.Id != systemId)
        {
            if (hasPrivateOverride)
                throw Errors.NotOwnInfo;
            return LookupContext.ByNonOwner;
        }

        if (hasPrivateOverride)
            return LookupContext.ByOwner;
        if (hasPublicOverride)
            return LookupContext.ByNonOwner;

        return Config.ShowPrivateInfo
            ? LookupContext.ByOwner
            : LookupContext.ByNonOwner;
    }

    public IComponentContext Services => _provider;
}
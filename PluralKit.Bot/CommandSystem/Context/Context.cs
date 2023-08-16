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
        Redis = provider.Resolve<RedisService>();
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
    internal readonly RedisService Redis;

    public async Task<Message> Reply(string text = null, Embed embed = null, Embed[] embeds = null, AllowedMentions? mentions = null)
    {
        var botPerms = await BotPermissions;

        if (!botPerms.HasFlag(PermissionSet.SendMessages))
            // Will be "swallowed" during the error handler anyway, this message is never shown.
            throw new PKError("PluralKit does not have permission to send messages in this channel.");

        if ((embed != null || embeds != null) && !botPerms.HasFlag(PermissionSet.EmbedLinks))
            throw new PKError("PluralKit does not have permission to send embeds in this channel. Please ensure I have the **Embed Links** permission enabled.");

        var embedArr = embeds ?? (embed != null ? new[] { embed } : null);

        var msg = await Rest.CreateMessage(Channel.Id, new MessageRequest
        {
            Content = text,
            Embeds = embedArr,
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
    public async Task<LookupContext> DirectLookupContextFor(SystemId systemId)
    {
        var trusted = await this.CheckTrusted(system: systemId);
        if (System?.Id == systemId)
            return LookupContext.ByOwner;
        if (trusted)
            return LookupContext.ByTrusted;
        return LookupContext.ByNonOwner;
    }

    public async Task<LookupContext> LookupContextFor(SystemId targetSystemId)
    {
        var trusted = await this.CheckTrusted(system: targetSystemId);

        var hasPrivateOverride = this.MatchFlag("private", "priv");
        var hasPublicOverride = this.MatchFlag("public", "pub");
        var hasTrustedOverride = this.MatchFlag("trusted", "tru");

        var overrideCount = new[] { hasPrivateOverride, hasPublicOverride, hasTrustedOverride }.Count(e => e);

        if (overrideCount > 1)
            throw new PKError("Cannot match more than one type of privacy flag (`-private`, `-public`, `-trusted`) at once.");

        if (hasPrivateOverride)
        {
            if (System?.Id == targetSystemId)
                return LookupContext.ByOwner;
        }
        else if (hasTrustedOverride)
        {
            if (System?.Id == targetSystemId || trusted)
                return LookupContext.ByTrusted;
            throw Errors.NotTrusted;
        }
        if (hasPublicOverride)
        {
            return LookupContext.ByNonOwner;
        }

        if (System?.Id == targetSystemId)
        {
            return Config.ShowPrivateInfo ? LookupContext.ByOwner : LookupContext.ByNonOwner;
            /*return Config.DefaultPrivacyShown switch
            {
                PrivacyLevel.Private => LookupContext.ByOwner,
                PrivacyLevel.Public => LookupContext.ByNonOwner,
                PrivacyLevel.Trusted => LookupContext.ByTrusted,
                _ => LookupContext.ByNonOwner,
            };*/
        }

        if (trusted)
        {
            return Config.ShowPrivateInfo ? LookupContext.ByTrusted : LookupContext.ByNonOwner;
            /*return Config.DefaultPrivacyShown switch
            {
                PrivacyLevel.Private => LookupContext.ByTrusted,
                PrivacyLevel.Trusted => LookupContext.ByTrusted,
                PrivacyLevel.Public => LookupContext.ByNonOwner,
                _ => LookupContext.ByNonOwner
            };*/
        }
        return LookupContext.ByNonOwner;
    }

    public IComponentContext Services => _provider;
}
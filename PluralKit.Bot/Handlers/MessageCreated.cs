using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class MessageCreated: IEventHandler<MessageCreateEvent>
{
    private readonly Bot _bot;
    private readonly IDiscordCache _cache;
    private readonly Cluster _cluster;
    private readonly BotConfig _config;
    private readonly IDatabase _db;
    private readonly LastMessageCacheService _lastMessageCache;
    private readonly LoggerCleanService _loggerClean;
    private readonly IMetrics _metrics;
    private readonly ProxyService _proxy;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;
    private readonly ILifetimeScope _services;
    private readonly CommandTree _tree;
    private readonly PrivateChannelService _dmCache;

    public MessageCreated(LastMessageCacheService lastMessageCache, LoggerCleanService loggerClean,
                          IMetrics metrics, ProxyService proxy,
                          CommandTree tree, ILifetimeScope services, IDatabase db, BotConfig config,
                          ModelRepository repo, IDiscordCache cache,
                          Bot bot, Cluster cluster, DiscordApiClient rest, PrivateChannelService dmCache)
    {
        _lastMessageCache = lastMessageCache;
        _loggerClean = loggerClean;
        _metrics = metrics;
        _proxy = proxy;
        _tree = tree;
        _services = services;
        _db = db;
        _config = config;
        _repo = repo;
        _cache = cache;
        _bot = bot;
        _cluster = cluster;
        _rest = rest;
        _dmCache = dmCache;
    }

    public ulong? ErrorChannelFor(MessageCreateEvent evt, ulong userId) => evt.ChannelId;
    private bool IsDuplicateMessage(Message msg) =>
        // We consider a message duplicate if it has the same ID as the previous message that hit the gateway
        _lastMessageCache.GetLastMessage(msg.ChannelId)?.Current.Id == msg.Id;

    public async Task Handle(int shardId, MessageCreateEvent evt)
    {
        if (evt.Author.Id == await _cache.GetOwnUser()) return;
        if (evt.Type != Message.MessageType.Default && evt.Type != Message.MessageType.Reply) return;
        if (IsDuplicateMessage(evt)) return;

        if (!(await _cache.PermissionsIn(evt.ChannelId)).HasFlag(PermissionSet.SendMessages)) return;

        // spawn off saving the private channel into another thread
        // it is not a fatal error if this fails, and it shouldn't block message processing
        _ = _dmCache.TrySavePrivateChannel(evt);

        var guild = evt.GuildId != null ? await _cache.GetGuild(evt.GuildId.Value) : null;
        var channel = await _cache.GetChannel(evt.ChannelId);
        var rootChannel = await _cache.GetRootChannel(evt.ChannelId);

        // Log metrics and message info
        _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
        _lastMessageCache.AddMessage(evt);

        // Get message context from DB (tracking w/ metrics)
        MessageContext ctx;
        using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
            ctx = await _repo.GetMessageContext(evt.Author.Id, evt.GuildId ?? default, rootChannel.Id);

        // Try each handler until we find one that succeeds
        if (await TryHandleLogClean(evt, ctx))
            return;

        // Only do command/proxy handling if it's a user account
        if (evt.Author.Bot || evt.WebhookId != null || evt.Author.System == true)
            return;

        if (await TryHandleCommand(shardId, evt, guild, channel, ctx))
            return;
        await TryHandleProxy(evt, guild, channel, ctx);
    }

    private async ValueTask<bool> TryHandleLogClean(MessageCreateEvent evt, MessageContext ctx)
    {
        var channel = await _cache.GetChannel(evt.ChannelId);
        if (!evt.Author.Bot || channel.Type != Channel.ChannelType.GuildText ||
            !ctx.LogCleanupEnabled) return false;

        await _loggerClean.HandleLoggerBotCleanup(evt);
        return true;
    }

    private async ValueTask<bool> TryHandleCommand(int shardId, MessageCreateEvent evt, Guild? guild,
                                                   Channel channel, MessageContext ctx)
    {
        var content = evt.Content;
        if (content == null) return false;

        var ourUserId = await _cache.GetOwnUser();

        // Check for command prefix
        if (!HasCommandPrefix(content, ourUserId, out var cmdStart) || cmdStart == content.Length)
            return false;

        if (ctx.IsDeleting)
        {
            await _rest.CreateMessage(evt.ChannelId, new()
            {
                Content = $"{Emojis.Error} Your system is currently being deleted."
                    + " Due to database issues, it is not possible to use commands while a system is being deleted. Please wait a few minutes and try again.",
                MessageReference = new(guild?.Id, channel.Id, evt.Id)
            });
            return true;
        }

        // Trim leading whitespace from command without actually modifying the string
        // This just moves the argPos pointer by however much whitespace is at the start of the post-argPos string
        var trimStartLengthDiff =
            content.Substring(cmdStart).Length - content.Substring(cmdStart).TrimStart().Length;
        cmdStart += trimStartLengthDiff;

        try
        {
            var system = ctx.SystemId != null ? await _repo.GetSystem(ctx.SystemId.Value) : null;
            var config = ctx.SystemId != null ? await _repo.GetSystemConfig(ctx.SystemId.Value) : null;
            await _tree.ExecuteCommand(new Context(_services, shardId, guild, channel, evt, cmdStart, system, config, ctx));
        }
        catch (PKError)
        {
            // Only permission errors will ever bubble this far and be caught here instead of Context.Execute
            // so we just catch and ignore these. TODO: this may need to change.
        }

        return true;
    }

    private bool HasCommandPrefix(string message, ulong currentUserId, out int argPos)
    {
        // First, try prefixes defined in the config
        var prefixes = _config.Prefixes ?? BotConfig.DefaultPrefixes;
        foreach (var prefix in prefixes)
        {
            if (!message.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)) continue;

            argPos = prefix.Length;
            return true;
        }

        // Then, check mention prefix (must be the bot user, ofc)
        argPos = -1;
        if (DiscordUtils.HasMentionPrefix(message, ref argPos, out var id))
            return id == currentUserId;

        return false;
    }

    private async ValueTask<bool> TryHandleProxy(MessageCreateEvent evt, Guild guild, Channel channel,
                                                 MessageContext ctx)
    {
        if (ctx.IsDeleting) return false;

        var botPermissions = await _cache.PermissionsIn(channel.Id);

        try
        {
            return await _proxy.HandleIncomingMessage(evt, ctx, guild, channel, ctx.AllowAutoproxy,
                botPermissions);
        }

        // Catch any failed proxy checks so they get ignored in the global error handler
        catch (ProxyService.ProxyChecksFailedException) { }

        catch (PKError e)
        {
            // User-facing errors, print to the channel properly formatted
            if (botPermissions.HasFlag(PermissionSet.SendMessages))
                await _rest.CreateMessage(evt.ChannelId,
                    new MessageRequest { Content = $"{Emojis.Error} {e.Message}" });
        }

        return false;
    }
}
using System;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MessageCreated: IEventHandler<MessageCreateEvent>
    {
        private readonly Bot _bot;
        private readonly CommandTree _tree;
        private readonly IDiscordCache _cache;
        private readonly LastMessageCacheService _lastMessageCache;
        private readonly LoggerCleanService _loggerClean;
        private readonly IMetrics _metrics;
        private readonly ProxyService _proxy;
        private readonly ILifetimeScope _services;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly BotConfig _config;
        private readonly DiscordApiClient _rest;

        public MessageCreated(LastMessageCacheService lastMessageCache, LoggerCleanService loggerClean,
                              IMetrics metrics, ProxyService proxy,
                              CommandTree tree, ILifetimeScope services, IDatabase db, BotConfig config, ModelRepository repo, IDiscordCache cache, Bot bot, DiscordApiClient rest)
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
            _rest = rest;
        }

        public ulong? ErrorChannelFor(MessageCreateEvent evt) => evt.ChannelId;

        private bool IsDuplicateMessage(Message msg) =>
            // We consider a message duplicate if it has the same ID as the previous message that hit the gateway
            _lastMessageCache.GetLastMessage(msg.ChannelId)?.Id == msg.Id;

        public async Task Handle(Shard shard, MessageCreateEvent evt)
        {
            if (evt.Author.Id == shard.User?.Id) return;
            if (evt.Type != Message.MessageType.Default && evt.Type != Message.MessageType.Reply) return;
            if (IsDuplicateMessage(evt)) return;

            var guild = evt.GuildId != null ? _cache.GetGuild(evt.GuildId.Value) : null;
            var channel = _cache.GetChannel(evt.ChannelId);
            
            // Log metrics and message info
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
            _lastMessageCache.AddMessage(evt);
            
            // Get message context from DB (tracking w/ metrics)
            MessageContext ctx;
            await using (var conn = await _db.Obtain())
            using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
                ctx = await _repo.GetMessageContext(conn, evt.Author.Id, evt.GuildId ?? default, evt.ChannelId);
            
            // Try each handler until we find one that succeeds
            if (await TryHandleLogClean(evt, ctx)) 
                return;
            
            // Only do command/proxy handling if it's a user account
            if (evt.Author.Bot || evt.WebhookId != null || evt.Author.System == true) 
                return;
            
            if (await TryHandleCommand(shard, evt, guild, channel, ctx))
                return;
            await TryHandleProxy(shard, evt, guild, channel, ctx);
        }

        private async ValueTask<bool> TryHandleLogClean(MessageCreateEvent evt, MessageContext ctx)
        {
            var channel = _cache.GetChannel(evt.ChannelId);
            if (!evt.Author.Bot || channel.Type != Channel.ChannelType.GuildText ||
                !ctx.LogCleanupEnabled) return false;

            await _loggerClean.HandleLoggerBotCleanup(evt);
            return true;
        }

        private async ValueTask<bool> TryHandleCommand(Shard shard, MessageCreateEvent evt, Guild? guild, Channel channel, MessageContext ctx)
        {
            var content = evt.Content;
            if (content == null) return false;

            // Check for command prefix
            if (!HasCommandPrefix(content, shard.User?.Id ?? default, out var cmdStart) || cmdStart == content.Length)
                return false;

            // Trim leading whitespace from command without actually modifying the string
            // This just moves the argPos pointer by however much whitespace is at the start of the post-argPos string
            var trimStartLengthDiff = content.Substring(cmdStart).Length - content.Substring(cmdStart).TrimStart().Length;
            cmdStart += trimStartLengthDiff;

            try
            {
                var system = ctx.SystemId != null ? await _db.Execute(c => _repo.GetSystem(c, ctx.SystemId.Value)) : null;
                await _tree.ExecuteCommand(new Context(_services, shard, guild, channel, evt, cmdStart, system, ctx, _bot.PermissionsIn(channel.Id)));
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

        private async ValueTask<bool> TryHandleProxy(Shard shard, MessageCreateEvent evt, Guild guild, Channel channel, MessageContext ctx)
        {
            var botPermissions = _bot.PermissionsIn(channel.Id);

            try
            {
                return await _proxy.HandleIncomingMessage(shard, evt, ctx, guild, channel, allowAutoproxy: ctx.AllowAutoproxy, botPermissions);
            }
            catch (PKError e)
            {
                // User-facing errors, print to the channel properly formatted
                if (botPermissions.HasFlag(PermissionSet.SendMessages))
                {
                    await _rest.CreateMessage(evt.ChannelId,
                        new MessageRequest {Content = $"{Emojis.Error} {e.Message}"});
                }
            }

            return false;
        }
    }
}
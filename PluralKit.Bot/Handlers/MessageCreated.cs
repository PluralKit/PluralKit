using System;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class MessageCreated: IEventHandler<MessageCreateEventArgs>
    {
        private readonly CommandTree _tree;
        private readonly DiscordShardedClient _client;
        private readonly LastMessageCacheService _lastMessageCache;
        private readonly LoggerCleanService _loggerClean;
        private readonly IMetrics _metrics;
        private readonly ProxyService _proxy;
        private readonly ILifetimeScope _services;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly BotConfig _config;

        public MessageCreated(LastMessageCacheService lastMessageCache, LoggerCleanService loggerClean,
                              IMetrics metrics, ProxyService proxy, DiscordShardedClient client,
                              CommandTree tree, ILifetimeScope services, IDatabase db, BotConfig config, ModelRepository repo)
        {
            _lastMessageCache = lastMessageCache;
            _loggerClean = loggerClean;
            _metrics = metrics;
            _proxy = proxy;
            _client = client;
            _tree = tree;
            _services = services;
            _db = db;
            _config = config;
            _repo = repo;
        }

        public DiscordChannel ErrorChannelFor(MessageCreateEventArgs evt) => evt.Channel;

        private bool IsDuplicateMessage(DiscordMessage evt) =>
            // We consider a message duplicate if it has the same ID as the previous message that hit the gateway
            _lastMessageCache.GetLastMessage(evt.ChannelId) == evt.Id;

        public async Task Handle(MessageCreateEventArgs evt)
        {
            if (evt.Author?.Id == _client.CurrentUser?.Id) return;
            if (evt.Message.MessageType != MessageType.Default) return;
            if (IsDuplicateMessage(evt.Message)) return;
            
            // Log metrics and message info
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
            _lastMessageCache.AddMessage(evt.Channel.Id, evt.Message.Id);
            
            // Get message context from DB (tracking w/ metrics)
            MessageContext ctx;
            await using (var conn = await _db.Obtain())
            using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
                ctx = await _repo.GetMessageContext(conn, evt.Author.Id, evt.Channel.GuildId, evt.Channel.Id);

            // Try each handler until we find one that succeeds
            if (await TryHandleLogClean(evt, ctx)) 
                return;
            
            // Only do command/proxy handling if it's a user account
            if (evt.Message.Author.IsBot || evt.Message.WebhookMessage || evt.Message.Author.IsSystem == true) 
                return;
            if (await TryHandleCommand(evt, ctx))
                return;
            await TryHandleProxy(evt, ctx);
        }

        private async ValueTask<bool> TryHandleLogClean(MessageCreateEventArgs evt, MessageContext ctx)
        {
            if (!evt.Message.Author.IsBot || evt.Message.Channel.Type != ChannelType.Text ||
                !ctx.LogCleanupEnabled) return false;

            await _loggerClean.HandleLoggerBotCleanup(evt.Message);
            return true;
        }

        private async ValueTask<bool> TryHandleCommand(MessageCreateEventArgs evt, MessageContext ctx)
        {
            var content = evt.Message.Content;
            if (content == null) return false;

            // Check for command prefix
            if (!HasCommandPrefix(content, out var cmdStart))
                return false;

            // Trim leading whitespace from command without actually modifying the string
            // This just moves the argPos pointer by however much whitespace is at the start of the post-argPos string
            var trimStartLengthDiff = content.Substring(cmdStart).Length - content.Substring(cmdStart).TrimStart().Length;
            cmdStart += trimStartLengthDiff;

            try
            {
                var system = ctx.SystemId != null ? await _db.Execute(c => _repo.GetSystem(c, ctx.SystemId.Value)) : null;
                await _tree.ExecuteCommand(new Context(_services, evt.Client, evt.Message, cmdStart, system, ctx));
            }
            catch (PKError)
            {
                // Only permission errors will ever bubble this far and be caught here instead of Context.Execute
                // so we just catch and ignore these. TODO: this may need to change.
            }

            return true;
        }

        private bool HasCommandPrefix(string message, out int argPos)
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
                return id == _client.CurrentUser.Id;

            return false;
        }

        private async ValueTask<bool> TryHandleProxy(MessageCreateEventArgs evt, MessageContext ctx)
        {
            try
            {
                return await _proxy.HandleIncomingMessage(evt.Message, ctx, allowAutoproxy: true);
            }
            catch (PKError e)
            {
                // User-facing errors, print to the channel properly formatted
                var msg = evt.Message;
                if (msg.Channel.Guild == null || msg.Channel.BotHasAllPermissions(Permissions.SendMessages))
                    await msg.Channel.SendMessageFixedAsync($"{Emojis.Error} {e.Message}");
            }

            return false;
        }
    }
}
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
        private readonly IDataStore _data;

        public MessageCreated(LastMessageCacheService lastMessageCache, LoggerCleanService loggerClean,
                              IMetrics metrics, ProxyService proxy, DiscordShardedClient client,
                              CommandTree tree, ILifetimeScope services, IDatabase db, IDataStore data)
        {
            _lastMessageCache = lastMessageCache;
            _loggerClean = loggerClean;
            _metrics = metrics;
            _proxy = proxy;
            _client = client;
            _tree = tree;
            _services = services;
            _db = db;
            _data = data;
        }

        public DiscordChannel ErrorChannelFor(MessageCreateEventArgs evt) => evt.Channel;

        private bool IsDuplicateMessage(DiscordMessage evt) =>
            // We consider a message duplicate if it has the same ID as the previous message that hit the gateway
            _lastMessageCache.GetLastMessage(evt.ChannelId) == evt.Id;

        public async Task Handle(MessageCreateEventArgs evt)
        {
            if (evt.Message.MessageType != MessageType.Default) return;
            if (IsDuplicateMessage(evt.Message)) return;
            
            // Log metrics and message info
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
            _lastMessageCache.AddMessage(evt.Channel.Id, evt.Message.Id);
            
            // Get message context from DB (tracking w/ metrics)
            MessageContext ctx;
            await using (var conn = await _db.Obtain())
            using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
                ctx = await conn.QueryMessageContext(evt.Author.Id, evt.Channel.GuildId, evt.Channel.Id);

            // Try each handler until we find one that succeeds
            var _ = await TryHandleLogClean(evt, ctx) ||
                    await TryHandleCommand(evt, ctx) || 
                    await TryHandleProxy(evt, ctx);
        }

        private async ValueTask<bool> TryHandleLogClean(MessageCreateEventArgs evt, MessageContext ctx)
        {
            if (!evt.Message.Author.IsBot || evt.Message.Channel.Type != ChannelType.Text ||
                ctx == null || !ctx.LogCleanupEnabled) return false;

            await _loggerClean.HandleLoggerBotCleanup(evt.Message);
            return true;
        }

        private async ValueTask<bool> TryHandleCommand(MessageCreateEventArgs evt, MessageContext ctx)
        {
            var content = evt.Message.Content;
            if (content == null) return false;

            var argPos = -1;
            // Check if message starts with the command prefix
            if (content.StartsWith("pk;", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (content.StartsWith("pk!", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (StringUtils.HasMentionPrefix(content, ref argPos, out var id)) // Set argPos to the proper value
                if (id != _client.CurrentUser.Id) // But undo it if it's someone else's ping
                    argPos = -1;

            // If we didn't find a prefix, give up handling commands
            if (argPos == -1) return false;

            // Trim leading whitespace from command without actually modifying the wring
            // This just moves the argPos pointer by however much whitespace is at the start of the post-argPos string
            var trimStartLengthDiff = content.Substring(argPos).Length - content.Substring(argPos).TrimStart().Length;
            argPos += trimStartLengthDiff;

            try
            {
                var system = ctx?.SystemId != null ? await _db.Execute(c => c.QuerySystem(ctx.SystemId.Value)) : null;
                await _tree.ExecuteCommand(new Context(_services, evt.Client, evt.Message, argPos, system, ctx));
            }
            catch (PKError)
            {
                // Only permission errors will ever bubble this far and be caught here instead of Context.Execute
                // so we just catch and ignore these. TODO: this may need to change.
            }

            return true;
        }

        private async ValueTask<bool> TryHandleProxy(MessageCreateEventArgs evt, MessageContext ctx)
        {
            if (ctx == null) return false;
            try
            {
                return await _proxy.HandleIncomingMessage(evt.Message, ctx, allowAutoproxy: true);
            }
            catch (PKError e)
            {
                // User-facing errors, print to the channel properly formatted
                var msg = evt.Message;
                if (msg.Channel.Guild == null || msg.Channel.BotHasAllPermissions(Permissions.SendMessages))
                    await msg.Channel.SendMessageAsync($"{Emojis.Error} {e.Message}");
            }

            return false;
        }
    }
}
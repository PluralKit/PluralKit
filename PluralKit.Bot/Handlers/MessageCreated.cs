using System;
using System.Threading.Tasks;

using App.Metrics;

using Autofac;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using PluralKit.Core;

using Sentry;

using Serilog;

namespace PluralKit.Bot
{
    public class MessageCreated: IEventHandler<MessageCreateEventArgs>
    {
        private readonly CommandTree _tree;
        private readonly DiscordShardedClient _client;
        private readonly LastMessageCacheService _lastMessageCache;
        private readonly ILogger _logger;
        private readonly LoggerCleanService _loggerClean;
        private readonly IMetrics _metrics;
        private readonly ProxyService _proxy;
        private readonly ProxyCache _proxyCache;
        private readonly Scope _sentryScope;
        private readonly ILifetimeScope _services;
        
        public MessageCreated(LastMessageCacheService lastMessageCache, ILogger logger, LoggerCleanService loggerClean, IMetrics metrics, ProxyService proxy, ProxyCache proxyCache, Scope sentryScope, DiscordShardedClient client, CommandTree tree, ILifetimeScope services)
        {
            _lastMessageCache = lastMessageCache;
            _logger = logger;
            _loggerClean = loggerClean;
            _metrics = metrics;
            _proxy = proxy;
            _proxyCache = proxyCache;
            _sentryScope = sentryScope;
            _client = client;
            _tree = tree;
            _services = services;
        }

        public DiscordChannel ErrorChannelFor(MessageCreateEventArgs evt) => evt.Channel;

        public async Task Handle(MessageCreateEventArgs evt)
        {
            // Drop the message if we've already received it.
            // Gateway occasionally resends events for whatever reason and it can break stuff relying on IDs being unique
            // Not a perfect fix since reordering could still break things but... it's good enough for now
            // (was considering checking the order of the IDs but IDs aren't guaranteed to be *perfectly* in order, so that'd cause false positives)
            // LastMessageCache is updated in RegisterMessageMetrics so the ordering here is correct.
            if (_lastMessageCache.GetLastMessage(evt.Channel.Id) == evt.Message.Id) return;
            RegisterMessageMetrics(evt);
            
            // Ignore system messages (member joined, message pinned, etc)
            var msg = evt.Message;
            if (msg.MessageType != MessageType.Default) return;

            var cachedGuild = await _proxyCache.GetGuildDataCached(msg.Channel.GuildId);
            var cachedAccount = await _proxyCache.GetAccountDataCached(msg.Author.Id);
            // this ^ may be null, do remember that down the line
            
            // Pass guild bot/WH messages onto the logger cleanup service
            if (msg.Author.IsBot && msg.Channel.Type == ChannelType.Text)
            {
                await _loggerClean.HandleLoggerBotCleanup(msg, cachedGuild);
                return;
            }

            // First try parsing a command, then try proxying
            if (await TryHandleCommand(evt, cachedGuild, cachedAccount)) return;
            await TryHandleProxy(evt, cachedGuild, cachedAccount);
        }

        private async Task<bool> TryHandleCommand(MessageCreateEventArgs evt, GuildConfig cachedGuild, CachedAccount cachedAccount)
        {
            var msg = evt.Message;
            
            int argPos = -1;
            // Check if message starts with the command prefix
            if (msg.Content.StartsWith("pk;", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (msg.Content.StartsWith("pk!", StringComparison.InvariantCultureIgnoreCase)) argPos = 3;
            else if (msg.Content != null && StringUtils.HasMentionPrefix(msg.Content, ref argPos, out var id)) // Set argPos to the proper value
                if (id != _client.CurrentUser.Id) // But undo it if it's someone else's ping
                    argPos = -1;
            
            // If we didn't find a prefix, give up handling commands
            if (argPos == -1) return false;
            
            // Trim leading whitespace from command without actually modifying the wring
            // This just moves the argPos pointer by however much whitespace is at the start of the post-argPos string
            var trimStartLengthDiff = msg.Content.Substring(argPos).Length - msg.Content.Substring(argPos).TrimStart().Length;
            argPos += trimStartLengthDiff;

            try
            {
                await _tree.ExecuteCommand(new Context(_services, evt.Client, msg, argPos, cachedAccount?.System));
            }
            catch (PKError)
            {
                // Only permission errors will ever bubble this far and be caught here instead of Context.Execute
                // so we just catch and ignore these. TODO: this may need to change.
            }

            return true;
        }

        private async Task<bool> TryHandleProxy(MessageCreateEventArgs evt, GuildConfig cachedGuild, CachedAccount cachedAccount)
        {
            var msg = evt.Message;
            
            // If we don't have any cached account data, this means no member in the account has a proxy tag set
            if (cachedAccount == null) return false;
            
            try
            {
                await _proxy.HandleMessageAsync(evt.Client, cachedGuild, cachedAccount, msg, allowAutoproxy: true);
            }
            catch (PKError e)
            {
                // User-facing errors, print to the channel properly formatted
                if (msg.Channel.Guild == null || msg.Channel.BotHasAllPermissions(Permissions.SendMessages))
                    await msg.Channel.SendMessageAsync($"{Emojis.Error} {e.Message}");
            }

            return true;
        }

        private void RegisterMessageMetrics(MessageCreateEventArgs evt)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesReceived);
            _lastMessageCache.AddMessage(evt.Channel.Id, evt.Message.Id);
        }
    }
}
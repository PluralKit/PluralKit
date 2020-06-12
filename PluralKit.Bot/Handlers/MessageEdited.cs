using System.Collections.Generic;
using System.Threading.Tasks;

using DSharpPlus.EventArgs;

using PluralKit.Core;

using Sentry;


namespace PluralKit.Bot
{
    public class MessageEdited: IEventHandler<MessageUpdateEventArgs>
    {
        private readonly LastMessageCacheService _lastMessageCache;
        private readonly ProxyService _proxy;
        private readonly ProxyCache _proxyCache;
        private readonly Scope _sentryScope;

        public MessageEdited(LastMessageCacheService lastMessageCache, ProxyService proxy, ProxyCache proxyCache, Scope sentryScope)
        {
            _lastMessageCache = lastMessageCache;
            _proxy = proxy;
            _proxyCache = proxyCache;
            _sentryScope = sentryScope;
        }

        public async Task Handle(MessageUpdateEventArgs evt)
        {
            // Sometimes edit message events arrive for other reasons (eg. an embed gets updated server-side)
            // If this wasn't a *content change* (ie. there's message contents to read), bail
            // It'll also sometimes arrive with no *author*, so we'll go ahead and ignore those messages too
            if (evt.Message.Content == null) return;
            if (evt.Author == null) return;
            
            // Also, if this is in DMs don't bother either
            if (evt.Channel.Guild == null) return;

            // If this isn't the last message in the channel, don't do anything
            if (_lastMessageCache.GetLastMessage(evt.Channel.Id) != evt.Message.Id) return;
            
            // Fetch account and guild info from cache if there is any
            var account = await _proxyCache.GetAccountDataCached(evt.Author.Id);
            if (account == null) return; // Again: no cache = no account = no system = no proxy
            var guild = await _proxyCache.GetGuildDataCached(evt.Channel.GuildId);
            
            // Just run the normal message handling stuff, with a flag to disable autoproxying
            await _proxy.HandleIncomingMessage(evt.Message, allowAutoproxy: false);
        }
    }
}
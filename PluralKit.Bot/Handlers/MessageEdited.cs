using System.Threading.Tasks;

using DSharpPlus.EventArgs;

using PluralKit.Core;


namespace PluralKit.Bot
{
    public class MessageEdited: IEventHandler<MessageUpdateEventArgs>
    {
        private readonly LastMessageCacheService _lastMessageCache;
        private readonly ProxyService _proxy;
        private readonly DbConnectionFactory _db;

        public MessageEdited(LastMessageCacheService lastMessageCache, ProxyService proxy, DbConnectionFactory db)
        {
            _lastMessageCache = lastMessageCache;
            _proxy = proxy;
            _db = db;
        }

        public async Task Handle(MessageUpdateEventArgs evt)
        {
            // Edit message events sometimes arrive with missing data; double-check it's all there
            if (evt.Message.Content == null || evt.Author == null || evt.Channel.Guild == null) return;
            
            // Only react to the last message in the channel
            if (_lastMessageCache.GetLastMessage(evt.Channel.Id) != evt.Message.Id) return;
            
            // Just run the normal message handling code, with a flag to disable autoproxying
            var ctx = await _db.Execute(c => c.QueryMessageContext(evt.Author.Id, evt.Channel.GuildId, evt.Channel.Id));
            await _proxy.HandleIncomingMessage(evt.Message, ctx, allowAutoproxy: false);
        }
    }
}
using System.Threading.Tasks;

using App.Metrics;

using DSharpPlus;
using DSharpPlus.EventArgs;

using PluralKit.Core;


namespace PluralKit.Bot
{
    public class MessageEdited: IEventHandler<MessageUpdateEventArgs>
    {
        private readonly LastMessageCacheService _lastMessageCache;
        private readonly ProxyService _proxy;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IMetrics _metrics;
        private readonly DiscordShardedClient _client;

        public MessageEdited(LastMessageCacheService lastMessageCache, ProxyService proxy, IDatabase db, IMetrics metrics, ModelRepository repo, DiscordShardedClient client)
        {
            _lastMessageCache = lastMessageCache;
            _proxy = proxy;
            _db = db;
            _metrics = metrics;
            _repo = repo;
            _client = client;
        }

        public async Task Handle(MessageUpdateEventArgs evt)
        {
            if (evt.Author?.Id == _client.CurrentUser?.Id) return;
            
            // Edit message events sometimes arrive with missing data; double-check it's all there
            if (evt.Message.Content == null || evt.Author == null || evt.Channel.Guild == null) return;
            
            // Only react to the last message in the channel
            if (_lastMessageCache.GetLastMessage(evt.Channel.Id) != evt.Message.Id) return;
            
            // Just run the normal message handling code, with a flag to disable autoproxying
            MessageContext ctx;
            await using (var conn = await _db.Obtain())
            using (_metrics.Measure.Timer.Time(BotMetrics.MessageContextQueryTime))
                ctx = await _repo.GetMessageContext(conn, evt.Author.Id, evt.Channel.GuildId, evt.Channel.Id);
            await _proxy.HandleIncomingMessage(evt.Message, ctx, allowAutoproxy: false);
        }
    }
}
using System.Collections.Concurrent;
using System.Collections.Generic;

using Myriad.Types;

namespace PluralKit.Bot
{
    // TODO: Should this be moved to Myriad.Cache?
    public class LastMessageCacheService
    {
        private readonly IDictionary<ulong, CachedMessage> _cache = new ConcurrentDictionary<ulong, CachedMessage>();

        public void AddMessage(Message msg)
        {
            _cache[msg.ChannelId] = new CachedMessage(msg);
        }

        public CachedMessage GetLastMessage(ulong channel)
        {
            if (_cache.TryGetValue(channel, out var message)) return message;
            return null;
        }
    }

    public class CachedMessage
    {
        public ulong mid;
        public ulong? referenced_message;

        public CachedMessage(Message msg)
        {
            mid = msg.Id;
            if (msg.ReferencedMessage.Value != null)
                referenced_message = msg.ReferencedMessage.Value.Id;
        }
    }
}

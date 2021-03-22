using System.Collections.Concurrent;
using System.Collections.Generic;

using Myriad.Types;

namespace PluralKit.Bot
{
    // Doing things like this instead of enabling D.NET's message cache because the message cache is, let's face it,
    // not particularly efficient? It allocates a dictionary *and* a queue for every single channel (500k in prod!)
    // whereas this is, worst case, one dictionary *entry* of a single ulong per channel, and one dictionary instance
    // on the whole instance, total. Yeah, much more efficient.
    // TODO: is this still needed after the D#+ migration?
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
            if (msg.ReferencedMessage.HasValue)
                referenced_message = msg.ReferencedMessage.Value.Id;
        }
    }
}
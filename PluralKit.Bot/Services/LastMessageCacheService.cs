using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PluralKit.Bot
{
    // Doing things like this instead of enabling D.NET's message cache because the message cache is, let's face it,
    // not particularly efficient? It allocates a dictionary *and* a queue for every single channel (500k in prod!)
    // whereas this is, worst case, one dictionary *entry* of a single ulong per channel, and one dictionary instance
    // on the whole instance, total. Yeah, much more efficient.
    // TODO: is this still needed after the D#+ migration?
    public class LastMessageCacheService
    {
        private readonly IDictionary<ulong, ulong> _cache = new ConcurrentDictionary<ulong, ulong>();

        public void AddMessage(ulong channel, ulong message)
        {
            _cache[channel] = message;
        }

        public ulong? GetLastMessage(ulong channel)
        {
            if (_cache.TryGetValue(channel, out var message)) return message;
            return null;
        }
    }
}
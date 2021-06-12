#nullable enable
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
            _cache[msg.ChannelId] = new CachedMessage(msg.Id, msg.ReferencedMessage.Value?.Id);
        }

        public CachedMessage? GetLastMessage(ulong channel)
        {
            return _cache.TryGetValue(channel, out var message) ? message : null;
        }
    }

    public record CachedMessage(ulong Id, ulong? ReferencedMessage);
}

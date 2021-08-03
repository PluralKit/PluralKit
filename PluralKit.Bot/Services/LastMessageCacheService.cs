#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using Myriad.Types;

namespace PluralKit.Bot
{
    // TODO: Should this be moved to Myriad.Cache?
    public class LastMessageCacheService
    {
        private readonly IDictionary<ulong, CachedMessage> _cache = new ConcurrentDictionary<ulong, CachedMessage>();

        public void AddMessage(Message msg)
        {
            var previous = GetLastMessage(msg.ChannelId);
            _cache[msg.ChannelId] = new CachedMessage(msg.Id, msg.ReferencedMessage.Value?.Id, previous?.Id);
        }

        public CachedMessage? GetLastMessage(ulong channel)
        {
            return _cache.TryGetValue(channel, out var message) ? message : null;
        }

        public void HandleMessageDeletion(ulong channel, ulong message)
        {
            var storedMessage = GetLastMessage(channel);
            if (storedMessage == null)
                return;

            if (message == storedMessage.Id)
                if (storedMessage.Previous != null)
                    _cache[channel] = new CachedMessage(storedMessage.Previous.Value, null, null);
                else
                    _cache.Remove(channel);
            else if (message == storedMessage.Previous)
                _cache[channel] = new CachedMessage(storedMessage.Id, storedMessage.ReferencedMessage, null);
        }

        public void HandleMessageDeletion(ulong channel, List<ulong> messages)
        {
            var storedMessage = GetLastMessage(channel);
            if (storedMessage == null)
                return;

            if (!(messages.Contains(storedMessage.Id) || (storedMessage.Previous != null && messages.Contains(storedMessage.Previous.Value))))
                // none of the deleted messages are relevant to the cache
                return;

            ulong? newLastMessage = null;

            if (messages.Contains(storedMessage.Id))
                newLastMessage = storedMessage.Previous;

            if (storedMessage.Previous != null && messages.Contains(storedMessage.Previous.Value))
                if (newLastMessage == storedMessage.Previous)
                    newLastMessage = null;
                else
                {
                    _cache[channel] = new CachedMessage(storedMessage.Id, storedMessage.ReferencedMessage, null);
                    return;
                }

            if (newLastMessage == null)
                _cache.Remove(channel);
        }
    }

    public record CachedMessage(ulong Id, ulong? ReferencedMessage, ulong? Previous);
}

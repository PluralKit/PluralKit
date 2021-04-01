using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

using Myriad.Types;

namespace PluralKit.Bot
{
    public class LastMessageCacheService
    {
        private readonly IDictionary<ulong, CachedMessage> _cache = new ConcurrentDictionary<ulong, CachedMessage>();

        public void AddMessage(Message msg)
        {
            _cache.TryGetValue(msg.ChannelId, out var message);
            _cache[msg.ChannelId] = new CachedMessage(message, msg);
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
        public string webhook_name;

        public CachedMessage previous;

        public CachedMessage(CachedMessage old, Message msg)
        {
            // let's not memory leak
            if (old != null) old.previous = null;
            this.previous = old;

            mid = msg.Id;
            if (msg.ReferencedMessage.Value != null)
                referenced_message = msg.ReferencedMessage.Value.Id;
            if (msg.WebhookId != null)
                webhook_name = msg.Author.Username;
        }
    }
}

#nullable enable
using System.Collections.Concurrent;

using Myriad.Types;

namespace PluralKit.Bot;

public class LastMessageCacheService
{
    private readonly IDictionary<ulong, CacheEntry> _cache = new ConcurrentDictionary<ulong, CacheEntry>();

    public void AddMessage(Message msg)
    {
        var previous = GetLastMessage(msg.ChannelId);
        var current = ToCachedMessage(msg);
        _cache[msg.ChannelId] = new CacheEntry(current, previous?.Current);
    }

    private CachedMessage ToCachedMessage(Message msg) =>
        new(msg.Id, msg.ReferencedMessage.Value?.Id, msg.Author.Username);

    public CacheEntry? GetLastMessage(ulong channel) =>
        _cache.TryGetValue(channel, out var message) ? message : null;

    public void HandleMessageDeletion(ulong channel, ulong message)
    {
        var storedMessage = GetLastMessage(channel);
        if (storedMessage == null)
            return;

        if (message == storedMessage.Current.Id)
            if (storedMessage.Previous != null)
                _cache[channel] = new CacheEntry(storedMessage.Previous, null);
            else
                _cache.Remove(channel);
        else if (message == storedMessage.Previous?.Id)
            _cache[channel] = new CacheEntry(storedMessage.Current, null);
    }

    public void HandleMessageDeletion(ulong channel, List<ulong> messages)
    {
        var storedMessage = GetLastMessage(channel);
        if (storedMessage == null)
            return;

        if (!(messages.Contains(storedMessage.Current.Id) ||
              storedMessage.Previous != null && messages.Contains(storedMessage.Previous.Id)))
            // none of the deleted messages are relevant to the cache
            return;

        ulong? newLastMessage = null;

        if (messages.Contains(storedMessage.Current.Id))
            newLastMessage = storedMessage.Previous?.Id;

        if (storedMessage.Previous != null && messages.Contains(storedMessage.Previous.Id))
            if (newLastMessage == storedMessage.Previous?.Id)
            {
                newLastMessage = null;
            }
            else
            {
                _cache[channel] = new CacheEntry(storedMessage.Current, null);
                return;
            }

        if (newLastMessage == null)
            _cache.Remove(channel);
    }
}

public record CacheEntry(CachedMessage Current, CachedMessage? Previous);

public record CachedMessage(ulong Id, ulong? ReferencedMessage, string AuthorUsername);
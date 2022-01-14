using Myriad.Gateway;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

// Double duty :)
public class MessageDeleted: IEventHandler<MessageDeleteEvent>, IEventHandler<MessageDeleteBulkEvent>
{
    private static readonly TimeSpan MessageDeleteDelay = TimeSpan.FromSeconds(15);

    private readonly IDatabase _db;
    private readonly ModelRepository _repo;
    private readonly ILogger _logger;
    private readonly LastMessageCacheService _lastMessage;

    public MessageDeleted(ILogger logger, IDatabase db, ModelRepository repo, LastMessageCacheService lastMessage)
    {
        _db = db;
        _repo = repo;
        _lastMessage = lastMessage;
        _logger = logger.ForContext<MessageDeleted>();
    }

    public Task Handle(int shardId, MessageDeleteEvent evt)
    {
        // Delete deleted webhook messages from the data store
        // Most of the data in the given message is wrong/missing, so always delete just to be sure.

        async Task Inner()
        {
            await Task.Delay(MessageDeleteDelay);
            await _repo.DeleteMessage(evt.Id);
        }

        _lastMessage.HandleMessageDeletion(evt.ChannelId, evt.Id);

        // Fork a task to delete the message after a short delay
        // to allow for lookups to happen for a little while after deletion
        _ = Inner();
        return Task.CompletedTask;
    }

    public Task Handle(int shardId, MessageDeleteBulkEvent evt)
    {
        // Same as above, but bulk
        async Task Inner()
        {
            await Task.Delay(MessageDeleteDelay);

            _logger.Information("Bulk deleting {Count} messages in channel {Channel}",
                evt.Ids.Length, evt.ChannelId);
            await _repo.DeleteMessagesBulk(evt.Ids);
        }

        _lastMessage.HandleMessageDeletion(evt.ChannelId, evt.Ids.ToList());
        _ = Inner();
        return Task.CompletedTask;
    }

}
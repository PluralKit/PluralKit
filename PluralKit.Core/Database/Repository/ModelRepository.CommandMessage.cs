using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task SaveCommandMessage(ulong messageId, ulong channelId, ulong authorId)
    {
        var query = new Query("command_messages").AsInsert(new
        {
            message_id = messageId,
            channel_id = channelId,
            author_id = authorId,
        });
        return _db.ExecuteQuery(query);
    }

    public Task<CommandMessage?> GetCommandMessage(ulong messageId)
    {
        var query = new Query("command_messages").Where("message_id", messageId);
        return _db.QueryFirst<CommandMessage?>(query);
    }

    public Task<int> DeleteCommandMessagesBefore(ulong messageIdThreshold)
    {
        var query = new Query("command_messages").AsDelete().Where("message_id", "<", messageIdThreshold);
        return _db.QueryFirst<int>(query);
    }
}

public class CommandMessage
{
    public ulong AuthorId { get; set; }
    public ulong MessageId { get; set; }
    public ulong ChannelId { get; set; }
}
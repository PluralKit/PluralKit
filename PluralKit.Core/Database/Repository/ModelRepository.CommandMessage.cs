using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task SaveCommandMessage(IPKConnection conn, ulong messageId, ulong channelId, ulong authorId) =>
            conn.QueryAsync("insert into command_messages (message_id, channel_id, author_id) values (@Message, @Channel, @Author)",
                new { Message = messageId, Channel = channelId, Author = authorId });

        public Task<CommandMessage?> GetCommandMessage(IPKConnection conn, ulong messageId) =>
            conn.QuerySingleOrDefaultAsync<CommandMessage>("select * from command_messages where message_id = @Message",
                new { Message = messageId });

        public Task<int> DeleteCommandMessagesBefore(IPKConnection conn, ulong messageIdThreshold) =>
            conn.ExecuteAsync("delete from command_messages where message_id < @Threshold",
                new { Threshold = messageIdThreshold });
    }

    public class CommandMessage
    {
        public ulong AuthorId { get; set; }
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }
    }
}
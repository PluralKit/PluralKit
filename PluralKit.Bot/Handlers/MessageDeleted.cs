using System.Linq;
using System.Threading.Tasks;

using DSharpPlus.EventArgs;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    // Double duty :)
    public class MessageDeleted: IEventHandler<MessageDeleteEventArgs>, IEventHandler<MessageBulkDeleteEventArgs>
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly ILogger _logger;

        public MessageDeleted(ILogger logger, IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
            _logger = logger.ForContext<MessageDeleted>();
        }
        
        public async Task Handle(MessageDeleteEventArgs evt)
        {
            // Delete deleted webhook messages from the data store
            // Most of the data in the given message is wrong/missing, so always delete just to be sure.
            await _db.Execute(c => _repo.DeleteMessage(c, evt.Message.Id));
        }

        public async Task Handle(MessageBulkDeleteEventArgs evt)
        {
            // Same as above, but bulk
            _logger.Information("Bulk deleting {Count} messages in channel {Channel}", evt.Messages.Count, evt.Channel.Id);
            await _db.Execute(c => _repo.DeleteMessagesBulk(c, evt.Messages.Select(m => m.Id).ToList()));
        }
    }
}
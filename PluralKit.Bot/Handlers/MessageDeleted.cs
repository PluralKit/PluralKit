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
        private readonly IDataStore _data;
        private readonly ILogger _logger;

        public MessageDeleted(IDataStore data, ILogger logger)
        {
            _data = data;
            _logger = logger.ForContext<MessageDeleted>();
        }
        
        public async Task Handle(MessageDeleteEventArgs evt)
        {
            // Delete deleted webhook messages from the data store
            // (if we don't know whether it's a webhook, delete it just to be safe)
            if (!evt.Message.WebhookMessage) return;
            await _data.DeleteMessage(evt.Message.Id);
        }

        public async Task Handle(MessageBulkDeleteEventArgs evt)
        {
            // Same as above, but bulk
            _logger.Information("Bulk deleting {Count} messages in channel {Channel}", evt.Messages.Count, evt.Channel.Id);
            await _data.DeleteMessagesBulk(evt.Messages.Select(m => m.Id).ToList());
        }
    }
}
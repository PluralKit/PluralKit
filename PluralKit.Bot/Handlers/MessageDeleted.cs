using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus.EventArgs;

using Sentry;

namespace PluralKit.Bot
{
    // Double duty :)
    public class MessageDeleted: IEventHandler<MessageDeleteEventArgs>, IEventHandler<MessageBulkDeleteEventArgs>
    {
        private readonly ProxyService _proxy;

        public MessageDeleted(ProxyService proxy)
        {
            _proxy = proxy;
        }
        
        public Task Handle(MessageDeleteEventArgs evt)
        {
            return _proxy.HandleMessageDeletedAsync(evt);
        }

        public Task Handle(MessageBulkDeleteEventArgs evt)
        {
            return _proxy.HandleMessageBulkDeleteAsync(evt);
        }
    }
}
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
        private readonly Scope _sentryScope;

        public MessageDeleted(Scope sentryScope, ProxyService proxy)
        {
            _sentryScope = sentryScope;
            _proxy = proxy;
        }
        
        public Task Handle(MessageDeleteEventArgs evt)
        {
            _sentryScope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", evt.Channel.Id.ToString()},
                {"guild", evt.Channel.GuildId.ToString()},
                {"message", evt.Message.Id.ToString()},
            });
            _sentryScope.SetTag("shard", evt.Client.ShardId.ToString());

            return _proxy.HandleMessageDeletedAsync(evt);
        }

        public Task Handle(MessageBulkDeleteEventArgs evt)
        {
            _sentryScope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", evt.Channel.Id.ToString()},
                {"guild", evt.Channel.Id.ToString()},
                {"messages", string.Join(",", evt.Messages.Select(m => m.Id))},
            });
            _sentryScope.SetTag("shard", evt.Client.ShardId.ToString());

            return _proxy.HandleMessageBulkDeleteAsync(evt);
        }
    }
}
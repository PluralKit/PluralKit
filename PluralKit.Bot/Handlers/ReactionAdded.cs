using System.Collections.Generic;
using System.Threading.Tasks;

using DSharpPlus.EventArgs;

using Sentry;

namespace PluralKit.Bot
{
    public class ReactionAdded: IEventHandler<MessageReactionAddEventArgs>
    {
        private readonly ProxyService _proxy;
        private readonly Scope _sentryScope;

        public ReactionAdded(ProxyService proxy, Scope sentryScope)
        {
            _proxy = proxy;
            _sentryScope = sentryScope;
        }

        public Task Handle(MessageReactionAddEventArgs evt)
        {
            _sentryScope.AddBreadcrumb("", "event.reaction", data: new Dictionary<string, string>()
            {
                {"user", evt.User.Id.ToString()},
                {"channel", (evt.Channel?.Id ?? 0).ToString()},
                {"guild", (evt.Channel?.GuildId ?? 0).ToString()},
                {"message", evt.Message.Id.ToString()},
                {"reaction", evt.Emoji.Name}
            });
            _sentryScope.SetTag("shard", evt.Client.ShardId.ToString());
            return _proxy.HandleReactionAddedAsync(evt);
        }
    }
}
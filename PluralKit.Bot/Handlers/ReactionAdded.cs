using System.Collections.Generic;
using System.Threading.Tasks;

using DSharpPlus.EventArgs;

using Sentry;

namespace PluralKit.Bot
{
    public class ReactionAdded: IEventHandler<MessageReactionAddEventArgs>
    {
        private readonly ProxyService _proxy;

        public ReactionAdded(ProxyService proxy)
        {
            _proxy = proxy;
        }

        public Task Handle(MessageReactionAddEventArgs evt)
        {
            return _proxy.HandleReactionAddedAsync(evt);
        }
    }
}
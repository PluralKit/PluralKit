using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Sentry;

namespace PluralKit.Bot
{
    public static class BreadcrumbExtensions
    {
        public static void AddMessageBreadcrumb(this Scope scope, SocketMessage msg)
        {
            scope.AddBreadcrumb(msg.Content, "event.message", data: new Dictionary<string, string>()
            {
                {"user", msg.Author.Id.ToString()},
                {"channel", msg.Channel.Id.ToString()},
                {"guild", ((msg.Channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"message", msg.Id.ToString()},
            });
        }

        public static void AddReactionAddedBreadcrumb(this Scope scope, Cacheable<IUserMessage, ulong> message,
            ISocketMessageChannel channel, SocketReaction reaction)
        {
            scope.AddBreadcrumb("", "event.reaction", data: new Dictionary<string, string>()
            {
                {"user", reaction.UserId.ToString()},
                {"channel", channel.Id.ToString()},
                {"guild", ((channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"message", message.Id.ToString()},
                {"reaction", reaction.Emote.Name}
            });
        }
        
        public static void AddMessageDeleteBreadcrumb(this Scope scope, Cacheable<IMessage, ulong> message,
            ISocketMessageChannel channel)
        {
            scope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", channel.Id.ToString()},
                {"guild", ((channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"message", message.Id.ToString()},
            });
        }
        
        public static void AddMessageBulkDeleteBreadcrumb(this Scope scope, IReadOnlyCollection<Cacheable<IMessage, ulong>> messages,
            ISocketMessageChannel channel)
        {
            scope.AddBreadcrumb("", "event.messageDelete", data: new Dictionary<string, string>()
            {
                {"channel", channel.Id.ToString()},
                {"guild", ((channel as IGuildChannel)?.GuildId ?? 0).ToString()},
                {"messages", string.Join(",", messages.Select(m => m.Id))},
            });
        }

        public static void AddPeriodicBreadcrumb(this Scope scope) => scope.AddBreadcrumb("", "periodic");
    }
}
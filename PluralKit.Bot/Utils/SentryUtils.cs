using System.Collections.Generic;
using System.Linq;

using DSharpPlus;
using DSharpPlus.EventArgs;

using Sentry;

namespace PluralKit.Bot
{
    public interface ISentryEnricher<T> where T: DiscordEventArgs
    {
        void Enrich(Scope scope, T evt);
    }

    public class SentryEnricher:
        ISentryEnricher<MessageCreateEventArgs>,
        ISentryEnricher<MessageDeleteEventArgs>,
        ISentryEnricher<MessageUpdateEventArgs>,
        ISentryEnricher<MessageBulkDeleteEventArgs>,
        ISentryEnricher<MessageReactionAddEventArgs>
    {
        // TODO: should this class take the Scope by dependency injection instead?
        // Would allow us to create a centralized "chain of handlers" where this class could just be registered as an entry in
        
        public void Enrich(Scope scope, MessageCreateEventArgs evt)
        {
            scope.AddBreadcrumb(evt.Message.Content, "event.message", data: new Dictionary<string, string>
            {
                {"user", evt.Author.Id.ToString()},
                {"channel", evt.Channel.Id.ToString()},
                {"guild", evt.Channel.GuildId.ToString()},
                {"message", evt.Message.Id.ToString()},
            });
            scope.SetTag("shard", evt.Client.ShardId.ToString());

            // Also report information about the bot's permissions in the channel
            // We get a lot of permission errors so this'll be useful for determining problems
            var perms = evt.Channel.BotPermissions();
            scope.AddBreadcrumb(perms.ToPermissionString(), "permissions");
        }

        public void Enrich(Scope scope, MessageDeleteEventArgs evt)
        {
            scope.AddBreadcrumb("", "event.messageDelete",
                data: new Dictionary<string, string>()
                {
                    {"channel", evt.Channel.Id.ToString()},
                    {"guild", evt.Channel.GuildId.ToString()},
                    {"message", evt.Message.Id.ToString()},
                });
            scope.SetTag("shard", evt.Client.ShardId.ToString());
        }

        public void Enrich(Scope scope, MessageUpdateEventArgs evt)
        {
            scope.AddBreadcrumb(evt.Message.Content ?? "<unknown>", "event.messageEdit",
                data: new Dictionary<string, string>()
                {
                    {"channel", evt.Channel.Id.ToString()},
                    {"guild", evt.Channel.GuildId.ToString()},
                    {"message", evt.Message.Id.ToString()}
                });
            scope.SetTag("shard", evt.Client.ShardId.ToString());
        }

        public void Enrich(Scope scope, MessageBulkDeleteEventArgs evt)
        {
            scope.AddBreadcrumb("", "event.messageDelete",
                data: new Dictionary<string, string>()
                {
                    {"channel", evt.Channel.Id.ToString()},
                    {"guild", evt.Channel.Id.ToString()},
                    {"messages", string.Join(",", evt.Messages.Select(m => m.Id))},
                });
            scope.SetTag("shard", evt.Client.ShardId.ToString());
        }

        public void Enrich(Scope scope, MessageReactionAddEventArgs evt)
        {
            scope.AddBreadcrumb("", "event.reaction",
                data: new Dictionary<string, string>()
                {
                    {"user", evt.User.Id.ToString()},
                    {"channel", (evt.Channel?.Id ?? 0).ToString()},
                    {"guild", (evt.Channel?.GuildId ?? 0).ToString()},
                    {"message", evt.Message.Id.ToString()},
                    {"reaction", evt.Emoji.Name}
                });
            scope.SetTag("shard", evt.Client.ShardId.ToString());
        }
    }
}
using Myriad.Gateway;

using Sentry;

namespace PluralKit.Bot;

public interface ISentryEnricher<T> where T : IGatewayEvent
{
    void Enrich(Scope scope, int shardId, T evt);
}

public class SentryEnricher:
    ISentryEnricher<MessageCreateEvent>,
    ISentryEnricher<MessageDeleteEvent>,
    ISentryEnricher<MessageUpdateEvent>,
    ISentryEnricher<MessageDeleteBulkEvent>,
    ISentryEnricher<MessageReactionAddEvent>
{
    private readonly Bot _bot;
    private readonly BotConfig _config;

    public SentryEnricher(Bot bot, BotConfig config)
    {
        _bot = bot;
        _config = config;
    }

    // TODO: should this class take the Scope by dependency injection instead?
    // Would allow us to create a centralized "chain of handlers" where this class could just be registered as an entry in

    public void Enrich(Scope scope, int shardId, MessageCreateEvent evt)
    {
        scope.AddBreadcrumb(evt.Content, "event.message",
            data: new Dictionary<string, string>
            {
                {"user", evt.Author.Id.ToString()},
                {"channel", evt.ChannelId.ToString()},
                {"guild", evt.GuildId.ToString()},
                {"message", evt.Id.ToString()}
            });
        scope.SetTag("shard", shardId.ToString());
        if (_config.Cluster != null)
            scope.SetTag("cluster", _config.Cluster!.NodeIndex.ToString());

        // Also report information about the bot's permissions in the channel
        // We get a lot of permission errors so this'll be useful for determining problems

        // todo: re-add this
        // var perms = _bot.PermissionsIn(evt.ChannelId);
        // scope.AddBreadcrumb(perms.ToPermissionString(), "permissions");
    }

    public void Enrich(Scope scope, int shardId, MessageDeleteBulkEvent evt)
    {
        scope.AddBreadcrumb("", "event.messageDelete",
            data: new Dictionary<string, string>
            {
                {"channel", evt.ChannelId.ToString()},
                {"guild", evt.GuildId.ToString()},
                {"messages", string.Join(",", evt.Ids)}
            });
        scope.SetTag("shard", shardId.ToString());
        if (_config.Cluster != null)
            scope.SetTag("cluster", _config.Cluster!.NodeIndex.ToString());
    }

    public void Enrich(Scope scope, int shardId, MessageUpdateEvent evt)
    {
        scope.AddBreadcrumb(evt.Content.Value ?? "<unknown>", "event.messageEdit",
            data: new Dictionary<string, string>
            {
                {"channel", evt.ChannelId.ToString()},
                {"guild", evt.GuildId.Value.ToString()},
                {"message", evt.Id.ToString()}
            });
        scope.SetTag("shard", shardId.ToString());
        if (_config.Cluster != null)
            scope.SetTag("cluster", _config.Cluster!.NodeIndex.ToString());
    }

    public void Enrich(Scope scope, int shardId, MessageDeleteEvent evt)
    {
        scope.AddBreadcrumb("", "event.messageDelete",
            data: new Dictionary<string, string>
            {
                {"channel", evt.ChannelId.ToString()},
                {"guild", evt.GuildId.ToString()},
                {"message", evt.Id.ToString()}
            });
        scope.SetTag("shard", shardId.ToString());
        if (_config.Cluster != null)
            scope.SetTag("cluster", _config.Cluster!.NodeIndex.ToString());
    }

    public void Enrich(Scope scope, int shardId, MessageReactionAddEvent evt)
    {
        scope.AddBreadcrumb("", "event.reaction",
            data: new Dictionary<string, string>
            {
                {"user", evt.UserId.ToString()},
                {"channel", evt.ChannelId.ToString()},
                {"guild", (evt.GuildId ?? 0).ToString()},
                {"message", evt.MessageId.ToString()},
                {"reaction", evt.Emoji.Name}
            });
        scope.SetTag("shard", shardId.ToString());
        if (_config.Cluster != null)
            scope.SetTag("cluster", _config.Cluster!.NodeIndex.ToString());
    }
}
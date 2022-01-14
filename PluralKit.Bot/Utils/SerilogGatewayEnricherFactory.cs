using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;

using Serilog.Core;
using Serilog.Events;

namespace PluralKit.Bot;

public class SerilogGatewayEnricherFactory
{
    private readonly Bot _bot;
    private readonly IDiscordCache _cache;
    private readonly BotConfig _botConfig;

    public SerilogGatewayEnricherFactory(Bot bot, IDiscordCache cache, BotConfig botConfig)
    {
        _bot = bot;
        _cache = cache;
        _botConfig = botConfig;
    }

    public async Task<ILogEventEnricher> GetEnricher(int shardId, IGatewayEvent evt)
    {
        var props = new List<LogEventProperty> { new("ShardId", new ScalarValue(shardId)) };

        if (_botConfig.Cluster != null)
            props.Add(new LogEventProperty("ClusterId", new ScalarValue(_botConfig.Cluster.NodeName)));

        var (guild, channel) = GetGuildChannelId(evt);
        var user = GetUserId(evt);
        var message = GetMessageId(evt);

        if (guild != null)
            props.Add(new LogEventProperty("GuildId", new ScalarValue(guild.Value)));

        if (channel != null)
        {
            props.Add(new LogEventProperty("ChannelId", new ScalarValue(channel.Value)));

            if (await _cache.TryGetChannel(channel.Value) != null)
            {
                var botPermissions = await _cache.PermissionsIn(channel.Value);
                props.Add(new LogEventProperty("BotPermissions", new ScalarValue(botPermissions)));
            }
        }

        if (message != null)
            props.Add(new LogEventProperty("MessageId", new ScalarValue(message.Value)));

        if (user != null)
            props.Add(new LogEventProperty("UserId", new ScalarValue(user.Value)));

        if (evt is MessageCreateEvent mce)
            props.Add(new LogEventProperty("UserPermissions", new ScalarValue(await _cache.PermissionsFor(mce))));

        return new Inner(props);
    }

    private (ulong?, ulong?) GetGuildChannelId(IGatewayEvent evt) => evt switch
    {
        ChannelCreateEvent e => (e.GuildId, e.Id),
        ChannelUpdateEvent e => (e.GuildId, e.Id),
        ChannelDeleteEvent e => (e.GuildId, e.Id),
        MessageCreateEvent e => (e.GuildId, e.ChannelId),
        MessageUpdateEvent e => (e.GuildId.Value, e.ChannelId),
        MessageDeleteEvent e => (e.GuildId, e.ChannelId),
        MessageDeleteBulkEvent e => (e.GuildId, e.ChannelId),
        MessageReactionAddEvent e => (e.GuildId, e.ChannelId),
        MessageReactionRemoveEvent e => (e.GuildId, e.ChannelId),
        MessageReactionRemoveAllEvent e => (e.GuildId, e.ChannelId),
        MessageReactionRemoveEmojiEvent e => (e.GuildId, e.ChannelId),
        InteractionCreateEvent e => (e.GuildId, e.ChannelId),
        _ => (null, null)
    };

    private ulong? GetUserId(IGatewayEvent evt) => evt switch
    {
        MessageCreateEvent e => e.Author.Id,
        MessageUpdateEvent e => e.Author.HasValue ? e.Author.Value.Id : null,
        MessageReactionAddEvent e => e.UserId,
        MessageReactionRemoveEvent e => e.UserId,
        InteractionCreateEvent e => e.User?.Id ?? e.Member.User.Id,
        _ => null
    };

    private ulong? GetMessageId(IGatewayEvent evt) => evt switch
    {
        MessageCreateEvent e => e.Id,
        MessageUpdateEvent e => e.Id,
        MessageDeleteEvent e => e.Id,
        MessageReactionAddEvent e => e.MessageId,
        MessageReactionRemoveEvent e => e.MessageId,
        MessageReactionRemoveAllEvent e => e.MessageId,
        MessageReactionRemoveEmojiEvent e => e.MessageId,
        InteractionCreateEvent e => e.Message?.Id,
        _ => null
    };

    private record Inner(List<LogEventProperty> Properties): ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            foreach (var prop in Properties)
                logEvent.AddPropertyIfAbsent(prop);
        }
    }
}
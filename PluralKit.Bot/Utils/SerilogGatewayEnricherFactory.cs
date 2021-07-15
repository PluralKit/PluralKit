using System.Collections.Generic;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;

using Serilog.Core;
using Serilog.Events;

namespace PluralKit.Bot
{
    public class SerilogGatewayEnricherFactory
    {
        private readonly Bot _bot;
        private readonly IDiscordCache _cache;

        public SerilogGatewayEnricherFactory(Bot bot, IDiscordCache cache)
        {
            _bot = bot;
            _cache = cache;
        }

        public ILogEventEnricher GetEnricher(Shard shard, IGatewayEvent evt)
        {
            var props = new List<LogEventProperty>
            {
                new("ShardId", new ScalarValue(shard.ShardId)),
            };
            
            var (guild, channel) = GetGuildChannelId(evt);
            var user = GetUserId(evt);
            var message = GetMessageId(evt);
            
            if (guild != null)
                props.Add(new("GuildId", new ScalarValue(guild.Value)));
            
            if (channel != null)
            {
                props.Add(new("ChannelId", new ScalarValue(channel.Value)));

                if (_cache.TryGetChannel(channel.Value, out _))
                {
                    var botPermissions = _bot.PermissionsIn(channel.Value);
                    props.Add(new("BotPermissions", new ScalarValue(botPermissions)));
                }
            }
            
            if (message != null)
                props.Add(new("MessageId", new ScalarValue(message.Value)));
            
            if (user != null)
                props.Add(new("UserId", new ScalarValue(user.Value)));

            if (evt is MessageCreateEvent mce)
                props.Add(new("UserPermissions", new ScalarValue(_cache.PermissionsFor(mce))));

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
            InteractionCreateEvent e => e.Member.User.Id,
            _ => null,
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
            _ => null,
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
}
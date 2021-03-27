using System.Collections.Generic;

using Myriad.Gateway;

using Serilog.Core;
using Serilog.Events;

namespace PluralKit.Bot
{
    // This class is unused and commented out in Init.cs - it's here from before the lib conversion. Is it needed??
    public class EventDestructuring: IDestructuringPolicy
    {
        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
                                   out LogEventPropertyValue result)
        {
            if (!(value is IGatewayEvent evt))
            {
                result = null;
                return false;
            }

            var props = new List<LogEventProperty>
            {
                new("Type", new ScalarValue(evt.EventType())),
            };

            void AddMessage(ulong id, ulong channelId, ulong? guildId, ulong? author)
            {
                props.Add(new LogEventProperty("MessageId", new ScalarValue(id)));
                props.Add(new LogEventProperty("ChannelId", new ScalarValue(channelId)));
                props.Add(new LogEventProperty("GuildId", new ScalarValue(guildId ?? 0)));

                if (author != null)
                    props.Add(new LogEventProperty("AuthorId", new ScalarValue(author)));
            }

            if (value is MessageCreateEvent mc)
                AddMessage(mc.Id, mc.ChannelId, mc.GuildId, mc.Author.Id);
            else if (value is MessageUpdateEvent mu)
                AddMessage(mu.Id, mu.ChannelId, mu.GuildId.Value, mu.Author.Value?.Id);
            else if (value is MessageDeleteEvent md)
                AddMessage(md.Id, md.ChannelId, md.GuildId, null);
            else if (value is MessageReactionAddEvent mra)
            {
                AddMessage(mra.MessageId, mra.ChannelId, mra.GuildId, null);
                props.Add(new LogEventProperty("ReactingUserId", new ScalarValue(mra.Emoji)));
                props.Add(new LogEventProperty("Emoji", new ScalarValue(mra.Emoji.Name)));
            }
            
            // Want shard last, just for visual reasons
            // TODO: D#+ update means we can't pull shard ID out of this, what do?
            // props.Add(new LogEventProperty("Shard", new ScalarValue(dea.Client.ShardId)));

            result = new StructureValue(props);
            return true;
        }
    }
}
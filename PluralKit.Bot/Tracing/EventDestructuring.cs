using System.Collections.Generic;

using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

using Serilog.Core;
using Serilog.Events;

namespace PluralKit.Bot
{
    public class EventDestructuring: IDestructuringPolicy
    {
        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
                                   out LogEventPropertyValue result)
        {
            if (!(value is DiscordEventArgs dea))
            {
                result = null;
                return false;
            }

            var props = new List<LogEventProperty>
            {
                new LogEventProperty("Type", new ScalarValue(dea.EventType())),
            };

            void AddMessage(DiscordMessage msg)
            {
                props.Add(new LogEventProperty("MessageId", new ScalarValue(msg.Id)));
                props.Add(new LogEventProperty("ChannelId", new ScalarValue(msg.ChannelId)));
                props.Add(new LogEventProperty("GuildId", new ScalarValue(msg.Channel.GuildId)));

                if (msg.Author != null)
                    props.Add(new LogEventProperty("AuthorId", new ScalarValue(msg.Author.Id)));
            }

            if (value is MessageCreateEventArgs mc)
                AddMessage(mc.Message);
            else if (value is MessageUpdateEventArgs mu)
                AddMessage(mu.Message);
            else if (value is MessageDeleteEventArgs md)
                AddMessage(md.Message);
            else if (value is MessageReactionAddEventArgs mra)
            {
                AddMessage(mra.Message);
                props.Add(new LogEventProperty("ReactingUserId", new ScalarValue(mra.User.Id)));
                props.Add(new LogEventProperty("Emoji", new ScalarValue(mra.Emoji.GetDiscordName())));
            }
            
            // Want shard last, just for visual reasons
            props.Add(new LogEventProperty("Shard", new ScalarValue(dea.Client.ShardId)));

            result = new StructureValue(props);
            return true;
        }
    }
}
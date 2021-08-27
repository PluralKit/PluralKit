using System;

using Myriad.Types;

namespace Myriad.Extensions
{
    public static class SnowflakeExtensions
    {
        public static readonly DateTimeOffset DiscordEpoch = new(2015, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static DateTimeOffset SnowflakeToTimestamp(ulong snowflake) =>
            DiscordEpoch + TimeSpan.FromMilliseconds(snowflake >> 22);

        public static DateTimeOffset Timestamp(this Message msg) => SnowflakeToTimestamp(msg.Id);
        public static DateTimeOffset Timestamp(this Channel channel) => SnowflakeToTimestamp(channel.Id);
        public static DateTimeOffset Timestamp(this Guild guild) => SnowflakeToTimestamp(guild.Id);
        public static DateTimeOffset Timestamp(this Webhook webhook) => SnowflakeToTimestamp(webhook.Id);
        public static DateTimeOffset Timestamp(this User user) => SnowflakeToTimestamp(user.Id);
    }
}
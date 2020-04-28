using System;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;

using NodaTime;

namespace PluralKit.Bot
{
    public static class DiscordUtils
    {
        public static DiscordColor Blue = new DiscordColor(0x1f99d8);
        public static DiscordColor Green = new DiscordColor(0x00cc78);
        public static DiscordColor Red = new DiscordColor(0xef4b3d);
        public static DiscordColor Gray = new DiscordColor(0x979c9f);
        
        public static string NameAndMention(this DiscordUser user) {
            return $"{user.Username}#{user.Discriminator} ({user.Mention})";
        }

        public static async Task<Permissions> PermissionsIn(this DiscordChannel channel, DiscordUser user)
        {
            if (channel.Guild != null)
            {
                var member = await channel.Guild.GetMemberAsync(user.Id);
                return member.PermissionsIn(channel);
            }
            
            if (channel.Type == ChannelType.Private)
                return (Permissions) 0b00000_1000110_1011100110000_000000;

            return Permissions.None;
        }

        public static Permissions PermissionsInSync(this DiscordChannel channel, DiscordUser user)
        {
            if (user is DiscordMember dm && channel.Guild != null)
                return dm.PermissionsIn(channel);
            
            if (channel.Type == ChannelType.Private)
                return (Permissions) 0b00000_1000110_1011100110000_000000;

            return Permissions.None;
        }

        public static Permissions BotPermissions(this DiscordChannel channel)
        {
            if (channel.Guild != null)
            {
                var member = channel.Guild.CurrentMember;
                return channel.PermissionsFor(member);
            }

            if (channel.Type == ChannelType.Private)
                return (Permissions) 0b00000_1000110_1011100110000_000000;

            return Permissions.None;
        }

        public static bool BotHasPermission(this DiscordChannel channel, Permissions permissionSet) =>
            (BotPermissions(channel) & permissionSet) == permissionSet;

        public static Instant SnowflakeToInstant(ulong snowflake) => 
            Instant.FromUtc(2015, 1, 1, 0, 0, 0) + Duration.FromMilliseconds(snowflake >> 22);

        public static ulong InstantToSnowflake(Instant time) =>
            (ulong) (time - Instant.FromUtc(2015, 1, 1, 0, 0, 0)).TotalMilliseconds >> 22;

        public static ulong InstantToSnowflake(DateTimeOffset time) =>
            (ulong) (time - new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalMilliseconds >> 22;

        public static async Task CreateReactionsBulk(this DiscordMessage msg, string[] reactions)
        {
            foreach (var reaction in reactions)
            {
                await msg.CreateReactionAsync(DiscordEmoji.FromUnicode(reaction));
            }
        }
    }
}
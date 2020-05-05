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
        
        public static Permissions DM_PERMISSIONS = (Permissions) 0b00000_1000110_1011100110000_000000;
        
        public static string NameAndMention(this DiscordUser user) {
            return $"{user.Username}#{user.Discriminator} ({user.Mention})";
        }

        // We funnel all "permissions from DiscordMember" calls through here 
        // This way we can ensure we do the read permission correction everywhere
        private static Permissions PermissionsInGuild(DiscordChannel channel, DiscordMember member)
        {
            var permissions = channel.PermissionsFor(member);
            
            // This method doesn't account for channels without read permissions
            // If we don't have read permissions in the channel, we don't have *any* permissions
            if ((permissions & Permissions.AccessChannels) != Permissions.AccessChannels)
                return Permissions.None;
            
            return permissions;
        }

        public static async Task<Permissions> PermissionsIn(this DiscordChannel channel, DiscordUser user)
        {
            // Just delegates to PermissionsInSync, but handles the case of a non-member User in a guild properly
            // This is a separate method because it requires an async call
            if (channel.Guild != null && !(user is DiscordMember))
                return PermissionsInSync(channel, await channel.Guild.GetMemberAsync(user.Id));
            return PermissionsInSync(channel, user);
        }
        
        // Same as PermissionsIn, but always synchronous. DiscordUser must be a DiscordMember if channel is in guild.
        public static Permissions PermissionsInSync(this DiscordChannel channel, DiscordUser user)
        {
            if (channel.Guild != null && !(user is DiscordMember))
                throw new ArgumentException("Function was passed a guild channel but a non-member DiscordUser");
            
            if (user is DiscordMember m) return PermissionsInGuild(channel, m);
            if (channel.Type == ChannelType.Private) return DM_PERMISSIONS;
            return Permissions.None;
        }

        public static Permissions BotPermissions(this DiscordChannel channel)
        {
            // TODO: can we get a CurrentMember somehow without a guild context?
            // at least, without somehow getting a DiscordClient reference as an arg(which I don't want to do)
            if (channel.Guild != null)
                return PermissionsInSync(channel, channel.Guild.CurrentMember);
            if (channel.Type == ChannelType.Private) return DM_PERMISSIONS;
            return Permissions.None;
        }

        public static bool BotHasAllPermissions(this DiscordChannel channel, Permissions permissionSet) =>
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

        public static string WorkaroundForUrlBug(string input)
        {
            // Workaround for https://github.com/DSharpPlus/DSharpPlus/issues/565
            return input.Replace("%20", "+");
        }
    }
}
using Discord;
using Discord.WebSocket;

namespace PluralKit.Bot
{
    public static class DiscordUtils
    {
        public static string NameAndMention(this IUser user) {
            return $"{user.Username}#{user.Discriminator} ({user.Mention})";
        }
        
        public static ChannelPermissions PermissionsIn(this IChannel channel)
        {
            switch (channel)
            {
                case IDMChannel _:
                    return ChannelPermissions.DM;
                case IGroupChannel _:
                    return ChannelPermissions.Group;
                case SocketGuildChannel gc:
                    var currentUser = gc.Guild.CurrentUser;
                    return currentUser.GetPermissions(gc);
                default:
                    return ChannelPermissions.None;
            }
        }

        public static bool HasPermission(this IChannel channel, ChannelPermission permission) =>
            PermissionsIn(channel).Has(permission);
    }
}
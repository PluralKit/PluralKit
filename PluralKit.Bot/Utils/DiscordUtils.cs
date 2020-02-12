using System.Threading.Tasks;

using Discord;

namespace PluralKit.Bot
{
    public static class DiscordUtils
    {
        public static string NameAndMention(this IUser user) {
            return $"{user.Username}#{user.Discriminator} ({user.Mention})";
        }
        
        public static async Task<ChannelPermissions> PermissionsIn(this IChannel channel)
        {
            switch (channel)
            {
                case IDMChannel _:
                    return ChannelPermissions.DM;
                case IGroupChannel _:
                    return ChannelPermissions.Group;
                case IGuildChannel gc:
                    var currentUser = await gc.Guild.GetCurrentUserAsync();
                    return currentUser.GetPermissions(gc);
                default:
                    return ChannelPermissions.None;
            }
        }

        public static async Task<bool> HasPermission(this IChannel channel, ChannelPermission permission) =>
            (await PermissionsIn(channel)).Has(permission);
    }
}
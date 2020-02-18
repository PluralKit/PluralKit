using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

using Discord;
using Discord.Net;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class MiscUtils {
        public static string ProxyTagsString(this PKMember member) => string.Join(", ", member.ProxyTags.Select(t => $"`{t.ProxyString.EscapeMarkdown()}`"));
        
        public static bool IsOurProblem(this Exception e)
        {
            // This function filters out sporadic errors out of our control from being reported to Sentry
            // otherwise we'd blow out our error reporting budget as soon as Discord takes a dump, or something.
            
            // Discord server errors are *not our problem*
            if (e is HttpException he && ((int) he.HttpCode) >= 500) return false;
            
            // Socket errors are *not our problem*
            if (e is SocketException) return false;
            
            // Tasks being cancelled for whatver reason are, you guessed it, also not our problem.
            if (e is TaskCanceledException) return false;
            
            // This may expanded at some point.
            return true;
        }

        public static async Task<bool> EnsureProxyPermissions(ITextChannel channel)
        {
            var guildUser = await channel.Guild.GetCurrentUserAsync();
            var permissions = guildUser.GetPermissions(channel);

            // If we can't send messages at all, just bail immediately.
            // TODO: can you have ManageMessages and *not* SendMessages? What happens then?
            if (!permissions.SendMessages && !permissions.ManageMessages) return false;

            if (!permissions.ManageWebhooks)
            {
                throw Errors.MissingPermissions("Manage Webhooks", "proxy messages");
                return false;
            }

            if (!permissions.ManageMessages)
            {
                throw Errors.MissingPermissions("Manage Messages", "delete the original trigger message");
                return false;
            }

            return true;
        }
    }
}
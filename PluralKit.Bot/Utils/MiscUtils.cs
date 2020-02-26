using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

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
            
            // Webhook server errors are also *not our problem*
            if (e is WebhookExecutionErrorOnDiscordsEnd) return false;
            
            // Socket errors are *not our problem*
            if (e is SocketException) return false;
            
            // Tasks being cancelled for whatver reason are, you guessed it, also not our problem.
            if (e is TaskCanceledException) return false;

            // Sometimes Discord just times everything out.
            if (e is TimeoutException) return false;
            
            // This may expanded at some point.
            return true;
        }
    }
}
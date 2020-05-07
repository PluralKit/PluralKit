using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

using DSharpPlus.Exceptions;

using Newtonsoft.Json;

using Npgsql;

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
            // TODO: DSharpPlus doesn't have a generic "HttpException" type and only special cases a couple response codes (that we don't need here)
            // Doesn't seem to handle 500s in the library at all, I'm not sure what it does in case it receives one...
            // if (e is DSharpPlus.Exceptions he && ((int) he.HttpCode) >= 500) return false;

            // Occasionally Discord's API will Have A Bad Time and return a bunch of CloudFlare errors (in HTML format).
            // The library tries to parse these HTML responses as JSON and crashes with a consistent exception message.
            if (e is JsonReaderException jre && jre.Message == "Unexpected character encountered while parsing value: <. Path '', line 0, position 0.") return false;
            
            // Webhook server errors are also *not our problem*
            // (this includes rate limit errors, WebhookRateLimited is a subclass)
            if (e is WebhookExecutionErrorOnDiscordsEnd) return false;
            
            // Socket errors are *not our problem*
            if (e is SocketException) return false;
            
            // Tasks being cancelled for whatver reason are, you guessed it, also not our problem.
            if (e is TaskCanceledException) return false;

            // Sometimes Discord just times everything out.
            if (e is TimeoutException) return false;
            
            // Ignore "Database is shutting down" error
            if (e is PostgresException pe && pe.SqlState == "57P03") return false;
            
            // This may expanded at some point.
            return true;
        }

        public static string ExtractError(BadRequestException e)
        {
            return e.WebResponse.Response;
        }
    }
}
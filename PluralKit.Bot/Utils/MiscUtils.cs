using System.Net;
using System.Net.Sockets;
using System.Globalization;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;

using Newtonsoft.Json;

using Npgsql;

using PluralKit.Core;

using Polly.Timeout;

namespace PluralKit.Bot;

public static class MiscUtils
{
    public static string ProxyTagsString(this PKMember member, string separator = ", ") =>
        string.Join(separator, member.ProxyTags.Select(t => t.ProxyString.AsCode()));

    public static bool IsOurProblem(this Exception e)
    {
        // This function filters out sporadic errors out of our control from being reported to Sentry
        // otherwise we'd blow out our error reporting budget as soon as Discord takes a dump, or something.

        // Occasionally Discord's API will Have A Bad Time and return a bunch of CloudFlare errors (in HTML format).
        // The library tries to parse these HTML responses as JSON and crashes with a consistent exception message.
        if (e is JsonReaderException jre && jre.Message ==
            "Unexpected character encountered while parsing value: <. Path '', line 0, position 0.") return false;

        // And now (2020-05-12), apparently Discord returns these weird responses occasionally. Also not our problem.
        if (e is BadRequestException bre && bre.ResponseBody.Contains("<center>nginx</center>")) return false;
        if (e is NotFoundException ne && ne.ResponseBody.Contains("<center>nginx</center>")) return false;
        if (e is UnauthorizedException ue && ue.ResponseBody.Contains("<center>nginx</center>")) return false;

        // Filter out timeout/ratelimit related stuff
        if (e is TooManyRequestsException) return false;
        if (e is RatelimitBucketExhaustedException) return false;
        if (e is TimeoutRejectedException) return false;

        // 5xxs? also not our problem :^)
        if (e is UnknownDiscordRequestException udre && (int)udre.StatusCode >= 500) return false;

        // 409s apparently happen for Discord internal issues.
        if (e is UnknownDiscordRequestException udre2 && udre2.StatusCode == HttpStatusCode.Conflict) return false;

        // Webhook server errors are also *not our problem*
        // (this includes rate limit errors, WebhookRateLimited is a subclass)
        if (e is WebhookExecutionErrorOnDiscordsEnd) return false;

        // Socket errors are *not our problem*
        // if (e.GetBaseException() is SocketException) return false;

        // Tasks being cancelled for whatver reason are, you guessed it, also not our problem.
        // if (e is TaskCanceledException) return false;

        // Sometimes Discord just times everything out.
        // if (e is TimeoutException) return false;
        if (e is UnknownDiscordRequestException tde && tde.Message == "Request Timeout") return false;

        // HTTP/2 streams are complicated and break sometimes.
        // if (e is HttpRequestException) return false;

        // This may expanded at some point.
        return true;
    }

    public static bool ShowToUser(this Exception e)
    {
        if (e is PostgresException pe)
        {
            // ignore "cached plan must not change result type" error
            if (pe.SqlState == "0A000") return false;

            // Ignore "Database is shutting down" error
            if (pe.SqlState == "57P03") return false;

            // Ignore *other* "database is shutting down" error (57P01)
            if (pe.SqlState == "57P01") return false;

            // ignore "out of shared memory" error
            if (pe.SqlState == "53200") return false;

            // ignore "too many clients already" error
            if (pe.SqlState == "53300") return false;
        }

        // Ignore database timing out as well.
        if (e is NpgsqlException tpe && tpe.InnerException is TimeoutException)
            return false;

        if (e is NpgsqlException npe &&
            (
                // Ignore thread pool exhaustion errors
                npe.Message.Contains("The connection pool has been exhausted")
             // ignore "Exception while reading from stream"
             || npe.Message.Contains("Exception while reading from stream")
             // ignore "Exception while connecting"
             || npe.Message.Contains("Exception while connecting")
            ))
            return false;

        return true;
    }

    public static MultipartFile GenerateColorPreview(string color)
    {
        //generate a 128x128 solid color gif from bytes
        //image data is a 1x1 pixel, using the background color to fill the rest of the canvas
        var imgBytes = new byte[]
        {
            0x47, 0x49, 0x46, 0x38, 0x39, 0x61, // Header
            0x80, 0x00, 0x80, 0x00, 0x80, 0x00, 0x00, // Logical Screen Descriptor
            0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, // Global Color Table
            0x21, 0xF9, 0x04, 0x08, 0x00, 0x00, 0x00, 0x00, // Graphics Control Extension
            0x2C, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, // Image Descriptor
            0x02, 0x02, 0x4C, 0x01, 0x00, // Image Data
            0x3B // Trailer
        }; //indices 13, 14 and 15 are the R, G, and B values respectively

        imgBytes[13] = byte.Parse(color.Substring(0, 2), NumberStyles.HexNumber);
        imgBytes[14] = byte.Parse(color.Substring(2, 2), NumberStyles.HexNumber);
        imgBytes[15] = byte.Parse(color.Substring(4, 2), NumberStyles.HexNumber);

        return new MultipartFile("color.gif", new MemoryStream(imgBytes), null, null, null);
    }
}
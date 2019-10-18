using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;

using PluralKit.Core;
using Image = SixLabors.ImageSharp.Image;

namespace PluralKit.Bot
{
    public static class Utils {
        public static string NameAndMention(this IUser user) {
            return $"{user.Username}#{user.Discriminator} ({user.Mention})";
        }

        public static Color? ToDiscordColor(this string color)
        {
            if (uint.TryParse(color, NumberStyles.HexNumber, null, out var colorInt))
                return new Color(colorInt);
            throw new ArgumentException($"Invalid color string '{color}'.");
        }

        public static async Task VerifyAvatarOrThrow(string url)
        {
            // List of MIME types we consider acceptable
            var acceptableMimeTypes = new[]
            {
                "image/jpeg",
                "image/gif",
                "image/png"
                // TODO: add image/webp once ImageSharp supports this
            };

            using (var client = new HttpClient())
            {
                Uri uri;
                try
                {
                    uri = new Uri(url);
                    if (!uri.IsAbsoluteUri) throw Errors.InvalidUrl(url);
                }
                catch (UriFormatException)
                {
                    throw Errors.InvalidUrl(url);
                }

                var response = await client.GetAsync(uri);
                if (!response.IsSuccessStatusCode) // Check status code
                    throw Errors.AvatarServerError(response.StatusCode);
                if (response.Content.Headers.ContentLength == null) // Check presence of content length
                    throw Errors.AvatarNotAnImage(null);
                if (response.Content.Headers.ContentLength > Limits.AvatarFileSizeLimit) // Check content length
                    throw Errors.AvatarFileSizeLimit(response.Content.Headers.ContentLength.Value);
                if (!acceptableMimeTypes.Contains(response.Content.Headers.ContentType.MediaType)) // Check MIME type
                    throw Errors.AvatarNotAnImage(response.Content.Headers.ContentType.MediaType);

                // Parse the image header in a worker
                var stream = await response.Content.ReadAsStreamAsync();
                var image = await Task.Run(() => Image.Identify(stream));
                if (image.Width > Limits.AvatarDimensionLimit || image.Height > Limits.AvatarDimensionLimit) // Check image size
                    throw Errors.AvatarDimensionsTooLarge(image.Width, image.Height);
            }
        }

        public static bool HasMentionPrefix(string content, ref int argPos, out ulong mentionId)
        {
            mentionId = 0;
            
            // Roughly ported from Discord.Commands.MessageExtensions.HasMentionPrefix
            if (string.IsNullOrEmpty(content) || content.Length <= 3 || (content[0] != '<' || content[1] != '@'))
                return false;
            int num = content.IndexOf('>');
            if (num == -1 || content.Length < num + 2 || content[num + 1] != ' ' || !MentionUtils.TryParseUser(content.Substring(0, num + 1), out mentionId))
                return false;
            argPos = num + 2;
            return true;
        }

        public static bool TryParseMention(this string potentialMention, out ulong id)
        {
            if (ulong.TryParse(potentialMention, out id)) return true;
            if (MentionUtils.TryParseUser(potentialMention, out id)) return true;
            return false;
        }

        public static string SanitizeMentions(this string input) =>
            Regex.Replace(Regex.Replace(input, "<@[!&]?(\\d{17,19})>", "<\u200B@$1>"), "@(everyone|here)", "@\u200B$1");

        public static string SanitizeEveryone(this string input) =>
            Regex.Replace(input, "@(everyone|here)", "@\u200B$1");

        public static string EscapeMarkdown(this string input)
        {
            Regex pattern = new Regex(@"[*_~>`(||)\\]", RegexOptions.Multiline);
            if (input != null) return pattern.Replace(input, @"\$&");
            else return input;
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

    /// <summary>
    /// An exception class representing user-facing errors caused when parsing and executing commands.
    /// </summary>
    public class PKError : Exception
    {
        public PKError(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// A subclass of <see cref="PKError"/> that represent command syntax errors, meaning they'll have their command
    /// usages printed in the message.
    /// </summary>
    public class PKSyntaxError : PKError
    {
        public PKSyntaxError(string message) : base(message)
        {
        }
    }
}
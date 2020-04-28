using System;
using System.Globalization;

namespace PluralKit.Bot.Utils
{
    // PK note: class is wholesale copied from Discord.NET (MIT-licensed)
    // https://github.com/discord-net/Discord.Net/blob/ff0fea98a65d907fbce07856f1a9ef4aebb9108b/src/Discord.Net.Core/Utils/MentionUtils.cs
    
    /// <summary>
    ///     Provides a series of helper methods for parsing mentions.
    /// </summary>
    public static class MentionUtils
    {
        private const char SanitizeChar = '\x200b';

        //If the system can't be positive a user doesn't have a nickname, assume useNickname = true (source: Jake)
        internal static string MentionUser(string id, bool useNickname = true) => useNickname ? $"<@!{id}>" : $"<@{id}>";
        /// <summary>
        ///     Returns a mention string based on the user ID.
        /// </summary>
        /// <returns>
        ///     A user mention string (e.g. &lt;@80351110224678912&gt;).
        /// </returns>
        public static string MentionUser(ulong id) => MentionUser(id.ToString(), true);
        internal static string MentionChannel(string id) => $"<#{id}>";
        /// <summary>
        ///     Returns a mention string based on the channel ID.
        /// </summary>
        /// <returns>
        ///     A channel mention string (e.g. &lt;#103735883630395392&gt;).
        /// </returns>
        public static string MentionChannel(ulong id) => MentionChannel(id.ToString());
        internal static string MentionRole(string id) => $"<@&{id}>";
        /// <summary>
        ///     Returns a mention string based on the role ID.
        /// </summary>
        /// <returns>
        ///     A role mention string (e.g. &lt;@&amp;165511591545143296&gt;).
        /// </returns>
        public static string MentionRole(ulong id) => MentionRole(id.ToString());

        /// <summary>
        ///     Parses a provided user mention string.
        /// </summary>
        /// <exception cref="ArgumentException">Invalid mention format.</exception>
        public static ulong ParseUser(string text)
        {
            if (TryParseUser(text, out ulong id))
                return id;
            throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
        }
        /// <summary>
        ///     Tries to parse a provided user mention string.
        /// </summary>
        public static bool TryParseUser(string text, out ulong userId)
        {
            if (text.Length >= 3 && text[0] == '<' && text[1] == '@' && text[text.Length - 1] == '>')
            {
                if (text.Length >= 4 && text[2] == '!')
                    text = text.Substring(3, text.Length - 4); //<@!123>
                else
                    text = text.Substring(2, text.Length - 3); //<@123>

                if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out userId))
                    return true;
            }
            userId = 0;
            return false;
        }

        /// <summary>
        ///     Parses a provided channel mention string.
        /// </summary>
        /// <exception cref="ArgumentException">Invalid mention format.</exception>
        public static ulong ParseChannel(string text)
        {
            if (TryParseChannel(text, out ulong id))
                return id;
            throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
        }
        /// <summary>
        ///     Tries to parse a provided channel mention string.
        /// </summary>
        public static bool TryParseChannel(string text, out ulong channelId)
        {
            if (text.Length >= 3 && text[0] == '<' && text[1] == '#' && text[text.Length - 1] == '>')
            {
                text = text.Substring(2, text.Length - 3); //<#123>

                if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out channelId))
                    return true;
            }
            channelId = 0;
            return false;
        }

        /// <summary>
        ///     Parses a provided role mention string.
        /// </summary>
        /// <exception cref="ArgumentException">Invalid mention format.</exception>
        public static ulong ParseRole(string text)
        {
            if (TryParseRole(text, out ulong id))
                return id;
            throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
        }
        /// <summary>
        ///     Tries to parse a provided role mention string.
        /// </summary>
        public static bool TryParseRole(string text, out ulong roleId)
        {
            if (text.Length >= 4 && text[0] == '<' && text[1] == '@' && text[2] == '&' && text[text.Length - 1] == '>')
            {
                text = text.Substring(3, text.Length - 4); //<@&123>

                if (ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out roleId))
                    return true;
            }
            roleId = 0;
            return false;
        }
    }
}
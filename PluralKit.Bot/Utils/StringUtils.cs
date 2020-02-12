using System;
using System.Globalization;
using System.Text.RegularExpressions;

using Discord;

namespace PluralKit.Bot
{
    public static class StringUtils
    {
        public static Color? ToDiscordColor(this string color)
        {
            if (uint.TryParse(color, NumberStyles.HexNumber, null, out var colorInt))
                return new Color(colorInt);
            throw new ArgumentException($"Invalid color string '{color}'.");
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
    }
}
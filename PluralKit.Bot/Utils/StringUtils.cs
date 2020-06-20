using System;
using System.Globalization;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;

namespace PluralKit.Bot
{
    public static class StringUtils
    {
        private static readonly Regex USER_MENTION = new Regex("^<@!?(\\d{17,19})>$");
        public static DiscordColor? ToDiscordColor(this string color)
        {
            if (int.TryParse(color, NumberStyles.HexNumber, null, out var colorInt))
                return new DiscordColor(colorInt);
            throw new ArgumentException($"Invalid color string '{color}'.");
        }
        
        public static bool HasMentionPrefix(string content, ref int argPos, out ulong mentionId)
        {
            mentionId = 0;
            
            // Roughly ported from Discord.Commands.MessageExtensions.HasMentionPrefix
            if (string.IsNullOrEmpty(content) || content.Length <= 3 || (content[0] != '<' || content[1] != '@'))
                return false;
            int num = content.IndexOf('>');
            if (num == -1 || content.Length < num + 2 || content[num + 1] != ' ' || !TryParseMention(content.Substring(0, num + 1), out mentionId))
                return false;
            argPos = num + 2;
            return true;
        }

        public static bool TryParseMention(this string potentialMention, out ulong id)
        {
            if (ulong.TryParse(potentialMention, out id)) return true;

            var match = USER_MENTION.Match(potentialMention);
            if (match.Success) 
            {
                id = ulong.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
                return true;
            }

            return false;
        }

        public static string EscapeMarkdown(this string input)
        {
            Regex pattern = new Regex(@"[*_~>`(||)\\]", RegexOptions.Multiline);
            if (input != null) return pattern.Replace(input, @"\$&");
            else return input;
        }
    }
}
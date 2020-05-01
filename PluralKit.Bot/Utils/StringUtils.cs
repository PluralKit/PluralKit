using System;
using System.Globalization;
using System.Text.RegularExpressions;

using DSharpPlus.Entities;

namespace PluralKit.Bot
{
    public static class StringUtils
    {
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
            
            // Roughly ported from Discord.MentionUtils.TryParseUser
            if (potentialMention.Length >= 3 && potentialMention[0] == '<' && potentialMention[1] == '@' && potentialMention[potentialMention.Length - 1] == '>')
            {
                if (potentialMention.Length >= 4 && potentialMention[2] == '!')
                    potentialMention = potentialMention.Substring(3, potentialMention.Length - 4); //<@!123>
                else
                    potentialMention = potentialMention.Substring(2, potentialMention.Length - 3); //<@123>
                
                if (ulong.TryParse(potentialMention, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                    return true;
            }
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
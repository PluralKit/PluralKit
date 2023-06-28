using System.Globalization;
using System.Text.RegularExpressions;

using Myriad.Builders;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class DiscordUtils
{
    public const uint Blue = 0x1f99d8;
    public const uint Green = 0x00cc78;
    public const uint Red = 0xef4b3d;
    public const uint Gray = 0x979c9f;

    private static readonly Regex USER_MENTION = new("<@!?(\\d{17,19})>");
    private static readonly Regex ROLE_MENTION = new("<@&(\\d{17,19})>");
    private static readonly Regex EVERYONE_HERE_MENTION = new("@(everyone|here)");

    // Discord uses Khan Academy's simple-markdown library for parsing Markdown,
    // which uses the following regex for link detection: 
    // ^(https?:\/\/[^\s<]+[^<.,:;"')\]\s])
    // Source: https://raw.githubusercontent.com/DJScias/Discord-Datamining/master/2020/2020-07-10/47efb8681861cb7c5ffa.js @ line 20633
    // corresponding to: https://github.com/Khan/simple-markdown/blob/master/src/index.js#L1489
    // I added <? and >? at the start/end; they need to be handled specially later...
    private static readonly Regex UNBROKEN_LINK_REGEX = new("<?(https?:\\/\\/[^\\s<]+[^<.,:;\"')\\]\\s])>?");

    public static string NameAndMention(this User user) =>
        $"{user.Username}{(user.Discriminator == "0" ? "" : $"#{user.Discriminator}")} ({user.Mention()})";

    public static Instant SnowflakeToInstant(ulong snowflake) =>
        Instant.FromUtc(2015, 1, 1, 0, 0, 0) + Duration.FromMilliseconds(snowflake >> 22);

    public static ulong InstantToSnowflake(Instant time) =>
        (ulong)(time - Instant.FromUtc(2015, 1, 1, 0, 0, 0)).TotalMilliseconds << 22;

    public static async Task CreateReactionsBulk(this DiscordApiClient rest, Message msg, string[] reactions)
    {
        foreach (var reaction in reactions)
            await rest.CreateReaction(msg.ChannelId, msg.Id, new Emoji { Name = reaction });
    }

    public static async Task<Guild?> GetGuildOrNull(this DiscordApiClient rest, ulong guildId)
    {
        try
        {
            return await rest.GetGuild(guildId);
        }
        catch (ForbiddenException)
        {
            // no permission, couldn't fetch, oh well
            return null;
        }
    }

    public static async Task<Channel?> GetChannelOrNull(this DiscordApiClient rest, ulong channelId)
    {
        try
        {
            return await rest.GetChannel(channelId);
        }
        catch (ForbiddenException)
        {
            // no permission, couldn't fetch, oh well
            return null;
        }
    }

    public static async Task<Message?> GetMessageOrNull(this DiscordApiClient rest, ulong channelId,
                                                        ulong messageId)
    {
        try
        {
            return await rest.GetMessage(channelId, messageId);
        }
        catch (ForbiddenException)
        {
            // no permission, couldn't fetch, oh well
            return null;
        }
    }

    public static uint? ToDiscordColor(this string color)
    {
        if (uint.TryParse(color, NumberStyles.HexNumber, null, out var colorInt))
            return colorInt;
        throw new ArgumentException($"Invalid color string '{color}'.");
    }

    public static bool HasMentionPrefix(string content, ref int argPos, out ulong mentionId)
    {
        mentionId = 0;

        // Roughly ported from Discord.Commands.MessageExtensions.HasMentionPrefix
        if (string.IsNullOrEmpty(content) || content.Length <= 3 || content[0] != '<' || content[1] != '@')
            return false;
        var num = content.IndexOf('>');
        if (num == -1 || content.Length < num + 2 || content[num + 1] != ' ' ||
            !TryParseMention(content.Substring(0, num + 1), out mentionId))
            return false;
        argPos = num + 2;
        return true;
    }

    public static bool TryParseMention(this string potentialMention, out ulong id)
    {
        if (ulong.TryParse(potentialMention, out id)) return true;

        var match = USER_MENTION.Match(potentialMention);
        if (match.Success && match.Index == 0 && match.Length == potentialMention.Length)
        {
            id = ulong.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    public static AllowedMentions ParseMentions(this string input)
    {
        var users = USER_MENTION.Matches(input).Select(x => ulong.Parse(x.Groups[1].Value));
        var roles = ROLE_MENTION.Matches(input).Select(x => ulong.Parse(x.Groups[1].Value));
        var everyone = EVERYONE_HERE_MENTION.IsMatch(input);

        return new AllowedMentions
        {
            Users = users.Distinct().ToArray(),
            Roles = roles.Distinct().ToArray(),
            Parse = everyone ? new[] { AllowedMentions.ParseType.Everyone } : null
        };
    }

    public static AllowedMentions RemoveUnmentionableRoles(this AllowedMentions mentions, Guild guild)
    {
        return mentions with
        {
            Roles = mentions.Roles
                ?.Where(id => guild.Roles.FirstOrDefault(r => r.Id == id)?.Mentionable == true)
                .ToArray()
        };
    }

    public static string EscapeMarkdown(this string input)
    {
        var pattern = new Regex(@"[*_~>`(||)\\]", RegexOptions.Multiline);
        if (input != null) return pattern.Replace(input, @"\$&");
        return input;
    }

    public static string EscapeBacktickPair(this string input)
    {
        if (input == null)
            return null;

        // Break all pairs of backticks by placing a ZWNBSP (U+FEFF) between them.
        // Run twice to catch any pairs that are created from the first pass
        var escaped = input
            .Replace("``", "`\ufeff`")
            .Replace("``", "`\ufeff`");

        // Escape the start/end of the string if necessary to better "connect" with other things
        if (escaped.StartsWith("`")) escaped = "\ufeff" + escaped;
        if (escaped.EndsWith("`")) escaped = escaped + "\ufeff";

        return escaped;
    }

    public static string AsCode(this string input) =>
        // Inline code blocks started with two backticks need to end with two backticks
        // So, surrounding with two backticks, then escaping all backtick pairs makes it impossible(!) to "break out"
        $"``{EscapeBacktickPair(input)}``";

    public static EmbedBuilder WithSimpleLineContent(this EmbedBuilder eb, IEnumerable<string> lines)
    {
        static int CharacterLimit(int pageNumber) =>
            // First chunk goes in description (2048 chars), rest go in embed values (1000 chars)
            pageNumber == 0 ? 4096 : 1024;

        var linesWithEnding = lines.Select(l => $"{l}\n");
        var pages = StringUtils.JoinPages(linesWithEnding, CharacterLimit);

        // Add the first page to the embed description
        if (pages.Count > 0)
            eb.Description(pages[0]);

        // Add the rest to blank-named (\u200B) fields
        for (var i = 1; i < pages.Count; i++)
            eb.Field(new Embed.Field("\u200B", pages[i]));

        return eb;
    }

    public static string BreakLinkEmbeds(this string str) =>
        // Encases URLs in <brackets>
        UNBROKEN_LINK_REGEX.Replace(str, match =>
        {
            // Don't break already-broken links
            // The regex will include the brackets in the match, so we can check for their presence here
            if (match.Value.StartsWith("<") && match.Value.EndsWith(">"))
                return match.Value;
            return $"<{match.Value}>";
        });

    public static string EventType(this IGatewayEvent evt) =>
        evt.GetType().Name.Replace("Event", "");

    public static async Task<bool> HasReactionPermissions(Context ctx)
    {
        var neededPermissions = PermissionSet.AddReactions | PermissionSet.ReadMessageHistory;
        return (await ctx.BotPermissions & neededPermissions) == neededPermissions;
    }

    public static bool IsValidGuildChannel(Channel channel) =>
        channel.Type is
            Channel.ChannelType.GuildText or
            Channel.ChannelType.GuildVoice or
            Channel.ChannelType.GuildNews or
            Channel.ChannelType.GuildPublicThread or
            Channel.ChannelType.GuildPrivateThread or
            Channel.ChannelType.GuildNewsThread or
            Channel.ChannelType.GuildStageVoice;
}
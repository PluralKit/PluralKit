using System.Text.RegularExpressions;

using Dapper;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using NodaTime;
using NodaTime.Extensions;
using NodaTime.Text;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public class LoggerCleanService
{
    private static readonly Regex _basicRegex = new("(\\d{17,19})");
    private static readonly Regex _dynoRegex = new("Message ID: (\\d{17,19})");
    private static readonly Regex _carlRegex = new("Message ID: (\\d{17,19})");
    private static readonly Regex _sapphireRegex = new("\\*\\*Message ID:\\*\\* \\[(\\d{17,19})\\]");
    private static readonly Regex _makiRegex = new("Message ID: (\\d{17,19})");
    private static readonly Regex _circleRegex = new("\\(`(\\d{17,19})`\\)");
    private static readonly Regex _loggerARegex = new("Message = (\\d{17,19})");
    private static readonly Regex _loggerBRegex = new("MessageID:(\\d{17,19})");
    private static readonly Regex _auttajaRegex = new("Message (\\d{17,19}) deleted");

    private static readonly Regex _mantaroRegex =
        new("Message \\(?ID:? (\\d{17,19})\\)? created by .* in channel .* was deleted\\.");

    private static readonly Regex _pancakeRegex = new("Message from <@(\\d{17,19})> deleted in");
    private static readonly Regex _unbelievaboatRegex = new("Message ID: (\\d{17,19})");
    private static readonly Regex _vanessaRegex = new("Message sent by <@!?(\\d{17,19})> deleted in");
    private static readonly Regex _salRegex = new("\\(ID: (\\d{17,19})\\)");
    private static readonly Regex _GearBotRegex = new("\\(``(\\d{17,19})``\\) in <#\\d{17,19}> has been removed.");
    private static readonly Regex _GiselleRegex = new("\\*\\*Message ID\\*\\*: `(\\d{17,19})`");
    private static readonly Regex _ProBotRegex = new("\\*\\*Message sent by <@(\\d{17,19})> deleted in <#\\d{17,19}>.\\*\\*");
    private static readonly Regex _DozerRegex = new("Message ID: (\\d{17,19}) - (\\d{17,19})\nUserID: (\\d{17,19})");
    private static readonly Regex _SkyraRegex = new("https://discord.com/channels/(\\d{17,19})/(\\d{17,19})/(\\d{17,19})");
    private static readonly Regex _AnnabelleRegex = new("```\n(\\d{17,19})\n```");
    private static readonly Regex _AnnabelleRegexFuzzy = new("\\<t:(\\d+)\\> A message from \\*\\*[\\w.]{2,32}\\*\\* \\(`(\\d{17,19})`\\) was deleted in <#\\d{17,19}>");
    private static readonly Regex _koiraRegex = new("ID:\\*\\* (\\d{17,19})");

    private static readonly Regex _VortexRegex =
        new("`\\[(\\d\\d:\\d\\d:\\d\\d)\\]` .* \\(ID:(\\d{17,19})\\).* <#\\d{17,19}>:");

    private static readonly Dictionary<ulong, LoggerBot> _bots = new[]
    {
        new LoggerBot("Carl-bot", 235148962103951360, ExtractCarlBot), // webhooks
        new LoggerBot("Circle", 497196352866877441, fuzzyExtractFunc: ExtractCircle),
        new LoggerBot("Pancake", 239631525350604801, fuzzyExtractFunc: ExtractPancake),
        new LoggerBot("Logger", 298822483060981760, ExtractLogger), // webhook
        new LoggerBot("Patron Logger", 579149474975449098, ExtractLogger), // webhook (?)
        new LoggerBot("Dyno#3861", 155149108183695360, ExtractDyno, applicationId: 161660517914509312), // webhook
        new LoggerBot("Dyno#7532", 168274214858653696, ExtractDyno), // webhook
        new LoggerBot("Dyno#1390", 470722245824610306, ExtractDyno), // webhook
        new LoggerBot("Dyno#8506", 470722416427925514, ExtractDyno), // webhook
        new LoggerBot("Dyno#0811", 470722753218084866, ExtractDyno), // webhook
        new LoggerBot("Dyno#9026", 470723667303727125, ExtractDyno), // webhook
        new LoggerBot("Dyno#8389", 470724017205149701, ExtractDyno), // webhook
        new LoggerBot("Dyno#5714", 470723870270160917, ExtractDyno), // webhook
        new LoggerBot("Dyno#1961", 347378323418251264, ExtractDyno), // webhook
        new LoggerBot("Maki", 563434444321587202, ExtractMaki), // webhook
        new LoggerBot("Sapphire", 678344927997853742, ExtractSapphire), // webhook
        new LoggerBot("Auttaja", 242730576195354624, ExtractAuttaja), // webhook
        new LoggerBot("GenericBot", 295329346590343168, ExtractGenericBot),
        new LoggerBot("blargbot", 134133271750639616, ExtractBlargBot),
        new LoggerBot("Mantaro", 213466096718708737, ExtractMantaro),
        new LoggerBot("UnbelievaBoat", 292953664492929025, ExtractUnbelievaBoat), // webhook
        new LoggerBot("UnbelievaBoat Premium", 356950275044671499, ExtractUnbelievaBoat), // webhook (?)
        new LoggerBot("Vanessa", 310261055060443136, fuzzyExtractFunc: ExtractVanessa),
        new LoggerBot("SafetyAtLast", 401549924199694338, fuzzyExtractFunc: ExtractSAL),
        new LoggerBot("GearBot", 349977940198555660, fuzzyExtractFunc: ExtractGearBot),
        new LoggerBot("GiselleBot", 356831787445387285, ExtractGiselleBot),
        new LoggerBot("Vortex", 240254129333731328, fuzzyExtractFunc: ExtractVortex),
        new LoggerBot("ProBot", 282859044593598464, fuzzyExtractFunc: ExtractProBot), // webhook
        new LoggerBot("ProBot Prime", 567703512763334685, fuzzyExtractFunc: ExtractProBot), // webhook (?)
        new LoggerBot("Dozer", 356535250932858885, ExtractDozer),
        new LoggerBot("Skyra", 266624760782258186, ExtractSkyra),
        new LoggerBot("Annabelle", 231241068383961088, ExtractAnnabelle, fuzzyExtractFunc: ExtractAnnabelleFuzzy),
        new LoggerBot("Koira", 1247013404569239624, ExtractKoira)
    }.ToDictionary(b => b.Id);

    private static Dictionary<ulong, LoggerBot> _botsByApplicationId
        => _bots.Values.ToDictionary(b => b.ApplicationId);

    private readonly IDiscordCache _cache;
    private readonly DiscordApiClient _client;

    private readonly RedisService _redis;
    private readonly ILogger _logger;

    public LoggerCleanService(RedisService redis, DiscordApiClient client, IDiscordCache cache, ILogger logger)
    {
        _redis = redis;
        _client = client;
        _cache = cache;
        _logger = logger.ForContext<LoggerCleanService>();
    }

    public static ICollection<LoggerBot> Bots => _bots.Values;

    public async ValueTask HandleLoggerBotCleanup(Message msg)
    {
        var channel = await _cache.GetChannel(msg.GuildId!.Value, msg.ChannelId!);

        if (channel.Type != Channel.ChannelType.GuildText) return;
        if (!(await _cache.BotPermissionsIn(msg.GuildId!.Value, channel.Id)).HasFlag(PermissionSet.ManageMessages)) return;

        // If this message is from a *webhook*, check if the application ID matches one of the bots we know
        // If it's from a *bot*, check the bot ID to see if we know it.
        LoggerBot bot = null;
        if (msg.WebhookId != null && msg.ApplicationId != null) _botsByApplicationId.TryGetValue(msg.ApplicationId.Value, out bot);
        else if (msg.Author.Bot) _bots.TryGetValue(msg.Author.Id, out bot);

        // If we didn't find anything before, or what we found is an unsupported bot, bail
        if (bot == null) return;

        try
        {
            // We try two ways of extracting the actual message, depending on the bots
            // Some bots have different log formats so we check for both types of extract function
            if (bot.FuzzyExtractFunc != null)
            {
                // Some bots (Carl, Circle, etc) only give us a user ID, so we try our best to
                // "cross-reference" those with the message DB. We know the deletion event happens *after* the message
                // was sent, so we're checking for any messages sent in the same guild within 3 seconds before the
                // delete event log, which is... good enough, I think? Potential for false positives and negatives
                // either way but shouldn't be too much, given it's constrained by user ID and guild.
                var fuzzy = bot.FuzzyExtractFunc(msg);
                if (fuzzy != null)
                {

                    _logger.Debug("Fuzzy logclean for {BotName} on {MessageId}: {@FuzzyExtractResult}",
                        bot.Name, msg.Id, fuzzy);

                    var exists = await _redis.HasLogCleanup(fuzzy.Value.User, msg.GuildId.Value);
                    _logger.Debug(exists.ToString());

                    // If we didn't find a corresponding message, bail
                    if (!exists) return;

                    // Otherwise, we can *reasonably assume* that this is a logged deletion, so delete the log message.
                    await _client.DeleteMessage(msg.ChannelId, msg.Id);

                }
            }
            if (bot.ExtractFunc != null)
            {
                // Other bots give us the message ID itself, and we can just extract that from the database directly.
                var extractedId = bot.ExtractFunc(msg);
                if (extractedId == null) return; // If we didn't find anything, bail.

                _logger.Debug("Pure logclean for {BotName} on {MessageId}: {@FuzzyExtractResult}",
                    bot.Name, msg.Id, extractedId);

                var mid = await _redis.GetOriginalMid(extractedId.Value);
                if (mid != null)
                {
                    // If we've gotten this far, we found a logged deletion of a trigger message. Just yeet it!
                    await _client.DeleteMessage(msg.ChannelId, msg.Id);
                }
            } // else should not happen, but idk, it might
        }
        catch (NotFoundException)
        {
            // Sort of a temporary measure: getting an error in Sentry about a NotFoundException from D#+ here
            // The only thing I can think of that'd cause this are the DeleteAsync() calls which 404 when
            // the message doesn't exist anyway - so should be safe to just ignore it, right?
        }
    }

    private static ulong? ExtractAuttaja(Message msg)
    {
        // Auttaja has an optional "compact mode" that logs without embeds
        // That one puts the ID in the message content, non-compact puts it in the embed description.
        // Regex also checks that this is a deletion.
        var stringWithId = msg.Embeds?.FirstOrDefault()?.Description ?? msg.Content;
        if (stringWithId == null) return null;

        var match = _auttajaRegex.Match(stringWithId);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractDyno(Message msg)
    {
        // Embed *description* contains "Message sent by [mention] deleted in [channel]", contains message ID in footer per regex
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Footer == null || !(embed.Description?.Contains("Deleted in") ?? false)) return null;
        var match = _dynoRegex.Match(embed.Footer.Text ?? "");
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractLogger(Message msg)
    {
        // Embed contains title "Message deleted in [channel]", and an ID field containing both message and user ID (see regex).
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed == null) return null;
        if (!embed.Description.StartsWith("Message deleted in")) return null;

        var idField = embed.Fields.FirstOrDefault(f => f.Name == "ID");
        if (idField.Value == null) return null; // "OrDefault" = all-null object
        var match = _loggerARegex.Match(idField.Value);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractGenericBot(Message msg)
    {
        // Embed, title is "Message Deleted", ID plain in footer.
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Footer == null || !(embed.Title?.Contains("Message Deleted") ?? false)) return null;
        var match = _basicRegex.Match(embed.Footer.Text ?? "");
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractBlargBot(Message msg)
    {
        // Embed, title ends with "Message Deleted", contains ID plain in a field.
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed == null || !(embed.Title?.EndsWith("Message Deleted") ?? false)) return null;
        var field = embed.Fields.FirstOrDefault(f => f.Name == "Message Id");
        var match = _basicRegex.Match(field.Value ?? "");
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractMantaro(Message msg)
    {
        // Plain message, "Message (ID: [id]) created by [user] (ID: [id]) in channel [channel] was deleted.
        if (!(msg.Content?.Contains("was deleted.") ?? false)) return null;
        var match = _mantaroRegex.Match(msg.Content);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractCarlBot(Message msg)
    {
        // Embed, title is "Message deleted in [channel]", description is message content followed by "Message ID: [id]"
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Footer == null || embed.Timestamp == null ||
            !(embed.Title?.StartsWith("Message deleted in") ?? false)) return null;
        var match = _carlRegex.Match(embed.Description);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractMaki(Message msg)
    {
        // Embed, Message Author Name field: "Message Deleted", footer is "Message ID: [id]"
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Author?.Name == null || embed?.Footer == null || (!embed?.Author?.Name.StartsWith("Message Deleted") ?? false)) return null;
        var match = _makiRegex.Match(embed.Footer.Text ?? "");
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static ulong? ExtractSapphire(Message msg)
    {
        // Embed, Message title field: "Message deleted", description contains "**Message ID:** [[id]]"
        // Example: "**Message ID:** [1297549791927996598]"
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed == null) return null;
        if (!(embed.Title?.StartsWith("Message deleted") ?? false)) return null;
        var match = _sapphireRegex.Match(embed.Description);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static FuzzyExtractResult? ExtractCircle(Message msg)
    {
        // Like Auttaja, Circle has both embed and compact modes, but the regex works for both.
        // Compact: "Message from [user] ([id]) deleted in [channel]"
        // Embed: Message Author field: "[user] ([id])"
        var stringWithId = msg.Content;
        if (msg.Embeds?.Length > 0)
        {
            var embed = msg.Embeds?.First();
            if (embed.Author?.Name == null || !embed.Author.Name.StartsWith("Message Deleted in")) return null;
            var field = embed.Fields.FirstOrDefault(f => f.Name == "Message Author");
            if (field.Value == null) return null;
            stringWithId = field.Value;
        }

        if (stringWithId == null) return null;

        var match = _circleRegex.Match(stringWithId);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[1].Value)
            }
            : null;
    }

    private static FuzzyExtractResult? ExtractPancake(Message msg)
    {
        // Embed, author is "Message Deleted", description includes a mention
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Description == null || embed.Author?.Name != "Message Deleted") return null;
        var match = _pancakeRegex.Match(embed.Description);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[1].Value)
            }
            : null;
    }

    private static ulong? ExtractUnbelievaBoat(Message msg)
    {
        // Embed author is "Message Deleted", footer contains message ID per regex
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Footer == null || embed.Author?.Name != "Message Deleted") return null;
        var match = _unbelievaboatRegex.Match(embed.Footer.Text ?? "");
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static FuzzyExtractResult? ExtractVanessa(Message msg)
    {
        // Title is "Message Deleted", embed description contains mention
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Title == null || embed.Title != "Message Deleted" || embed.Description == null) return null;
        var match = _vanessaRegex.Match(embed.Description);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[1].Value)
            }
            : null;
    }

    private static FuzzyExtractResult? ExtractSAL(Message msg)
    {
        // Title is "Message Deleted!", field "Message Author" contains ID
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Title == null || embed.Title != "Message Deleted!") return null;
        var authorField = embed.Fields.FirstOrDefault(f => f.Name == "Message Author");
        if (authorField == null) return null;
        var match = _salRegex.Match(authorField.Value);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[1].Value)
            }
            : null;
    }

    private static FuzzyExtractResult? ExtractGearBot(Message msg)
    {
        // Simple text based message log.
        // No message ID, but we have author ID.
        var match = _GearBotRegex.Match(msg.Content);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[1].Value)
            }
            : null;
    }

    private static ulong? ExtractGiselleBot(Message msg)
    {
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Title == null || embed.Title != "🗑 Message Deleted") return null;
        var match = _GiselleRegex.Match(embed?.Description);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static FuzzyExtractResult? ExtractVortex(Message msg)
    {
        var match = _VortexRegex.Match(msg.Content);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[2].Value)
            }
            : null;
    }

    private static FuzzyExtractResult? ExtractProBot(Message msg)
    {
        // user ID and channel ID are in the embed description (we don't use channel ID)
        if (msg.Embeds.Length == 0 || msg.Embeds[0].Description == null) return null;
        var match = _ProBotRegex.Match(msg.Embeds[0].Description);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[1].Value)
            }
            : null;
    }

    private static ulong? ExtractDozer(Message msg)
    {
        var embed = msg.Embeds?.FirstOrDefault();
        var match = _DozerRegex.Match(embed?.Footer.Text);
        return match.Success ? ulong.Parse(match.Groups[2].Value) : null;
    }

    private static ulong? ExtractSkyra(Message msg)
    {
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Footer?.Text == null || !embed.Footer.Text.StartsWith("Message Deleted")) return null;
        var match = _SkyraRegex.Match(embed.Author.Url);
        return match.Success ? ulong.Parse(match.Groups[3].Value) : null;
    }

    private static ulong? ExtractAnnabelle(Message msg)
    {
        // this bot has both an embed and a non-embed log format
        // the embed is precise matching (this), the non-embed is fuzzy (below)
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed?.Author?.Name == null || !embed.Author.Name.EndsWith("Deleted Message")) return null;
        var match = _AnnabelleRegex.Match(embed.Fields[2].Value);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    private static FuzzyExtractResult? ExtractAnnabelleFuzzy(Message msg)
    {
        // matching for annabelle's non-precise non-embed format
        // it has a discord (unix) timestamp for the message so we use that
        if (msg.Embeds.Length != 0) return null;
        var match = _AnnabelleRegexFuzzy.Match(msg.Content);
        return match.Success
            ? new FuzzyExtractResult
            {
                User = ulong.Parse(match.Groups[2].Value)
            }
            : null;
    }

    private static ulong? ExtractKoira(Message msg)
    {
        // Embed, Message author name field: "Message Deleted", description contains "**[emoji] ID:** [id]"
        // Example: "ID:** 347077478726238228"
        // We only use the end of the bold markdown because there's a custom emoji in the bold we don't want to copy the code of
        var embed = msg.Embeds?.FirstOrDefault();
        if (embed == null) return null;
        if (!(embed.Author?.Name?.StartsWith("Message Deleted") ?? false)) return null;
        var match = _koiraRegex.Match(embed.Description);
        return match.Success ? ulong.Parse(match.Groups[1].Value) : null;
    }

    public class LoggerBot
    {
        public ulong Id;
        public ulong ApplicationId;
        public string Name;

        public Func<Message, ulong?> ExtractFunc;
        public Func<Message, FuzzyExtractResult?> FuzzyExtractFunc;

        public LoggerBot(string name, ulong id, Func<Message, ulong?> extractFunc = null,
                         Func<Message, FuzzyExtractResult?> fuzzyExtractFunc = null,
                         ulong? applicationId = null)
        {
            Name = name;
            Id = id;
            FuzzyExtractFunc = fuzzyExtractFunc;
            ExtractFunc = extractFunc;
            ApplicationId = applicationId ?? id;
        }
    }

    public struct FuzzyExtractResult
    {
        public ulong User { get; set; }
    }
}
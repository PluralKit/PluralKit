﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using Dapper;

using NodaTime;
using NodaTime.Extensions;
using NodaTime.Text;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class LoggerCleanService
    {
        private static readonly Regex _basicRegex = new("(\\d{17,19})");
        private static readonly Regex _dynoRegex = new("Message ID: (\\d{17,19})");
        private static readonly Regex _carlRegex = new("ID: (\\d{17,19})");
        private static readonly Regex _circleRegex = new("\\(`(\\d{17,19})`\\)");
        private static readonly Regex _loggerARegex = new("Message = (\\d{17,19})");
        private static readonly Regex _loggerBRegex = new("MessageID:(\\d{17,19})");
        private static readonly Regex _auttajaRegex = new("Message (\\d{17,19}) deleted");
        private static readonly Regex _mantaroRegex = new("Message \\(?ID:? (\\d{17,19})\\)? created by .* in channel .* was deleted\\.");
        private static readonly Regex _pancakeRegex = new("Message from <@(\\d{17,19})> deleted in");
        private static readonly Regex _unbelievaboatRegex = new("Message ID: (\\d{17,19})");
        private static readonly Regex _vanessaRegex = new("Message sent by <@!?(\\d{17,19})> deleted in");
        private static readonly Regex _salRegex = new("\\(ID: (\\d{17,19})\\)");
        private static readonly Regex _GearBotRegex = new("\\(``(\\d{17,19})``\\) in <#\\d{17,19}> has been removed.");
        private static readonly Regex _GiselleRegex = new("\\*\\*Message ID\\*\\*: `(\\d{17,19})`");

        private static readonly Dictionary<ulong, LoggerBot> _bots = new[]
        {
            new LoggerBot("Carl-bot", 23514896210395136, fuzzyExtractFunc: ExtractCarlBot, webhookName: "Carl-bot Logging"),
            new LoggerBot("Circle", 497196352866877441, fuzzyExtractFunc: ExtractCircle),
            new LoggerBot("Pancake", 239631525350604801, fuzzyExtractFunc: ExtractPancake),
            
            // There are two "Logger"s. They seem to be entirely unrelated. Don't ask.
            new LoggerBot("Logger#6088", 298822483060981760 , ExtractLoggerA, webhookName: "Logger"),
            new LoggerBot("Logger#6278", 327424261180620801, ExtractLoggerB), 
            
            new LoggerBot("Dyno", 155149108183695360, ExtractDyno,  webhookName: "Dyno"),
            new LoggerBot("Auttaja", 242730576195354624, ExtractAuttaja, webhookName: "Auttaja"),
            new LoggerBot("GenericBot", 295329346590343168, ExtractGenericBot), 
            new LoggerBot("blargbot", 134133271750639616, ExtractBlargBot), 
            new LoggerBot("Mantaro", 213466096718708737, ExtractMantaro), 
            new LoggerBot("UnbelievaBoat", 292953664492929025, ExtractUnbelievaBoat, webhookName: "UnbelievaBoat"), 
            new LoggerBot("Vanessa", 310261055060443136, fuzzyExtractFunc: ExtractVanessa), 
            new LoggerBot("SafetyAtLast", 401549924199694338, fuzzyExtractFunc: ExtractSAL),
            new LoggerBot("GearBot", 349977940198555660, fuzzyExtractFunc: ExtractGearBot),
            new LoggerBot("GiselleBot", 356831787445387285, ExtractGiselleBot)
        }.ToDictionary(b => b.Id);

        private static readonly Dictionary<string, LoggerBot> _botsByWebhookName = _bots.Values
            .Where(b => b.WebhookName != null)
            .ToDictionary(b => b.WebhookName);

        private readonly IDatabase _db;
        private readonly DiscordApiClient _client;
        private readonly IDiscordCache _cache;
        private readonly Bot _bot; // todo: get rid of this nasty
        private readonly ILogger _logger;
        
        public LoggerCleanService(IDatabase db, DiscordApiClient client, IDiscordCache cache, Bot bot, ILogger logger)
        {
            _db = db;
            _client = client;
            _cache = cache;
            _bot = bot;
            _logger = logger.ForContext<LoggerCleanService>();
        }

        public ICollection<LoggerBot> Bots => _bots.Values;

        public async ValueTask HandleLoggerBotCleanup(Message msg)
        {
            var channel = _cache.GetChannel(msg.ChannelId);
            
            if (channel.Type != Channel.ChannelType.GuildText) return;
            if (!_bot.PermissionsIn(channel.Id).HasFlag(PermissionSet.ManageMessages)) return;
 
            // If this message is from a *webhook*, check if the name matches one of the bots we know
            // TODO: do we need to do a deeper webhook origin check, or would that be too hard on the rate limit?
            // If it's from a *bot*, check the bot ID to see if we know it.
            LoggerBot bot = null;
            if (msg.WebhookId != null) _botsByWebhookName.TryGetValue(msg.Author.Username, out bot);
            else if (msg.Author.Bot) _bots.TryGetValue(msg.Author.Id, out bot);
            
            // If we didn't find anything before, or what we found is an unsupported bot, bail
            if (bot == null) return;

            try
            {
                // We try two ways of extracting the actual message, depending on the bots
                if (bot.FuzzyExtractFunc != null)
                {
                    // Some bots (Carl, Circle, etc) only give us a user ID and a rough timestamp, so we try our best to
                    // "cross-reference" those with the message DB. We know the deletion event happens *after* the message
                    // was sent, so we're checking for any messages sent in the same guild within 3 seconds before the
                    // delete event timestamp, which is... good enough, I think? Potential for false positives and negatives
                    // either way but shouldn't be too much, given it's constrained by user ID and guild.
                    var fuzzy = bot.FuzzyExtractFunc(msg);
                    if (fuzzy == null) return;
                    
                    _logger.Debug("Fuzzy logclean for {BotName} on {MessageId}: {@FuzzyExtractResult}",
                        bot.Name, msg.Id, fuzzy);
                    
                    var mid = await _db.Execute(conn =>
                        conn.QuerySingleOrDefaultAsync<ulong?>(
                            "select mid from messages where sender = @User and mid > @ApproxID and guild = @Guild limit 1",
                            new
                            {
                                fuzzy.Value.User,
                                Guild = msg.GuildId,
                                ApproxId = DiscordUtils.InstantToSnowflake(
                                    fuzzy.Value.ApproxTimestamp - Duration.FromSeconds(3))
                            }));
                    
                    // If we didn't find a corresponding message, bail
                    if (mid == null) 
                        return; 
                    
                    // Otherwise, we can *reasonably assume* that this is a logged deletion, so delete the log message.
                    await _client.DeleteMessage(msg.ChannelId, msg.Id);
                }
                else if (bot.ExtractFunc != null)
                {
                    // Other bots give us the message ID itself, and we can just extract that from the database directly.
                    var extractedId = bot.ExtractFunc(msg);
                    if (extractedId == null) return; // If we didn't find anything, bail.
                    
                    _logger.Debug("Pure logclean for {BotName} on {MessageId}: {@FuzzyExtractResult}",
                        bot.Name, msg.Id, extractedId);

                    var mid = await _db.Execute(conn => conn.QuerySingleOrDefaultAsync<ulong?>(
                        "select mid from messages where original_mid = @Mid", new {Mid = extractedId.Value}));
                    if (mid == null) return;

                    // If we've gotten this far, we found a logged deletion of a trigger message. Just yeet it!
                    await _client.DeleteMessage(msg.ChannelId, msg.Id);
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
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        private static ulong? ExtractDyno(Message msg)
        {
            // Embed *description* contains "Message sent by [mention] deleted in [channel]", contains message ID in footer per regex
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed?.Footer == null || !(embed.Description?.Contains("deleted in") ?? false)) return null;
            var match = _dynoRegex.Match(embed.Footer.Text ?? "");
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        private static ulong? ExtractLoggerA(Message msg)
        {
            // This is for Logger#6088 (298822483060981760), distinct from Logger#6278 (327424261180620801).
            // Embed contains title "Message deleted in [channel]", and an ID field containing both message and user ID (see regex).
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed == null) return null;
            if (!embed.Description.StartsWith("Message deleted in")) return null;
            
            var idField = embed.Fields.FirstOrDefault(f => f.Name == "ID");
            if (idField.Value == null) return null; // "OrDefault" = all-null object
            var match = _loggerARegex.Match(idField.Value);
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        private static ulong? ExtractLoggerB(Message msg)
        {
            // This is for Logger#6278 (327424261180620801), distinct from Logger#6088 (298822483060981760).
            // Embed title ends with "A Message Was Deleted!", footer contains message ID as per regex.
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed?.Footer == null || !(embed.Title?.EndsWith("A Message Was Deleted!") ?? false)) return null;
            var match = _loggerBRegex.Match(embed.Footer.Text ?? "");
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        private static ulong? ExtractGenericBot(Message msg)
        {
            // Embed, title is "Message Deleted", ID plain in footer.
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed?.Footer == null || !(embed.Title?.Contains("Message Deleted") ?? false)) return null;
            var match = _basicRegex.Match(embed.Footer.Text ?? "");
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        private static ulong? ExtractBlargBot(Message msg)
        {
            // Embed, title ends with "Message Deleted", contains ID plain in a field.
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed == null || !(embed.Title?.EndsWith("Message Deleted") ?? false)) return null;
            var field = embed.Fields.FirstOrDefault(f => f.Name == "Message ID");
            var match = _basicRegex.Match(field.Value ?? "");
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        private static ulong? ExtractMantaro(Message msg)
        {
            // Plain message, "Message (ID: [id]) created by [user] (ID: [id]) in channel [channel] was deleted.
            if (!(msg.Content?.Contains("was deleted.") ?? false)) return null;
            var match = _mantaroRegex.Match(msg.Content);
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        private static FuzzyExtractResult? ExtractCarlBot(Message msg)
        {
            // Embed, title is "Message deleted in [channel], **user** ID in the footer, timestamp as, well, timestamp in embed.
            // This is the *deletion* timestamp, which we can assume is a couple seconds at most after the message was originally sent
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed?.Footer == null || embed.Timestamp == null || !(embed.Title?.StartsWith("Message deleted in") ?? false)) return null;
            var match = _carlRegex.Match(embed.Footer.Text ?? "");
            return match.Success 
                ? new FuzzyExtractResult
                {
                    User = ulong.Parse(match.Groups[1].Value), 
                    ApproxTimestamp = OffsetDateTimePattern.Rfc3339.Parse(embed.Timestamp).GetValueOrThrow().ToInstant()
                }
                : (FuzzyExtractResult?) null;
        }

        private static FuzzyExtractResult? ExtractCircle(Message msg)
        {
            // Like Auttaja, Circle has both embed and compact modes, but the regex works for both.
            // Compact: "Message from [user] ([id]) deleted in [channel]", no timestamp (use message time)
            // Embed: Message Author field: "[user] ([id])", then an embed timestamp
            string stringWithId = msg.Content;
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
                ? new FuzzyExtractResult {
                    User = ulong.Parse(match.Groups[1].Value),
                    ApproxTimestamp = msg.Timestamp().ToInstant()
                }
                : (FuzzyExtractResult?) null;
        }

        private static FuzzyExtractResult? ExtractPancake(Message msg)
        {
            // Embed, author is "Message Deleted", description includes a mention, timestamp is *message send time* (but no ID)
            // so we use the message timestamp to get somewhere *after* the message was proxied
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed?.Description == null || embed.Author?.Name != "Message Deleted") return null;
            var match = _pancakeRegex.Match(embed.Description);
            return match.Success
                ? new FuzzyExtractResult
                {
                    User = ulong.Parse(match.Groups[1].Value),
                    ApproxTimestamp = msg.Timestamp().ToInstant()
                }
                : (FuzzyExtractResult?) null;
        }
        
        private static ulong? ExtractUnbelievaBoat(Message msg)
        {
            // Embed author is "Message Deleted", footer contains message ID per regex
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed?.Footer == null || embed.Author?.Name != "Message Deleted") return null;
            var match = _unbelievaboatRegex.Match(embed.Footer.Text ?? "");
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
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
                    User = ulong.Parse(match.Groups[1].Value),
                    ApproxTimestamp = msg.Timestamp().ToInstant()
                }
                : (FuzzyExtractResult?) null;
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
                    User = ulong.Parse(match.Groups[1].Value),
                    ApproxTimestamp = msg.Timestamp().ToInstant()
                }
                : (FuzzyExtractResult?) null;
        }

        private static FuzzyExtractResult? ExtractGearBot(Message msg)
        {
            // Simple text based message log.
            // No message ID, but we have timestamp and author ID.
            // Not using timestamp here though (seems to be same as message timestamp), might be worth implementing in the future.
            var match = _GearBotRegex.Match(msg.Content);
            return match.Success
                ? new FuzzyExtractResult
                {
                    User = ulong.Parse(match.Groups[1].Value),
                    ApproxTimestamp = msg.Timestamp().ToInstant()
                }
                : (FuzzyExtractResult?) null;
        }

        private static ulong? ExtractGiselleBot(Message msg)
        {
            var embed = msg.Embeds?.FirstOrDefault();
            if (embed?.Title == null || embed.Title != "🗑 Message Deleted") return null;
            var match = _GiselleRegex.Match(embed?.Description);
            return match.Success ? ulong.Parse(match.Groups[1].Value) : (ulong?) null;
        }

        public class LoggerBot
        {
            public string Name;
            public ulong Id;
            public Func<Message, ulong?> ExtractFunc;
            public Func<Message, FuzzyExtractResult?> FuzzyExtractFunc;
            public string WebhookName;

            public LoggerBot(string name, ulong id, Func<Message, ulong?> extractFunc = null, Func<Message, FuzzyExtractResult?> fuzzyExtractFunc = null, string webhookName = null)
            {
                Name = name;
                Id = id;
                FuzzyExtractFunc = fuzzyExtractFunc;
                ExtractFunc = extractFunc;
                WebhookName = webhookName;
            }
        }

        public struct FuzzyExtractResult
        {
            public ulong User { get; set; }
            public Instant ApproxTimestamp { get; set; }
        }
    }
}
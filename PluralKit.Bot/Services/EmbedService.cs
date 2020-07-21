using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using Humanizer;
using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot {
    public class EmbedService
    {
        private IDataStore _data;
        private IDatabase _db;
        private DiscordShardedClient _client;

        public EmbedService(DiscordShardedClient client, IDataStore data, IDatabase db)
        {
            _client = client;
            _data = data;
            _db = db;
        }


        
        public async Task<DiscordEmbed> CreateSystemEmbed(DiscordClient client, PKSystem system, LookupContext ctx, CardOptions opts)
        {
            await using var conn = await _db.Obtain();

            if(opts.PrivacyFilter == PrivacyLevel.Public)
            {
                ctx = LookupContext.ByNonOwner;
            }
            
            // Fetch/render info for all accounts simultaneously
            var accounts = await conn.GetLinkedAccounts(system.Id);
            var users = await Task.WhenAll(accounts.Select(async uid => (await client.GetUser(uid))?.NameAndMention() ?? $"(deleted account {uid})"));

            var memberCount = await conn.GetSystemMemberCount(system.Id, PrivacyLevel.Public);
            var eb = new DiscordEmbedBuilder()
                .WithColor(DiscordUtils.Gray)
                .WithTitle(system.Name ?? null)
                .WithThumbnail(system.AvatarUrl)
                .WithFooter($"System ID: {system.Hid} | Created on {system.Created.FormatZoned(system)}{opts.createFooter()}");
 
            var latestSwitch = await _data.GetLatestSwitch(system.Id);
            if (latestSwitch != null && system.FrontPrivacy.CanAccess(ctx))
            {
                var switchMembers = await _data.GetSwitchMembers(latestSwitch).ToListAsync();
                if (switchMembers.Count > 0)
                    eb.AddField("Fronter".ToQuantity(switchMembers.Count(), ShowQuantityAs.None),
                        string.Join(", ", switchMembers.Select(m => m.NameFor(ctx))));
            }

            if (system.Tag != null) eb.AddField("Tag", system.Tag.EscapeMarkdown());
            eb.AddField("Linked accounts", string.Join(", ", users).Truncate(1000), true);

            if (system.MemberListPrivacy.CanAccess(ctx))
            {
                if (memberCount > 0)
                    eb.AddField($"Members ({memberCount})", $"(see `pk;system {system.Hid} list` or `pk;system {system.Hid} list full`)", true);
                else
                    eb.AddField($"Members ({memberCount})", "Add one with `pk;member new`!", true);
            }

            if (system.DescriptionFor(ctx) is { } desc)
                eb.AddField("Description", desc.NormalizeLineEndSpacing().Truncate(1024), false);

            return eb.Build();
        }

        public DiscordEmbed CreateLoggedMessageEmbed(PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, DiscordUser sender, string content, DiscordChannel channel) {
            // TODO: pronouns in ?-reacted response using this card
            var timestamp = DiscordUtils.SnowflakeToInstant(messageId);
            var name = member.NameFor(LookupContext.ByNonOwner); 
            return new DiscordEmbedBuilder()
                .WithAuthor($"#{channel.Name}: {name}", iconUrl: DiscordUtils.WorkaroundForUrlBug(member.AvatarFor(LookupContext.ByNonOwner)))
                .WithThumbnail(member.AvatarFor(LookupContext.ByNonOwner))
                .WithDescription(content?.NormalizeLineEndSpacing())
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Sender: {sender.Username}#{sender.Discriminator} ({sender.Id}) | Message ID: {messageId} | Original Message ID: {originalMsgId}")
                .WithTimestamp(timestamp.ToDateTimeOffset())
                .Build();
        }

        public async Task<DiscordEmbed> CreateMemberEmbed(PKSystem system, PKMember member, DiscordGuild guild, LookupContext ctx, CardOptions opts)
        {

            // string FormatTimestamp(Instant timestamp) => DateTimeFormats.ZonedDateTimeFormat.Format(timestamp.InZone(system.Zone));

            if(opts.PrivacyFilter == PrivacyLevel.Public)
            {
                ctx = LookupContext.ByNonOwner;
            }

            var name = member.NameFor(ctx);
            if (system.Name != null) name = $"{name} ({system.Name})";

            DiscordColor color;
            try
            {
                color = member.Color?.ToDiscordColor() ?? DiscordUtils.Gray;
            }
            catch (ArgumentException)
            {
                // Bad API use can cause an invalid color string
                // TODO: fix that in the API
                // for now we just default to a blank color, yolo
                color = DiscordUtils.Gray;
            }
            
            var guildSettings = guild != null ? await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(guild.Id, member.Id)) : null;
            var guildDisplayName = guildSettings?.DisplayName;
            var avatar = guildSettings?.AvatarUrl ?? member.AvatarFor(ctx);

            var proxyTagsStr = string.Join('\n', member.ProxyTags.Select(t => $"`` {t.ProxyString}﻿``"));

            var eb = new DiscordEmbedBuilder()
                // TODO: add URL of website when that's up
                .WithAuthor(name, iconUrl: DiscordUtils.WorkaroundForUrlBug(avatar))
                // .WithColor(member.ColorPrivacy.CanAccess(ctx) ? color : DiscordUtils.Gray)
                .WithColor(color)
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} {(member.MetadataPrivacy.CanAccess(ctx) ? $"| Created on {member.Created.FormatZoned(system)}":"")}{opts.createFooter()}");

            var description = "";
            if (member.MemberVisibility == PrivacyLevel.Private) description += "*(this member is hidden)*\n";
            if (guildSettings?.AvatarUrl != null)
                if (member.AvatarFor(ctx) != null) 
                    description += $"*(this member has a server-specific avatar set; [click here]({member.AvatarUrl}) to see the global avatar)*\n";
                else
                    description += "*(this member has a server-specific avatar set)*\n";
            if (description != "") eb.WithDescription(description);

            if (avatar != null) eb.WithThumbnail(avatar);

            if (!member.DisplayName.EmptyOrNull() && member.NamePrivacy.CanAccess(ctx)) eb.AddField("Display Name", member.DisplayName.Truncate(1024), true);
            if (guild != null && guildDisplayName != null) eb.AddField($"Server Nickname (for {guild.Name})", guildDisplayName.Truncate(1024), true);
            if (member.BirthdayFor(ctx) != null) eb.AddField("Birthdate", member.BirthdayString, true);
            if (member.PronounsFor(ctx) is {} pronouns && !string.IsNullOrWhiteSpace(pronouns)) eb.AddField("Pronouns", pronouns.Truncate(1024), true);
            if (member.MessageCountFor(ctx) is {} count && count > 0) eb.AddField("Message Count", member.MessageCount.ToString(), true);
            if (member.HasProxyTags) eb.AddField("Proxy Tags", string.Join('\n', proxyTagsStr).Truncate(1024), true);
            // --- For when this gets added to the member object itself or however they get added
            // if (member.LastMessage != null && member.MetadataPrivacy.CanAccess(ctx)) eb.AddField("Last message:" FormatTimestamp(DiscordUtils.SnowflakeToInstant(m.LastMessage.Value)));
            // if (member.LastSwitchTime != null && m.MetadataPrivacy.CanAccess(ctx)) eb.AddField("Last switched in:", FormatTimestamp(member.LastSwitchTime.Value));
            // if (!member.Color.EmptyOrNull() && member.ColorPrivacy.CanAccess(ctx)) eb.AddField("Color", $"#{member.Color}", true);
            if (!member.Color.EmptyOrNull()) eb.AddField("Color", $"#{member.Color}", true);
            
            if (member.DescriptionFor(ctx) is {} desc) eb.AddField("Description", member.Description.NormalizeLineEndSpacing(), false);

            return eb.Build();
        }

        public async Task<DiscordEmbed> CreateFronterEmbed(PKSwitch sw, DateTimeZone zone, LookupContext ctx)
        {
            var members = await _data.GetSwitchMembers(sw).ToListAsync();
            var timeSinceSwitch = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;
            return new DiscordEmbedBuilder()
                .WithColor(members.FirstOrDefault()?.Color?.ToDiscordColor() ?? DiscordUtils.Gray)
                .AddField($"Current {"fronter".ToQuantity(members.Count, ShowQuantityAs.None)}", members.Count > 0 ? string.Join(", ", members.Select(m => m.NameFor(ctx))) : "*(no fronter)*")
                .AddField("Since", $"{sw.Timestamp.FormatZoned(zone)} ({timeSinceSwitch.FormatDuration()} ago)")
                .Build();
        }

        public async Task<DiscordEmbed> CreateMessageInfoEmbed(DiscordClient client, FullMessage msg)
        {
            var ctx = LookupContext.ByNonOwner;
            
            var channel = await client.GetChannel(msg.Message.Channel);
            var serverMsg = channel != null ? await channel.GetMessage(msg.Message.Mid) : null;

            // Need this whole dance to handle cases where:
            // - the user is deleted (userInfo == null)
            // - the bot's no longer in the server we're querying (channel == null)
            // - the member is no longer in the server we're querying (memberInfo == null)
            DiscordMember memberInfo = null;
            DiscordUser userInfo = null;
            if (channel != null) memberInfo = await channel.Guild.GetMember(msg.Message.Sender);
            if (memberInfo != null) userInfo = memberInfo; // Don't do an extra request if we already have this info from the member lookup
            else userInfo = await client.GetUser(msg.Message.Sender);

            // Calculate string displayed under "Sent by"
            string userStr;
            if (memberInfo != null && memberInfo.Nickname != null)
                userStr = $"**Username:** {memberInfo.NameAndMention()}\n**Nickname:** {memberInfo.Nickname}";
            else if (userInfo != null) userStr = userInfo.NameAndMention();
            else userStr = $"*(deleted user {msg.Message.Sender})*";

            // Put it all together
            var eb = new DiscordEmbedBuilder()
                .WithAuthor(msg.Member.NameFor(ctx), iconUrl: DiscordUtils.WorkaroundForUrlBug(msg.Member.AvatarFor(ctx)))
                .WithDescription(serverMsg?.Content?.NormalizeLineEndSpacing() ?? "*(message contents deleted or inaccessible)*")
                .WithImageUrl(serverMsg?.Attachments?.FirstOrDefault()?.Url)
                .AddField("System",
                    msg.System.Name != null ? $"{msg.System.Name} (`{msg.System.Hid}`)" : $"`{msg.System.Hid}`", true)
                .AddField("Member", $"{msg.Member.NameFor(ctx)} (`{msg.Member.Hid}`)", true)
                .AddField("Sent by", userStr, inline: true)
                .WithTimestamp(DiscordUtils.SnowflakeToInstant(msg.Message.Mid).ToDateTimeOffset());

            var roles = memberInfo?.Roles?.ToList();
            if (roles != null && roles.Count > 0)
                eb.AddField($"Account roles ({roles.Count})", string.Join(", ", roles.Select(role => role.Name)));
            
            return eb.Build();
        }

        public Task<DiscordEmbed> CreateFrontPercentEmbed(FrontBreakdown breakdown, DateTimeZone tz, LookupContext ctx)
        {
            var actualPeriod = breakdown.RangeEnd - breakdown.RangeStart;
            var eb = new DiscordEmbedBuilder()
                .WithColor(DiscordUtils.Gray)
                .WithFooter($"Since {breakdown.RangeStart.FormatZoned(tz)} ({actualPeriod.FormatDuration()} ago)");

            var maxEntriesToDisplay = 24; // max 25 fields allowed in embed - reserve 1 for "others"

            // We convert to a list of pairs so we can add the no-fronter value
            // Dictionary doesn't allow for null keys so we instead have a pair with a null key ;)
            var pairs = breakdown.MemberSwitchDurations.ToList();
            if (breakdown.NoFronterDuration != Duration.Zero)
                pairs.Add(new KeyValuePair<PKMember, Duration>(null, breakdown.NoFronterDuration));

            var membersOrdered = pairs.OrderByDescending(pair => pair.Value).Take(maxEntriesToDisplay).ToList();
            foreach (var pair in membersOrdered)
            {
                var frac = pair.Value / actualPeriod;
                eb.AddField(pair.Key?.NameFor(ctx) ?? "*(no fronter)*", $"{frac*100:F0}% ({pair.Value.FormatDuration()})");
            }

            if (membersOrdered.Count > maxEntriesToDisplay)
            {
                eb.AddField("(others)",
                    membersOrdered.Skip(maxEntriesToDisplay)
                        .Aggregate(Duration.Zero, (prod, next) => prod + next.Value)
                        .FormatDuration(), true);
            }

            return Task.FromResult(eb.Build());
        }
    }
}
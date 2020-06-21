using System.Collections.Generic;

using DSharpPlus.Entities;

using Humanizer;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class LongRenderer: IListRenderer
    {
        public int MembersPerPage => 5;

        private readonly MemberFields _fields;
        public LongRenderer(MemberFields fields)
        {
            _fields = fields;
        }

        public void RenderPage(DiscordEmbedBuilder eb, DateTimeZone zone, IEnumerable<ListedMember> members, LookupContext ctx)
        {
            string FormatTimestamp(Instant timestamp) => DateTimeFormats.ZonedDateTimeFormat.Format(timestamp.InZone(zone));

            foreach (var m in members)
            {
                var profile = $"**ID**: {m.Hid}";
                if (_fields.ShowDisplayName && m.DisplayName != null && m.NamePrivacy.CanAccess(ctx)) profile += $"\n**Display name**: {m.DisplayName}";
                if (_fields.ShowPronouns && m.PronounsFor(ctx) is {} pronouns) profile += $"\n**Pronouns**: {pronouns}";
                if (_fields.ShowBirthday && m.BirthdayFor(ctx) != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                if (_fields.ShowProxyTags && m.ProxyTags.Count > 0) profile += $"\n**Proxy tags:** {m.ProxyTagsString()}";
                if (_fields.ShowMessageCount && m.MessageCountFor(ctx) is {} count && count > 0) profile += $"\n**Message count:** {count}";
                if (_fields.ShowLastMessage && m.MetadataPrivacy.TryGet(ctx, m.LastMessage, out var lastMsg)) profile += $"\n**Last message:** {FormatTimestamp(DiscordUtils.SnowflakeToInstant(lastMsg.Value))}";
                if (_fields.ShowLastSwitch && m.MetadataPrivacy.TryGet(ctx, m.LastSwitchTime, out var lastSw)) profile += $"\n**Last switched in:** {FormatTimestamp(lastSw.Value)}";
                if (_fields.ShowDescription && m.DescriptionFor(ctx) is {} desc) profile += $"\n\n{desc}";
                if (_fields.ShowPrivacy && m.MemberVisibility == PrivacyLevel.Private) profile += "\n*(this member is hidden)*";

                eb.AddField(m.NameFor(ctx), profile.Truncate(1024));
            }
        }
        
        public class MemberFields
        {
            public bool ShowDisplayName = true;
            public bool ShowCreated = false;
            public bool ShowPronouns = true;
            public bool ShowBirthday = true;
            public bool ShowProxyTags = true;
            public bool ShowDescription = true;
            public bool ShowPrivacy = true;
            
            public bool ShowMessageCount = false;
            public bool ShowLastSwitch = false;
            public bool ShowLastMessage = false;

            public static MemberFields FromFlags(Context ctx)
            {
                var def = new MemberFields();
                if (ctx.MatchFlag("with-last-switch", "with-last-fronted", "with-last-front", "wls", "wlf"))
                    def.ShowLastSwitch = true;
                if (ctx.MatchFlag("with-message-count", "wmc"))
                    def.ShowMessageCount = true;
                if (ctx.MatchFlag("with-last-message", "with-last-proxy", "wlm", "wlp"))
                    def.ShowLastMessage = true;
                return def;
            }
        }
    }
}
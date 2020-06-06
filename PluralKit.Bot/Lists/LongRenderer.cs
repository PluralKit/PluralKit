using System;
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

        public void RenderPage(DiscordEmbedBuilder eb, PKSystem system, IEnumerable<PKListMember> members)
        {
            foreach (var m in members)
            {
                var profile = $"**ID**: {m.Hid}";
                if (_fields.ShowDisplayName && m.DisplayName != null) profile += $"\n**Display name**: {m.DisplayName}";
                if (_fields.ShowPronouns && m.Pronouns != null) profile += $"\n**Pronouns**: {m.Pronouns}";
                if (_fields.ShowBirthday && m.Birthday != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                if (_fields.ShowPronouns && m.ProxyTags.Count > 0) profile += $"\n**Proxy tags:** {m.ProxyTagsString()}";
                if (_fields.ShowMessageCount && m.MessageCount > 0) profile += $"\n**Message count:** {m.MessageCount}";
                if (_fields.ShowLastMessage && m.LastMessage != null) profile += $"\n**Last message:** {FormatTimestamp(system, DiscordUtils.SnowflakeToInstant(m.LastMessage.Value))}";
                if (_fields.ShowLastSwitch && m.LastSwitchTime != null) profile += $"\n**Last switched in:** {FormatTimestamp(system, m.LastSwitchTime.Value)}";
                if (_fields.ShowDescription && m.Description != null) profile += $"\n\n{m.Description}";
                if (_fields.ShowPrivacy && m.MemberPrivacy == PrivacyLevel.Private)
                    profile += "\n*(this member is private)*";

                eb.AddField(m.Name, profile.Truncate(1024));
            }
        }

        private static string FormatTimestamp(PKSystem system, Instant timestamp) => DateTimeFormats.ZonedDateTimeFormat.Format(timestamp.InZone(system.Zone ?? DateTimeZone.Utc));

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
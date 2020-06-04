using System;
using System.Collections.Generic;

using DSharpPlus.Entities;

using Humanizer;

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

        public void RenderPage(DiscordEmbedBuilder eb, IEnumerable<PKListMember> members)
        {
            foreach (var m in members)
            {
                var profile = $"**ID**: {m.Hid}";
                if (_fields.ShowDisplayName && m.DisplayName != null) profile += $"\n**Display name**: {m.DisplayName}";
                if (_fields.ShowPronouns && m.Pronouns != null) profile += $"\n**Pronouns**: {m.Pronouns}";
                if (_fields.ShowBirthday && m.Birthday != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                if (_fields.ShowPronouns && m.ProxyTags.Count > 0) profile += $"\n**Proxy tags:** {m.ProxyTagsString()}";
                if (_fields.ShowMessageCount && m.MessageCount > 0) profile += $"\n**Message count:** {m.MessageCount}";
                if (_fields.ShowDescription && m.Description != null) profile += $"\n\n{m.Description}";
                if (_fields.ShowPrivacy && m.MemberPrivacy == PrivacyLevel.Private)
                    profile += "\n*(this member is private)*";

                eb.AddField(m.Name, profile.Truncate(1024));
            }
        }
        
        public class MemberFields
        {
            public bool ShowDisplayName = true;
            public bool ShowCreated = true;
            public bool ShowMessageCount = true;
            public bool ShowPronouns = true;
            public bool ShowBirthday = true;
            public bool ShowProxyTags = true;
            public bool ShowDescription = true;
            public bool ShowPrivacy = true;
            
            public static MemberFields FromFlags(Context ctx)
            {
                // TODO
                return new MemberFields
                {
                    ShowMessageCount = false,
                    ShowCreated = false
                };
            }
        }
    }
}
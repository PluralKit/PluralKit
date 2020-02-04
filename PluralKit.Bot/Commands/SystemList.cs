using System;
using System.Linq;
using System.Threading.Tasks;

using Humanizer;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class SystemList
    {
        private IDataStore _data;

        public SystemList(IDataStore data)
        {
            _data = data;
        }

        public async Task MemberShortList(Context ctx, PKSystem system) {
            if (system == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(system, system.MemberListPrivacy);
            
            var authCtx = ctx.LookupContextFor(system);
            var shouldShowPrivate = authCtx == LookupContext.ByOwner && ctx.Match("all", "everyone", "private");

            var embedTitle = system.Name != null ? $"Members of {system.Name.SanitizeMentions()} (`{system.Hid}`)" : $"Members of `{system.Hid}`";

            var memberCountPublic = _data.GetSystemMemberCount(system, false);
            var memberCountAll = _data.GetSystemMemberCount(system, true);
            await Task.WhenAll(memberCountPublic, memberCountAll);

            var memberCountDisplayed = shouldShowPrivate ? memberCountAll.Result : memberCountPublic.Result;

            var members = _data.GetSystemMembers(system)
                .Where(m => m.MemberPrivacy == PrivacyLevel.Public || shouldShowPrivate)
                .OrderBy(m => m.Name, StringComparer.InvariantCultureIgnoreCase);
            var anyMembersHidden = !shouldShowPrivate && memberCountPublic.Result != memberCountAll.Result;
                
            await ctx.Paginate(
                members,
                memberCountDisplayed,
                25,
                embedTitle,
                (eb, ms) =>
                {
                    eb.Description = string.Join("\n", ms.Select((m) =>
                    {
                        if (m.HasProxyTags)
                        {
                            var proxyTagsString = m.ProxyTagsString().SanitizeMentions();
                            if (proxyTagsString.Length > 100) // arbitrary threshold for now, tweak?
                                proxyTagsString = "tags too long, see member card";
                            
                            return $"[`{m.Hid}`] **{m.Name.SanitizeMentions()}** *({proxyTagsString})*";
                        }

                        return $"[`{m.Hid}`] **{m.Name.SanitizeMentions()}**";
                    }));

                    var footer = $"{memberCountDisplayed} total.";
                    if (anyMembersHidden && authCtx == LookupContext.ByOwner)
                        footer += " Private members have been hidden. type \"pk;system list all\" to include them.";
                    eb.WithFooter(footer);
                    
                    return Task.CompletedTask;
                });
        }

        public async Task MemberLongList(Context ctx, PKSystem system) {
            if (system == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(system, system.MemberListPrivacy);
            
            var authCtx = ctx.LookupContextFor(system);
            var shouldShowPrivate = authCtx == LookupContext.ByOwner && ctx.Match("all", "everyone", "private");
            
            var embedTitle = system.Name != null ? $"Members of {system.Name} (`{system.Hid}`)" : $"Members of `{system.Hid}`";

            var memberCountPublic = _data.GetSystemMemberCount(system, false);
            var memberCountAll = _data.GetSystemMemberCount(system, true);
            await Task.WhenAll(memberCountPublic, memberCountAll);

            var memberCountDisplayed = shouldShowPrivate ? memberCountAll.Result : memberCountPublic.Result;

            var members = _data.GetSystemMembers(system)
                .Where(m => m.MemberPrivacy == PrivacyLevel.Public || shouldShowPrivate)
                .OrderBy(m => m.Name, StringComparer.InvariantCultureIgnoreCase);
            var anyMembersHidden = !shouldShowPrivate && memberCountPublic.Result != memberCountAll.Result;
            
            await ctx.Paginate(
                members,
                memberCountDisplayed,
                5,
                embedTitle,
                (eb, ms) => {
                    foreach (var m in ms) {
                        var profile = $"**ID**: {m.Hid}";
                        if (m.Pronouns != null) profile += $"\n**Pronouns**: {m.Pronouns}";
                        if (m.Birthday != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                        if (m.ProxyTags.Count > 0) profile += $"\n**Proxy tags:** {m.ProxyTagsString()}";
                        if (m.Description != null) profile += $"\n\n{m.Description}";
                        if (m.MemberPrivacy == PrivacyLevel.Private)
                            profile += "*(this member is private)*";
                        
                        eb.AddField(m.Name, profile.Truncate(1024));
                    }

                    var footer = $"{memberCountDisplayed} total.";
                    if (anyMembersHidden && authCtx == LookupContext.ByOwner)
                        footer += " Private members have been hidden. type \"pk;system list full all\" to include them.";
                    eb.WithFooter(footer);
                    return Task.CompletedTask;
                }
            );
        }
    }
}
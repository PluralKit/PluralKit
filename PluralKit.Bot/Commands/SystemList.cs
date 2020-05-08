using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus.Entities;

using Humanizer;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SystemList
    {
        private IDataStore _data;

        public SystemList(IDataStore data)
        {
            _data = data;
        }

        private async Task RenderMemberList(Context ctx, PKSystem system, bool canShowPrivate, int membersPerPage, string embedTitle, Func<PKMember, bool> filter,
                                            Func<DiscordEmbedBuilder, IEnumerable<PKMember>, Task>
                                                renderer)
        {
            var authCtx = ctx.LookupContextFor(system);
            var shouldShowPrivate = authCtx == LookupContext.ByOwner && canShowPrivate;

            var membersToShow = await _data.GetSystemMembers(system)
                .Where(filter)
                .OrderBy(m => m.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToListAsync();

            var membersToShowWithPrivacy = membersToShow
                .Where(m => m.MemberPrivacy == PrivacyLevel.Public || shouldShowPrivate)
                .ToList();
            
            var anyMembersHidden = !shouldShowPrivate && membersToShowWithPrivacy.Count != membersToShow.Count;

            await ctx.Paginate(
                membersToShowWithPrivacy.ToAsyncEnumerable(),
                membersToShowWithPrivacy.Count,
                membersPerPage,
                embedTitle,
                (eb, ms) =>
                {
                    var footer = $"{membersToShowWithPrivacy.Count} total.";
                    if (anyMembersHidden && authCtx == LookupContext.ByOwner)
                        footer += " Private members have been hidden. Add \"all\" to the command to include them.";
                    eb.WithFooter(footer);
                    
                    return renderer(eb, ms);
                });
        }

        private Task ShortRenderer(DiscordEmbedBuilder eb, IEnumerable<PKMember> members)
        {
            eb.Description = string.Join("\n", members.Select((m) =>
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
            
            return Task.CompletedTask;
        }

        private Task LongRenderer(DiscordEmbedBuilder eb, IEnumerable<PKMember> members)
        {
            foreach (var m in members)
            {
                var profile = $"**ID**: {m.Hid}";
                if (m.DisplayName != null) profile += $"\n**Display name**: {m.DisplayName}";
                if (m.Pronouns != null) profile += $"\n**Pronouns**: {m.Pronouns}";
                if (m.Birthday != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                if (m.ProxyTags.Count > 0) profile += $"\n**Proxy tags:** {m.ProxyTagsString()}";
                if (m.Description != null) profile += $"\n\n{m.Description}";
                if (m.MemberPrivacy == PrivacyLevel.Private)
                    profile += "\n*(this member is private)*";

                eb.AddField(m.Name, profile.Truncate(1024));
            }

            return Task.CompletedTask;
        }

        public async Task MemberList(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(system, system.MemberListPrivacy);

            var embedTitle = system.Name != null
                ? $"Members of {system.Name.SanitizeMentions()} (`{system.Hid}`)"
                : $"Members of `{system.Hid}`";

            var shouldShowLongList = ctx.Match("f", "full", "big", "details", "long");
            var canShowPrivate = ctx.Match("a", "all", "everyone", "private");
            if (shouldShowLongList)
                await RenderMemberList(ctx, system,  canShowPrivate, 5, embedTitle, _ => true, LongRenderer);
            else 
                await RenderMemberList(ctx, system,  canShowPrivate, 25, embedTitle, _ => true, ShortRenderer);
        }

        public async Task MemberFind(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(system, system.MemberListPrivacy);

            var shouldShowLongList = ctx.Match("full", "big", "details", "long") || ctx.MatchFlag("f", "full");
            var canShowPrivate = ctx.Match("all", "everyone", "private") || ctx.MatchFlag("a", "all");

            var searchTerm = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must specify a search term.");
            
            var embedTitle = system.Name != null
                ? $"Members of {system.Name.SanitizeMentions()} (`{system.Hid}`) matching **{searchTerm.SanitizeMentions()}**"
                : $"Members of `{system.Hid}` matching **{searchTerm.SanitizeMentions()}**";

            bool Filter(PKMember member) =>
                member.Name.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                (member.DisplayName?.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ?? false);
            
            if (shouldShowLongList)
                await RenderMemberList(ctx, system,  canShowPrivate, 5, embedTitle, Filter, LongRenderer);
            else 
                await RenderMemberList(ctx, system,  canShowPrivate, 25, embedTitle, Filter, ShortRenderer);
        }
    }
}
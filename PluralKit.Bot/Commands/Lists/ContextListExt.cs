using System.Text;

using Humanizer;

using Myriad.Builders;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextListExt
{
    public static ListOptions ParseListOptions(this Context ctx, LookupContext lookupCtx)
    {
        var p = new ListOptions();

        // Short or long list? (parse this first, as it can potentially take a positional argument)
        var isFull = ctx.Match("f", "full", "big", "details", "long") || ctx.MatchFlag("f", "full");
        p.Type = isFull ? ListType.Long : ListType.Short;

        // Search query
        if (ctx.HasNext())
            p.Search = ctx.RemainderOrNull();

        // Include description in search?
        if (ctx.MatchFlag(
            "search-description",
            "filter-description",
            "in-description",
            "sd",
            "description",
            "desc"
        ))
            p.SearchDescription = true;

        // Sort property (default is by name, but adding a flag anyway, 'cause why not)
        if (ctx.MatchFlag("by-name", "bn")) p.SortProperty = SortProperty.Name;
        if (ctx.MatchFlag("by-display-name", "bdn")) p.SortProperty = SortProperty.DisplayName;
        if (ctx.MatchFlag("by-id", "bid")) p.SortProperty = SortProperty.Hid;
        if (ctx.MatchFlag("by-message-count", "bmc")) p.SortProperty = SortProperty.MessageCount;
        if (ctx.MatchFlag("by-created", "bc", "bcd")) p.SortProperty = SortProperty.CreationDate;
        if (ctx.MatchFlag("by-last-fronted", "by-last-front", "by-last-switch", "blf", "bls"))
            p.SortProperty = SortProperty.LastSwitch;
        if (ctx.MatchFlag("by-last-message", "blm", "blp")) p.SortProperty = SortProperty.LastMessage;
        if (ctx.MatchFlag("by-birthday", "by-birthdate", "bbd")) p.SortProperty = SortProperty.Birthdate;
        if (ctx.MatchFlag("random")) p.SortProperty = SortProperty.Random;

        // Sort reverse?
        if (ctx.MatchFlag("r", "rev", "reverse"))
            p.Reverse = true;

        // Privacy filter (default is public only)
        if (ctx.MatchFlag("a", "all")) p.PrivacyFilter = null;
        if (ctx.MatchFlag("private-only", "po")) p.PrivacyFilter = PrivacyLevel.Private;

        // PERM CHECK: If we're trying to access non-public members of another system, error
        if (p.PrivacyFilter != PrivacyLevel.Public && lookupCtx != LookupContext.ByOwner)
            // TODO: should this just return null instead of throwing or something? >.>
            throw Errors.NotOwnInfo;

        // Additional fields to include in the search results
        if (ctx.MatchFlag("with-last-switch", "with-last-fronted", "with-last-front", "wls", "wlf"))
            p.IncludeLastSwitch = true;
        if (ctx.MatchFlag("with-last-message", "with-last-proxy", "wlm", "wlp"))
            p.IncludeLastMessage = true;
        if (ctx.MatchFlag("with-message-count", "wmc"))
            p.IncludeMessageCount = true;
        if (ctx.MatchFlag("with-created", "wc"))
            p.IncludeCreated = true;
        if (ctx.MatchFlag("with-avatar", "with-image", "with-icon", "wa", "wi", "ia", "ii", "img"))
            p.IncludeAvatar = true;
        if (ctx.MatchFlag("with-pronouns", "wp", "wprns"))
            p.IncludePronouns = true;
        if (ctx.MatchFlag("with-displayname", "wdn"))
            p.IncludeDisplayName = true;
        if (ctx.MatchFlag("with-birthday", "wbd", "wb"))
            p.IncludeBirthday = true;

        // Always show the sort property, too (unless this is the short list and we are already showing something else)
        if (p.Type != ListType.Short || p.includedCount == 0)
        {
            if (p.SortProperty == SortProperty.DisplayName) p.IncludeDisplayName = true;
            if (p.SortProperty == SortProperty.MessageCount) p.IncludeMessageCount = true;
            if (p.SortProperty == SortProperty.CreationDate) p.IncludeCreated = true;
            if (p.SortProperty == SortProperty.LastSwitch) p.IncludeLastSwitch = true;
            if (p.SortProperty == SortProperty.LastMessage) p.IncludeLastMessage = true;
            if (p.SortProperty == SortProperty.Birthdate) p.IncludeBirthday = true;
        }

        // Make sure the options are valid
        p.AssertIsValid();

        // Done!
        return p;
    }

    public static async Task RenderMemberList(this Context ctx, LookupContext lookupCtx,
                                SystemId system, string embedTitle, string color, ListOptions opts)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var members = (await ctx.Database.Execute(conn => conn.QueryMemberList(system, opts.ToQueryOptions())))
            .SortByMemberListOptions(opts, lookupCtx)
            .ToList();

        var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;
        await ctx.Paginate(members.ToAsyncEnumerable(), members.Count, itemsPerPage, embedTitle, color, Renderer);

        // Base renderer, dispatches based on type
        Task Renderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
        {
            // Add a global footer with the filter/sort string + result count
            eb.Footer(new Embed.EmbedFooter($"{opts.CreateFilterString()}. {"result".ToQuantity(members.Count)}."));

            // Then call the specific renderers
            if (opts.Type == ListType.Short)
                ShortRenderer(eb, page);
            else
                LongRenderer(eb, page);

            return Task.CompletedTask;
        }

        void ShortRenderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
        {
            // We may end up over the description character limit
            // so run it through a helper that "makes it work" :)
            eb.WithSimpleLineContent(page.Select(m =>
            {
                var ret = $"[`{m.Hid}`] **{m.NameFor(ctx)}** ";

                if (opts.IncludeMessageCount && m.MessageCountFor(lookupCtx) is { } count)
                    ret += $"({count} messages)";
                else if (opts.IncludeLastSwitch && m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                    ret += $"(last switched in: <t:{lastSw.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludeLastMessage && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                    ret += $"(last message: <t:{m.LastMessageTimestamp.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludeCreated && m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                    ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                else if (opts.IncludeAvatar && m.AvatarFor(lookupCtx) is { } avatarUrl)
                    ret += $"([avatar URL]({avatarUrl}))";
                else if (opts.IncludePronouns && m.PronounsFor(lookupCtx) is { } pronouns)
                    ret += $"({pronouns})";
                else if (opts.IncludeDisplayName && m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                    ret += $"({m.DisplayName})";
                else if (opts.IncludeBirthday && m.BirthdayFor(lookupCtx) is { } birthday)
                    ret += $"(birthday: {m.BirthdayString})";
                else if (m.HasProxyTags && m.ProxyPrivacy.CanAccess(lookupCtx))
                {
                    var proxyTagsString = m.ProxyTagsString();
                    if (proxyTagsString.Length > 100) // arbitrary threshold for now, tweak?
                        proxyTagsString = "tags too long, see member card";
                    ret += $"*(*{proxyTagsString}*)*";
                }

                return ret;
            }));
        }

        void LongRenderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
        {
            foreach (var m in page)
            {
                var profile = new StringBuilder($"**ID**: {m.Hid}");

                if (m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Display name**: {m.DisplayName}");

                if (m.PronounsFor(lookupCtx) is { } pronouns)
                    profile.Append($"\n**Pronouns**: {pronouns}");

                if (m.BirthdayFor(lookupCtx) != null)
                    profile.Append($"\n**Birthdate**: {m.BirthdayString}");

                if (m.ProxyTags.Count > 0 && m.ProxyPrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Proxy tags**: {m.ProxyTagsString()}");

                if ((opts.IncludeMessageCount || opts.SortProperty == SortProperty.MessageCount) &&
                    m.MessageCountFor(lookupCtx) is { } count && count > 0)
                    profile.Append($"\n**Message count:** {count}");

                if ((opts.IncludeLastMessage || opts.SortProperty == SortProperty.LastMessage) && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                    profile.Append($"\n**Last message:** {m.LastMessageTimestamp.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludeLastSwitch || opts.SortProperty == SortProperty.LastSwitch) &&
                    m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                    profile.Append($"\n**Last switched in:** {lastSw.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludeCreated || opts.SortProperty == SortProperty.CreationDate) &&
                    m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludeAvatar && m.AvatarFor(lookupCtx) is { } avatar)
                    profile.Append($"\n**Avatar URL:** {avatar.TryGetCleanCdnUrl()}");

                if (m.DescriptionFor(lookupCtx) is { } desc)
                    profile.Append($"\n\n{desc}");

                if (m.MemberVisibility == PrivacyLevel.Private)
                    profile.Append("\n*(this member is hidden)*");

                eb.Field(new Embed.Field(m.NameFor(ctx), profile.ToString().Truncate(1024)));
            }
        }
    }

    public static async Task RenderGroupList(this Context ctx, LookupContext lookupCtx,
                                SystemId system, string embedTitle, string color, ListOptions opts)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var groups = (await ctx.Database.Execute(conn => conn.QueryGroupList(system, opts.ToQueryOptions())))
            .SortByGroupListOptions(opts, lookupCtx)
            .ToList();

        var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;
        await ctx.Paginate(groups.ToAsyncEnumerable(), groups.Count, itemsPerPage, embedTitle, color, Renderer);

        // Base renderer, dispatches based on type
        Task Renderer(EmbedBuilder eb, IEnumerable<ListedGroup> page)
        {
            // Add a global footer with the filter/sort string + result count
            eb.Footer(new Embed.EmbedFooter($"{opts.CreateFilterString()}. {"result".ToQuantity(groups.Count)}."));

            // Then call the specific renderers
            if (opts.Type == ListType.Short)
                ShortRenderer(eb, page);
            else
                LongRenderer(eb, page);

            return Task.CompletedTask;
        }

        void ShortRenderer(EmbedBuilder eb, IEnumerable<ListedGroup> page)
        {
            // We may end up over the description character limit
            // so run it through a helper that "makes it work" :)
            eb.WithSimpleLineContent(page.Select(g =>
            {
                var ret = $"[`{g.Hid}`] **{g.NameFor(ctx)}** ";

                switch (opts.SortProperty)
                {
                    case SortProperty.DisplayName:
                        {
                            if (g.NamePrivacy.CanAccess(lookupCtx) && g.DisplayName != null)
                                ret += $"({g.DisplayName})";
                            break;
                        }
                    case SortProperty.CreationDate:
                        {
                            if (g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            break;
                        }
                    default:
                        {
                            if (opts.IncludeCreated &&
                                     g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                            {
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            }
                            else if (opts.IncludeDisplayName && g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                            {
                                ret += $"({g.DisplayName})";
                            }
                            else if (opts.IncludeAvatar && g.IconFor(lookupCtx) is { } avatarUrl)
                            {
                                ret += $"([avatar URL]({avatarUrl}))";
                            }
                            else
                            {
                                // -priv/-pub and listprivacy affects whether count is shown
                                // -all and visibility affects what the count is
                                if (ctx.DirectLookupContextFor(system) == LookupContext.ByOwner)
                                {
                                    if (g.ListPrivacy == PrivacyLevel.Public || lookupCtx == LookupContext.ByOwner)
                                    {
                                        if (ctx.MatchFlag("all", "a"))
                                        {
                                            ret += $"({"member".ToQuantity(g.TotalMemberCount)})";
                                        }
                                        else
                                        {
                                            ret += $"({"member".ToQuantity(g.PublicMemberCount)})";
                                        }
                                    }
                                }
                                else
                                {
                                    if (g.ListPrivacy == PrivacyLevel.Public)
                                    {
                                        ret += $"({"member".ToQuantity(g.PublicMemberCount)})";
                                    }
                                }
                            }

                            break;
                        }
                }

                return ret;
            }));
        }

        void LongRenderer(EmbedBuilder eb, IEnumerable<ListedGroup> page)
        {
            foreach (var g in page)
            {
                var profile = new StringBuilder($"**ID**: {g.Hid}");

                if (g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Display name**: {g.DisplayName}");

                if (g.ListPrivacy == PrivacyLevel.Public || lookupCtx == LookupContext.ByOwner)
                {
                    if (ctx.MatchFlag("all", "a") && ctx.DirectLookupContextFor(system) == LookupContext.ByOwner)
                        profile.Append($"\n**Member Count:** {g.TotalMemberCount}");
                    else
                        profile.Append($"\n**Member Count:** {g.PublicMemberCount}");
                }

                if ((opts.IncludeCreated || opts.SortProperty == SortProperty.CreationDate) &&
                    g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludeAvatar && g.IconFor(lookupCtx) is { } avatar)
                    profile.Append($"\n**Avatar URL:** {avatar.TryGetCleanCdnUrl()}");

                if (g.DescriptionFor(lookupCtx) is { } desc)
                    profile.Append($"\n\n{desc}");

                if (g.Visibility == PrivacyLevel.Private)
                    profile.Append("\n*(this group is hidden)*");

                eb.Field(new Embed.Field(g.NameFor(ctx), profile.ToString().Truncate(1024)));
            }
        }
    }
}
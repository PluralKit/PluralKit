using System.Text;
using Humanizer;

using Myriad.Builders;
using Myriad.Types;
using static Myriad.Types.MessageComponent;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextListExt
{
    public static ListOptions ParseListOptions(this Context ctx, LookupContext directLookupCtx, LookupContext lookupContext, ListEntity entityType)
    {
        var p = new ListOptions(entityType);

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
        if (ctx.MatchFlag("by-name", "bn")) p.SortProperty = ListEntityField.Name;
        if (ctx.MatchFlag("by-display-name", "bdn")) p.SortProperty = ListEntityField.DisplayName;
        if (ctx.MatchFlag("by-id", "bid")) p.SortProperty = ListEntityField.Hid;
        if (ctx.MatchFlag("by-message-count", "bmc")) p.SortProperty = ListEntityField.MessageCount;
        if (ctx.MatchFlag("by-created", "bc", "bcd")) p.SortProperty = ListEntityField.CreationDate;
        if (ctx.MatchFlag("by-last-fronted", "by-last-front", "by-last-switch", "blf", "bls"))
            p.SortProperty = ListEntityField.LastSwitch;
        if (ctx.MatchFlag("by-last-message", "blm", "blp")) p.SortProperty = ListEntityField.LastMessage;
        if (ctx.MatchFlag("by-birthday", "by-birthdate", "bbd")) p.SortProperty = ListEntityField.Birthdate;
        if (ctx.MatchFlag("random", "rand")) p.SortProperty = SortOption.Random;

        // Sort reverse?
        if (ctx.MatchFlag("r", "rev", "reverse"))
            p.Reverse = true;

        // Privacy filter (default is public only)
        if (ctx.MatchFlag("a", "all")) p.PrivacyFilter = null;
        if (ctx.MatchFlag("private-only", "po")) p.PrivacyFilter = PrivacyLevel.Private;

        // PERM CHECK: If we're trying to access non-public members of another system, error
        if (p.PrivacyFilter != PrivacyLevel.Public && directLookupCtx != LookupContext.ByOwner)
            // TODO: should this just return null instead of throwing or something? >.>
            throw Errors.NotOwnInfo;

        //this is for searching
        p.Context = lookupContext;

        // Additional fields to include in the search results
        if (ctx.MatchFlag("with-last-switch", "with-last-fronted", "with-last-front", "wls", "wlf"))
            p.IncludedFields[ListEntityField.LastSwitch] = true;
        if (ctx.MatchFlag("with-last-message", "with-last-proxy", "wlm", "wlp"))
            p.IncludedFields[ListEntityField.LastMessage] = true;
        if (ctx.MatchFlag("with-message-count", "wmc"))
            p.IncludedFields[ListEntityField.MessageCount] = true;
        if (ctx.MatchFlag("with-created", "wc"))
            p.IncludedFields[ListEntityField.CreationDate] = true;
        if (ctx.MatchFlag("with-avatar", "with-image", "with-icon", "wa", "wi", "ia", "ii", "img"))
            p.IncludedFields[ListEntityField.Avatar] = true;
        if (ctx.MatchFlag("with-pronouns", "wp", "wprns"))
            p.IncludedFields[ListEntityField.Pronouns] = true;
        if (ctx.MatchFlag("with-displayname", "wdn"))
            p.IncludedFields[ListEntityField.DisplayName] = true;
        if (ctx.MatchFlag("with-birthday", "wbd", "wb"))
            p.IncludedFields[ListEntityField.Birthdate] = true;

        // Always show the sort property, too (unless this is the short list and we are already showing something else)
        if (p.Type != ListType.Short || p.IncludedCount == 0)
        {
            if (p.SortProperty == ListEntityField.DisplayName) p.IncludedFields[ListEntityField.DisplayName] = true;
            if (p.SortProperty == ListEntityField.MessageCount) p.IncludedFields[ListEntityField.MessageCount] = true;
            if (p.SortProperty == ListEntityField.CreationDate) p.IncludedFields[ListEntityField.CreationDate] = true;
            if (p.SortProperty == ListEntityField.LastSwitch) p.IncludedFields[ListEntityField.LastSwitch] = true;
            if (p.SortProperty == ListEntityField.LastMessage) p.IncludedFields[ListEntityField.LastMessage] = true;
            if (p.SortProperty == ListEntityField.Birthdate) p.IncludedFields[ListEntityField.Birthdate] = true;
            // If this is a short list and we went from 0 -> 1 included count here then we made the sort and includes match
            if (p.Type == ListType.Short && p.IncludedCount == 1) p.SortIncludeMatch = true;
        }

        // Make sure the options are valid
        p.AssertIsValid();

        // Done!
        return p;
    }

    public static void ParseListOptionUpdates(ListOptions opts, ApplicationCommandInteractionData optsOverride)
    {
        opts.ResetDynamicOptions();

        var sortComponent = optsOverride.Components.First((x) => x.Component.CustomId == "sort_select").Component;
        if (Enum.TryParse<ListEntityField>(sortComponent.Values[0], out var sortResult))
            opts.SortProperty = sortResult;
        else if (sortComponent.Values[0] == SortOption.Random.ToString())
            opts.SortProperty = SortOption.Random;

        var infoComponent = optsOverride.Components.First((x) => x.Component.CustomId == "info_select").Component;
        if (infoComponent?.Values?.Length > 0 && infoComponent.Values[0] == "default")
        {
            infoComponent.Values[0] = sortComponent.Values[0];
            opts.SortIncludeMatch = true;
        }
        foreach (var include in infoComponent.Values)
            if (Enum.TryParse<ListEntityField>(include, out var infoResult))
                opts.IncludedFields[infoResult] = true;

        var extraCheckboxesComponent = optsOverride.Components.FirstOrDefault((x) => x.Component.CustomId == "extra_checkboxes")?.Component;
        opts.ShowIds = extraCheckboxesComponent?.Values.FirstOrDefault(x => x == "id") != null;
        opts.NumberItems = extraCheckboxesComponent?.Values.FirstOrDefault(x => x == "number") != null;
        opts.Reverse = extraCheckboxesComponent?.Values.FirstOrDefault(x => x == "reverse") != null;

        var withFilterComponent = optsOverride.Components.FirstOrDefault(x => x.Component.CustomId == "filter_with")?.Component;
        foreach (var withFilterEntryStr in withFilterComponent?.Values ?? [])
            if (Enum.TryParse<ListEntityField>(withFilterEntryStr, out var withFilterEntry) && opts.WithFilter.ContainsKey(withFilterEntry))
                opts.WithFilter[withFilterEntry] = true;

        var withOutFilterComponent = optsOverride.Components.FirstOrDefault(x => x.Component.CustomId == "filter_without")?.Component;
        foreach (var withOutFilterEntryStr in withOutFilterComponent?.Values ?? [])
            if (Enum.TryParse<ListEntityField>(withOutFilterEntryStr, out var withOutFilterEntry) && opts.WithOutFilter.ContainsKey(withOutFilterEntry))
                opts.WithOutFilter[withOutFilterEntry] = true;

        opts.AssertIsValid();
    }

    public static MessageComponent[] shortOptionsModal(ListOptions opts, string entity) => [
            new()
            {
                Type = ComponentType.Label,
                Label = "Sort By",
                Component = new()
                {
                    Type = ComponentType.StringSelect,
                    CustomId = "sort_select",
                    MaxValues = 1,
                    Options = [
                        new()
                        {
                            Label = "Name (Default)",
                            Value = ListEntityField.Name.ToString(),
                            Default = opts.SortProperty == ListEntityField.Name
                        },
                        .. opts.SortOptions.Where(x => x != ListEntityField.Name).Select(x =>
                            new MessageComponentOption()
                            {
                                Label = x.ToLabel(),
                                Value = x.ToString(),
                                Default = opts.SortProperty == x
                            }),
                    ]
                }
            },
        new()
        {
            Type = ComponentType.Label,
            Label = "Extra Info",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "info_select",
                MaxValues = 1,
                Options = [
                        new()
                        {
                            Label = "Same as Sort",
                            Value = "default",
                            Default = opts.SortIncludeMatch
                        },
                    .. opts.IncludedFields.AsEnumerable().Where(f => !new List<ListEntityField>(){
                            ListEntityField.Description, ListEntityField.Groups, ListEntityField.Aliases
                        }.Any(except => except == f.Key)).Select(field =>
                            new MessageComponentOption()
                            {
                                Label = field.Key.ToLabel(),
                                Value = field.Key.ToString(),
                                Default = !opts.SortIncludeMatch && field.Value
                            }
                        ),
                    new()
                    {
                        Label = "No Extra Info",
                        Value = "none",
                        Default = !opts.SortIncludeMatch && opts.IncludedCount == 0
                    }
                    ]
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = $"Show only {entity}s with:",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "filter_with",
                Required = false,
                MaxValues = (uint?)opts.WithFilter.Count,
                Options = [
                        .. opts.WithFilter.AsEnumerable().Select(field =>
                        new MessageComponentOption()
                        {
                            Label = field.Key.ToLabel(),
                            Value = field.Key.ToString(),
                            Default = !opts.SortIncludeMatch && field.Value
                        }
                        )],
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = $"Show only {entity}s without:",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "filter_without",
                Required = false,
                MaxValues = (uint?)opts.WithOutFilter.Count,
                Options = [
                        .. opts.WithOutFilter.AsEnumerable().Select(field =>
                        new MessageComponentOption()
                        {
                            Label = field.Key.ToLabel(),
                            Value = field.Key.ToString(),
                            Default = !opts.SortIncludeMatch && field.Value
                        })
                    ],
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = "Extra Options",
            Component = new()
            {
                Type = ComponentType.CheckboxGroup,
                CustomId = "extra_checkboxes",
                Required = false,
                Options = [

                        new()
                        {
                            Label = "Reverse Sort",
                            Value = "reverse",
                            Default = opts.Reverse
                        },
                    new()
                    {
                        Label = "Show IDs",
                        Value = "id",
                        Default = opts.ShowIds
                    },
                    new()
                    {
                        Label = "Number Lines",
                        Value = "number",
                        Default = opts.NumberItems
                    }
                    ]
            }
        }
        ];

    public static MessageComponent[] longOptionsModal(ListOptions opts, string entity) => [
        new()
        {
            Type = ComponentType.Label,
            Label = "Sort By",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "sort_select",
                MaxValues = 1,
                Options = [
                        .. opts.SortOptions.Select(field =>
                        new MessageComponentOption()
                        {
                            Label = field.ToLabel(),
                            Value = field.ToString(),
                            Default = opts.SortProperty == field
                        })
                    ]
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = "Extra Info",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "info_select",
                Required = false,
                MaxValues = (uint?)opts.IncludedFields.Count,
                Options = [
                        .. opts.IncludedFields.AsEnumerable().Select(field =>
                            new MessageComponentOption()
                            {
                                Label = field.Key.ToLabel(),
                                Value = field.Key.ToString(),
                                Default = field.Value
                            }
                        )
                    ]
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = $"Show only {entity}s with:",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "filter_with",
                Required = false,
                MaxValues = (uint?)opts.WithFilter.Count,
                Options = [
                        .. opts.WithFilter.AsEnumerable().Select(field =>
                        new MessageComponentOption()
                        {
                            Label = field.Key.ToLabel(),
                            Value = field.Key.ToString(),
                            Default = !opts.SortIncludeMatch && field.Value
                        }
                        )],
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = $"Show only {entity}s without:",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "filter_without",
                Required = false,
                MaxValues = (uint?)opts.WithOutFilter.Count,
                Options = [
                        .. opts.WithOutFilter.AsEnumerable().Select(field =>
                        new MessageComponentOption()
                        {
                            Label = field.Key.ToLabel(),
                            Value = field.Key.ToString(),
                            Default = !opts.SortIncludeMatch && field.Value
                        })
                    ],
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = "Extra Options",
            Component = new()
            {
                Type = ComponentType.CheckboxGroup,
                CustomId = "extra_checkboxes",
                Required = false,
                Options = [
                        new()
                        {
                            Label = "Reverse Sort",
                            Value = "reverse",
                            Default = opts.Reverse
                        },
                    new()
                    {
                        Label = "Show IDs",
                        Value = "id",
                        Default = opts.ShowIds
                    }
                    ]
            }
        }
    ];

    public static async Task RenderCV2MemberList(this Context ctx, LookupContext lookupCtx, SystemId system,
                                string title, string color, ListOptions opts, EmbedService embeds)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var members = (await ctx.Database.Execute(conn => conn.QueryMemberList(system, opts.ToQueryOptions(), opts.Type == ListType.Long)))
            .SortByMemberListOptions(opts, lookupCtx)
            .ToList();

        // Make the defaults match the behavior of the non-cv2 list
        if (opts.IncludedCount == 0 || opts.Type == ListType.Long)
            opts.IncludedFields[ListEntityField.ProxyTags] = true;
        if (opts.Type == ListType.Long)
        {
            opts.IncludedFields[ListEntityField.DisplayName] = true;
            opts.IncludedFields[ListEntityField.Pronouns] = true;
            opts.IncludedFields[ListEntityField.Birthdate] = true;
            opts.IncludedFields[ListEntityField.Description] = true;
        }

        var footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(members.Count)}.";

        var itemsPerPage = 25;
        await ctx.PaginateCV2(title, color, Renderer, itemsPerPage, opts.Type == ListType.Short ? memberShortOptionsModal : memberLongOptionsModal);

        // Base renderer, dispatches based on type
        (string, List<ContextUtils.PageItem>) Renderer(ApplicationCommandInteractionData? optsOverride)
        {
            if (optsOverride != null)
            {
                ParseListOptionUpdates(opts, optsOverride);
                members = members.SortByMemberListOptions(opts, lookupCtx).ToList();
            }

            var filteredMembers = members.FilterByMemberListOptions(opts, lookupCtx);

            footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(filteredMembers.Count())}.";

            if (opts.Type == ListType.Short)
                return (footer, ShortRenderer(filteredMembers).ToList());
            else
                return (footer, LongRenderer(filteredMembers).ToList());
        }

        IEnumerable<ContextUtils.PageItem> ShortRenderer(IEnumerable<ListedMember> members)
        {
            // if there are both 5 and 6 character Hids they should be padded to align correctly.
            var shouldPad = members.Any(x => x.Hid.Length > 5);

            return members.Select((m, i) =>
                {
                    var ret = "";
                    if (opts.NumberItems) ret += $"{i + 1}. ";
                    if (opts.ShowIds)
                        ret += $"[`{m.DisplayHid(ctx.Config, isList: true, shouldPad: shouldPad)}`] ";
                    ret += $"**{m.NameFor(ctx)}** ";

                    if (opts.IncludedFields[ListEntityField.MessageCount] && m.MessageCountFor(lookupCtx) is { } count)
                        ret += $"({count} messages)";
                    else if (opts.IncludedFields[ListEntityField.LastSwitch] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                        ret += $"(last switched in: <t:{lastSw.Value.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludedFields[ListEntityField.LastMessage] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                        ret += $"(last message: <t:{m.LastMessageTimestamp.Value.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludedFields[ListEntityField.CreationDate] && m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                        ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludedFields[ListEntityField.Avatar] && m.AvatarFor(lookupCtx) is { } avatarUrl)
                        ret += $"([avatar URL]({avatarUrl}))";
                    else if (opts.IncludedFields[ListEntityField.Pronouns] && m.PronounsFor(lookupCtx) is { } pronouns)
                        ret += $"({pronouns})";
                    else if (opts.IncludedFields[ListEntityField.DisplayName] && m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                        ret += $"({m.DisplayName})";
                    else if (opts.IncludedFields[ListEntityField.Birthdate] && m.BirthdayFor(lookupCtx) is { } birthday)
                        ret += $"(birthday: {m.BirthdayString})";
                    else if (opts.IncludedFields[ListEntityField.ProxyTags] && m.HasProxyTags && m.ProxyPrivacy.CanAccess(lookupCtx))
                    {
                        var proxyTagsString = m.ProxyTagsString();
                        if (proxyTagsString.Length > 100) // arbitrary threshold for now, tweak?
                            proxyTagsString = "tags too long, see member card";
                        ret += $"*(*{proxyTagsString}*)*";
                    }

                    return new ContextUtils.PageItem() { CharacterCount = ret.Length, Text = ret };
                });
        }

        IEnumerable<ContextUtils.PageItem> LongRenderer(IEnumerable<ListedMember> page) =>
            page.Select(m =>
            {
                var container = embeds.CreateMemberMessageComponents(null, m, null, ctx.Config, lookupCtx, m.Groups.ToList(), opts, true);
                return new ContextUtils.PageItem()
                {
                    CharacterCount = MiscUtils.CountCharacters(container),
                    Components = container
                };
            });

        MessageComponent[] memberShortOptionsModal() => shortOptionsModal(opts, "member");
        MessageComponent[] memberLongOptionsModal() => longOptionsModal(opts, "member");

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
        await ctx.Paginate(members.ToAsyncEnumerable(), members.Count, itemsPerPage, embedTitle, color, Renderer, true);

        // Base renderer, dispatches based on type
        Task Renderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
        {
            // Add a global footer with the filter/sort string + result count
            eb.Footer(new Embed.EmbedFooter($"{opts.CreateFilterString()} {"result".ToQuantity(members.Count)}."));

            // Then call the specific renderers
            if (opts.Type == ListType.Short)
                ShortRenderer(eb, page);
            else
                LongRenderer(eb, page);

            return Task.CompletedTask;
        }

        void ShortRenderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
        {
            // if there are both 5 and 6 character Hids they should be padded to align correctly.
            var shouldPad = page.Any(x => x.Hid.Length > 5);

            // We may end up over the description character limit
            // so run it through a helper that "makes it work" :)
            eb.WithSimpleLineContent(page.Select(m =>
            {
                var ret = $"[`{m.DisplayHid(ctx.Config, isList: true, shouldPad: shouldPad)}`] **{m.NameFor(ctx)}** ";

                if (opts.IncludedFields[ListEntityField.MessageCount] && m.MessageCountFor(lookupCtx) is { } count)
                    ret += $"({count} messages)";
                else if (opts.IncludedFields[ListEntityField.LastSwitch] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                    ret += $"(last switched in: <t:{lastSw.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.LastMessage] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                    ret += $"(last message: <t:{m.LastMessageTimestamp.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.CreationDate] && m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                    ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.Avatar] && m.AvatarFor(lookupCtx) is { } avatarUrl)
                    ret += $"([avatar URL]({avatarUrl}))";
                else if (opts.IncludedFields[ListEntityField.Pronouns] && m.PronounsFor(lookupCtx) is { } pronouns)
                    ret += $"({pronouns})";
                else if (opts.IncludedFields[ListEntityField.DisplayName] && m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                    ret += $"({m.DisplayName})";
                else if (opts.IncludedFields[ListEntityField.Birthdate] && m.BirthdayFor(lookupCtx) is { } birthday)
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
                var profile = new StringBuilder($"**ID**: {m.DisplayHid(ctx.Config)}");

                if (m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Display name**: {m.DisplayName}");

                if (m.PronounsFor(lookupCtx) is { } pronouns)
                    profile.Append($"\n**Pronouns**: {pronouns}");

                if (m.BirthdayFor(lookupCtx) != null)
                    profile.Append($"\n**Birthdate**: {m.BirthdayString}");

                if (m.ProxyTags.Count > 0 && m.ProxyPrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Proxy tags**: {m.ProxyTagsString()}");

                if ((opts.IncludedFields[ListEntityField.MessageCount] || opts.SortProperty == ListEntityField.MessageCount) &&
                    m.MessageCountFor(lookupCtx) is { } count && m.MessageCount > 0)
                    profile.Append($"\n**Message count:** {count}");

                if ((opts.IncludedFields[ListEntityField.LastMessage] || opts.SortProperty == ListEntityField.LastMessage) && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                    profile.Append($"\n**Last message:** {m.LastMessageTimestamp.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludedFields[ListEntityField.LastSwitch] || opts.SortProperty == ListEntityField.LastSwitch) &&
                    m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                    profile.Append($"\n**Last switched in:** {lastSw.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludedFields[ListEntityField.CreationDate] || opts.SortProperty == ListEntityField.CreationDate) &&
                    m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludedFields[ListEntityField.Avatar] && m.AvatarFor(lookupCtx) is { } avatar)
                    profile.Append($"\n**Avatar URL:** {avatar.TryGetCleanCdnUrl()}");

                if (m.DescriptionFor(lookupCtx) is { } desc)
                    profile.Append($"\n\n{desc}");

                if (m.MemberVisibility == PrivacyLevel.Private)
                    profile.Append("\n*(this member is hidden)*");

                eb.Field(new Embed.Field(m.NameFor(ctx), profile.ToString().Truncate(1024)));
            }
        }
    }

    public static async Task RenderCV2GroupList(this Context ctx, LookupContext lookupCtx, SystemId system,
                                string title, string color, ListOptions opts, EmbedService embeds)
    {
        var countctx = LookupContext.ByNonOwner;
        if (ctx.MatchFlag("a", "all"))
        {
            if (system == ctx.System?.Id)
                countctx = LookupContext.ByOwner;
            else
                throw Errors.LookupNotAllowed;
        }

        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var groups = (await ctx.Database.Execute(conn => conn.QueryGroupList(system, opts.ToQueryOptions())))
            .SortByGroupListOptions(opts, lookupCtx, countctx)
            .ToList();

        // Make the defaults match the behavior of the non-cv2 list
        if (opts.IncludedCount == 0 || opts.Type == ListType.Long)
            opts.IncludedFields[ListEntityField.MemberCount] = true;
        if (opts.Type == ListType.Long)
        {
            opts.IncludedFields[ListEntityField.DisplayName] = true;
            opts.IncludedFields[ListEntityField.MemberCount] = true;
            opts.IncludedFields[ListEntityField.Description] = true;
        }

        var footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(groups.Count)}.";
        var itemsPerPage = 25;

        await ctx.PaginateCV2(title, color, Renderer, itemsPerPage, opts.Type == ListType.Short ? groupShortOptionsModal : groupLongOptionsModal);

        // Base renderer, dispatches based on type
        (string, List<ContextUtils.PageItem>) Renderer(ApplicationCommandInteractionData? optsOverride)
        {
            if (optsOverride != null)
            {
                ParseListOptionUpdates(opts, optsOverride);
                groups = groups.SortByGroupListOptions(opts, lookupCtx, countctx).ToList();
            }

            var filteredGroups = groups.FilterByGroupListOptions(opts, lookupCtx);

            if (opts.Type == ListType.Short)
                return (footer, ShortRenderer(filteredGroups).ToList());
            else
                return (footer, LongRenderer(filteredGroups).ToList());
        }

        IEnumerable<ContextUtils.PageItem> ShortRenderer(IEnumerable<ListedGroup> groups)
        {
            // if there are both 5 and 6 character Hids they should be padded to align correctly.
            var shouldPad = groups.Any(x => x.Hid.Length > 5);

            return groups.Select((g, i) =>
            {
                var ret = "";
                if (opts.NumberItems) ret += $"{i + 1}. ";
                if (opts.ShowIds)
                    ret += $"[`{g.DisplayHid(ctx.Config, isList: true, shouldPad: shouldPad)}`] ";
                ret += $"**{g.NameFor(ctx)}** ";

                if (opts.IncludedFields[ListEntityField.DisplayName] && g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                    ret += $"({g.DisplayName})";
                else if (opts.IncludedFields[ListEntityField.CreationDate] &&
                         g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                    ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.Avatar] && g.IconFor(lookupCtx) is { } avatarUrl)
                    ret += $"([avatar URL]({avatarUrl}))";
                else if (opts.IncludedFields[ListEntityField.MemberCount])
                    // -priv/-pub and listprivacy affects whether count is shown
                    // -all and visibility affects what the count is
                    if (ctx.DirectLookupContextFor(system) == LookupContext.ByOwner)
                    {
                        if (g.ListPrivacy == PrivacyLevel.Public || lookupCtx == LookupContext.ByOwner)
                            if (ctx.MatchFlag("all", "a"))
                                ret += $"({"member".ToQuantity(g.MemberIds.Count)})";
                            else
                                ret += $"({"member".ToQuantity(g.PublicMemberCount)})";
                    }
                    else
                        if (g.ListPrivacy == PrivacyLevel.Public)
                            ret += $"({"member".ToQuantity(g.PublicMemberCount)})";
                return new ContextUtils.PageItem() { CharacterCount = ret.Length, Text = ret };
            });
        }

        IEnumerable<ContextUtils.PageItem> LongRenderer(IEnumerable<ListedGroup> page) =>
            page.Select(g =>
                {
                    var memberCount = countctx == LookupContext.ByOwner ? g.MemberIds.Count : g.PublicMemberCount;
                    var components = embeds.CreateGroupMessageComponents(ctx, null, g, memberCount, lookupCtx, null, opts, true);
                    return new ContextUtils.PageItem()
                    {
                        CharacterCount = MiscUtils.CountCharacters(components),
                        Components = components
                    };
                });

        MessageComponent[] groupShortOptionsModal() => shortOptionsModal(opts, "group");
        MessageComponent[] groupLongOptionsModal() => longOptionsModal(opts, "group");

    }

    public static async Task RenderGroupList(this Context ctx, LookupContext lookupCtx,
                                SystemId system, string embedTitle, string color, ListOptions opts)
    {
        var countctx = LookupContext.ByNonOwner;
        if (ctx.MatchFlag("a", "all"))
        {
            if (system == ctx.System?.Id)
                countctx = LookupContext.ByOwner;
            else
                throw Errors.LookupNotAllowed;
        }

        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var groups = (await ctx.Database.Execute(conn => conn.QueryGroupList(system, opts.ToQueryOptions())))
            .SortByGroupListOptions(opts, lookupCtx, countctx)
            .ToList();

        var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;
        await ctx.Paginate(groups.ToAsyncEnumerable(), groups.Count, itemsPerPage, embedTitle, color, Renderer, true);

        // Base renderer, dispatches based on type
        Task Renderer(EmbedBuilder eb, IEnumerable<ListedGroup> page)
        {
            // Add a global footer with the filter/sort string + result count
            eb.Footer(new Embed.EmbedFooter($"{opts.CreateFilterString()} {"result".ToQuantity(groups.Count)}."));

            // Then call the specific renderers
            if (opts.Type == ListType.Short)
                ShortRenderer(eb, page);
            else
                LongRenderer(eb, page);

            return Task.CompletedTask;
        }

        void ShortRenderer(EmbedBuilder eb, IEnumerable<ListedGroup> page)
        {
            // if there are both 5 and 6 character Hids they should be padded to align correctly.
            var shouldPad = page.Any(x => x.Hid.Length > 5);

            // We may end up over the description character limit
            // so run it through a helper that "makes it work" :)
            eb.WithSimpleLineContent(page.Select(g =>
            {
                var ret = $"[`{g.DisplayHid(ctx.Config, isList: true, shouldPad: shouldPad)}`] **{g.NameFor(ctx)}** ";

                switch (opts.SortProperty.Field)
                {
                    case ListEntityField.DisplayName:
                        {
                            if (g.NamePrivacy.CanAccess(lookupCtx) && g.DisplayName != null)
                                ret += $"({g.DisplayName})";
                            break;
                        }
                    case ListEntityField.CreationDate:
                        {
                            if (g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            break;
                        }
                    default:
                        {
                            if (opts.IncludedFields[ListEntityField.CreationDate] &&
                                     g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                            {
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            }
                            else if (opts.IncludedFields[ListEntityField.DisplayName] && g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                            {
                                ret += $"({g.DisplayName})";
                            }
                            else if (opts.IncludedFields[ListEntityField.Avatar] && g.IconFor(lookupCtx) is { } avatarUrl)
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
                                            ret += $"({"member".ToQuantity(g.MemberIds.Count)})";
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
                var profile = new StringBuilder($"**ID**: {g.DisplayHid(ctx.Config)}");

                if (g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Display name**: {g.DisplayName}");

                if (g.ListPrivacy == PrivacyLevel.Public || lookupCtx == LookupContext.ByOwner)
                {
                    if (ctx.MatchFlag("all", "a") && ctx.DirectLookupContextFor(system) == LookupContext.ByOwner)
                        profile.Append($"\n**Member Count:** {g.MemberIds.Count}");
                    else
                        profile.Append($"\n**Member Count:** {g.PublicMemberCount}");
                }

                if ((opts.IncludedFields[ListEntityField.CreationDate] || opts.SortProperty == ListEntityField.CreationDate) &&
                    g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludedFields[ListEntityField.Avatar] && g.IconFor(lookupCtx) is { } avatar)
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
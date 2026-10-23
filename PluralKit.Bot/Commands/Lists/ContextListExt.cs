using System.Text;
using Humanizer;

using Myriad.Builders;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextListExt
{
    public static ListOptions ParseListOptions(this Context ctx, LookupContext directLookupCtx, LookupContext lookupContext)
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
        if (ctx.MatchFlag("random", "rand")) p.SortProperty = SortProperty.Random;

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
            p.IncludeValues[EntityField.LastSwitch] = true;
        if (ctx.MatchFlag("with-last-message", "with-last-proxy", "wlm", "wlp"))
            p.IncludeValues[EntityField.LastMessage] = true;
        if (ctx.MatchFlag("with-message-count", "wmc"))
            p.IncludeValues[EntityField.MessageCount] = true;
        if (ctx.MatchFlag("with-created", "wc"))
            p.IncludeValues[EntityField.CreationDate] = true;
        if (ctx.MatchFlag("with-avatar", "with-image", "with-icon", "wa", "wi", "ia", "ii", "img"))
            p.IncludeValues[EntityField.Avatar] = true;
        if (ctx.MatchFlag("with-pronouns", "wp", "wprns"))
            p.IncludeValues[EntityField.Pronouns] = true;
        if (ctx.MatchFlag("with-displayname", "wdn"))
            p.IncludeValues[EntityField.Displayname] = true;
        if (ctx.MatchFlag("with-birthday", "wbd", "wb"))
            p.IncludeValues[EntityField.Birthday] = true;

        // Always show the sort property, too (unless this is the short list and we are already showing something else)
        if (p.Type != ListType.Short || p.IncludedCount == 0)
        {
            if (p.SortProperty == SortProperty.DisplayName) p.IncludeValues[EntityField.Displayname] = true;
            if (p.SortProperty == SortProperty.MessageCount) p.IncludeValues[EntityField.MessageCount] = true;
            if (p.SortProperty == SortProperty.CreationDate) p.IncludeValues[EntityField.CreationDate] = true;
            if (p.SortProperty == SortProperty.LastSwitch) p.IncludeValues[EntityField.LastSwitch] = true;
            if (p.SortProperty == SortProperty.LastMessage) p.IncludeValues[EntityField.LastMessage] = true;
            if (p.SortProperty == SortProperty.Birthdate) p.IncludeValues[EntityField.Birthday] = true;
            // If this is a short list and we went from 0 -> 1 included count here then we made the sort and includes match
            if (p.Type == ListType.Short && p.IncludedCount == 1) p.SortIncludeMatch = true;
        }

        // Make sure the options are valid
        p.AssertIsValid();

        // Done!
        return p;
    }

    public static async Task RenderCV2MemberList(this Context ctx, LookupContext lookupCtx, SystemId system,
                                string title, string color, ListOptions opts, EmbedService embeds)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var members = (await ctx.Database.Execute(conn => conn.QueryMemberList(system, opts.ToQueryOptions())))
            .SortByMemberListOptions(opts, lookupCtx)
            .ToList();

        // Make the default match the behavior of the non-cv2 list
        opts.IncludeValues[EntityField.ProxyTags] = true;

        var footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(members.Count)}.";

        var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;
        await ctx.PaginateCV2(title, color, Renderer, itemsPerPage, opts.Type == ListType.Short ? shortOptionsModal : longOptionsModal);

        // Base renderer, dispatches based on type
        async Task<(string, List<ContextUtils.PageItem>)> Renderer(ApplicationCommandInteractionData? optsOverride)
        {
            if (optsOverride != null)
            {
                opts.ResetDynamicOptions();

                var sortComponent = optsOverride.Components.First((x) => x.Component.CustomId == "sort_select").Component;
                if (Enum.TryParse<SortProperty>(sortComponent.Values[0], out var sortResult))
                    opts.SortProperty = sortResult;

                var infoComponent = optsOverride.Components.First((x) => x.Component.CustomId == "info_select").Component;
                if (infoComponent?.Values?.Length > 0 && infoComponent.Values[0] == "default")
                {
                    infoComponent.Values[0] = sortComponent.Values[0];
                    opts.SortIncludeMatch = true;
                }
                foreach (var include in infoComponent.Values)
                    if (Enum.TryParse<EntityField>(include, out var infoResult) && opts.IncludeValues.ContainsKey(infoResult))
                        opts.IncludeValues[infoResult] = true;

                var extraCheckboxesComponent = optsOverride.Components.FirstOrDefault((x) => x.Component.CustomId == "extra_checkboxes")?.Component;
                opts.ShowIds = extraCheckboxesComponent?.Values.FirstOrDefault(x => x == "id") != null;
                opts.NumberItems = extraCheckboxesComponent?.Values.FirstOrDefault(x => x == "number") != null;
                opts.Reverse = extraCheckboxesComponent?.Values.FirstOrDefault(x => x == "reverse") != null;

                var withFilterComponent = optsOverride.Components.FirstOrDefault(x => x.Component.CustomId == "filter_with")?.Component;
                foreach (var withFilterEntryStr in withFilterComponent?.Values ?? [])
                    if (Enum.TryParse<EntityField>(withFilterEntryStr, out var withFilterEntry) && opts.WithFilter.ContainsKey(withFilterEntry))
                        opts.WithFilter[withFilterEntry] = true;

                var withOutFilterComponent = optsOverride.Components.FirstOrDefault(x => x.Component.CustomId == "filter_without")?.Component;
                foreach (var withOutFilterEntryStr in withOutFilterComponent?.Values ?? [])
                    if (Enum.TryParse<EntityField>(withOutFilterEntryStr, out var withOutFilterEntry) && opts.WithOutFilter.ContainsKey(withOutFilterEntry))
                        opts.WithOutFilter[withOutFilterEntry] = true;

                opts.AssertIsValid();

                members = members.SortByMemberListOptions(opts, lookupCtx).ToList();
            }

            var filteredMembers = members.FilterByMemberListOptions(opts, lookupCtx);

            footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(filteredMembers.Count())}.";

            if (opts.Type == ListType.Short)
                return (footer, ShortRenderer(filteredMembers).ToList());
            else
                return (footer, await LongRenderer(filteredMembers));
        }

        IEnumerable<ContextUtils.PageItem> ShortRenderer(IEnumerable<ListedMember> members)
        {
            // if there are both 5 and 6 character Hids they should be padded to align correctly.
            var shouldPad = members.Any(x => x.Hid.Length > 5);

            return members.Select(m =>
                {
                    var ret = "";
                    if (opts.NumberItems)
                        ret += $"{m.Order}. ";
                    if (opts.ShowIds)
                        ret += $"[`{m.DisplayHid(ctx.Config, isList: true, shouldPad: shouldPad)}`] ";
                    ret += $"**{m.NameFor(ctx)}** ";

                    if (opts.IncludeValues[EntityField.MessageCount] && m.MessageCountFor(lookupCtx) is { } count)
                        ret += $"({count} messages)";
                    else if (opts.IncludeValues[EntityField.LastSwitch] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                        ret += $"(last switched in: <t:{lastSw.Value.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludeValues[EntityField.LastMessage] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                        ret += $"(last message: <t:{m.LastMessageTimestamp.Value.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludeValues[EntityField.CreationDate] && m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                        ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludeValues[EntityField.Avatar] && m.AvatarFor(lookupCtx) is { } avatarUrl)
                        ret += $"([avatar URL]({avatarUrl}))";
                    else if (opts.IncludeValues[EntityField.Pronouns] && m.PronounsFor(lookupCtx) is { } pronouns)
                        ret += $"({pronouns})";
                    else if (opts.IncludeValues[EntityField.Displayname] && m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                        ret += $"({m.DisplayName})";
                    else if (opts.IncludeValues[EntityField.Birthday] && m.BirthdayFor(lookupCtx) is { } birthday)
                        ret += $"(birthday: {m.BirthdayString})";
                    else if (opts.IncludeValues[EntityField.ProxyTags] && m.HasProxyTags && m.ProxyPrivacy.CanAccess(lookupCtx))
                    {
                        var proxyTagsString = m.ProxyTagsString();
                        if (proxyTagsString.Length > 100) // arbitrary threshold for now, tweak?
                            proxyTagsString = "tags too long, see member card";
                        ret += $"*(*{proxyTagsString}*)*";
                    }

                    return new ContextUtils.PageItem() { CharacterCount = ret.Length, Text = ret };
                });
        }

        async Task<List<ContextUtils.PageItem>> LongRenderer(IEnumerable<ListedMember> page)
        {
            List<ContextUtils.PageItem> components = [];
            await foreach (var m in page.ToAsyncEnumerable())
            {
                var container = await embeds.CreateMemberMessageComponents(null, m, null, ctx.Config, lookupCtx, opts, true);
                components.Add(new()
                {
                    CharacterCount = countCharacters(container),
                    Components = container
                });
            }

            return components;
        }

        MessageComponent[] shortOptionsModal() => [
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
                            Value = SortProperty.Name.ToString(),
                            Default = opts.SortProperty == SortProperty.Name
                        },
                        new()
                        {
                            Label = "Displayname",
                            Value = SortProperty.DisplayName.ToString(),
                            Default = opts.SortProperty == SortProperty.DisplayName
                        },
                        new()
                        {
                            Label = "ID",
                            Value = SortProperty.Hid.ToString(),
                            Default = opts.SortProperty == SortProperty.Hid
                        },
                        new()
                        {
                            Label = "Message Count",
                            Value = SortProperty.MessageCount.ToString(),
                            Default = opts.SortProperty == SortProperty.MessageCount
                        },
                        new()
                        {
                            Label = "Creation Date",
                            Value = SortProperty.CreationDate.ToString(),
                            Default = opts.SortProperty == SortProperty.CreationDate
                        },
                        new()
                        {
                            Label = "Last Switch",
                            Value = SortProperty.LastSwitch.ToString(),
                            Default = opts.SortProperty == SortProperty.LastSwitch
                        },
                        new()
                        {
                            Label = "Last Message",
                            Value = SortProperty.LastMessage.ToString(),
                            Default = opts.SortProperty == SortProperty.LastMessage
                        },
                        new()
                        {
                            Label = "Birthday",
                            Value = SortProperty.Birthdate.ToString(),
                            Default = opts.SortProperty == SortProperty.Birthdate
                        },
                        new()
                        {
                            Label = "Random",
                            Value = SortProperty.Random.ToString(),
                            Default = opts.SortProperty == SortProperty.Random
                        },
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
                        new()
                        {
                            Label = "Proxy Tags",
                            Value = EntityField.ProxyTags.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.ProxyTags]
                        },
                        new()
                        {
                            Label = "Last Switch",
                            Value = EntityField.LastSwitch.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.LastSwitch]
                        },
                        new()
                        {
                            Label = "Last Message",
                            Value = EntityField.LastMessage.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.LastMessage]
                        },
                        new()
                        {
                            Label = "Message Count",
                            Value = EntityField.MessageCount.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.MessageCount]
                        },
                        new()
                        {
                            Label = "Creation Date",
                            Value = EntityField.CreationDate.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.CreationDate]
                        },
                        new()
                        {
                            Label = "Avatar Link",
                            Value = EntityField.Avatar.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.Avatar]
                        },
                        new()
                        {
                            Label = "Pronouns",
                            Value = EntityField.Pronouns.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.Pronouns]
                        },
                        new()
                        {
                            Label = "Displayname",
                            Value = EntityField.Displayname.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.Displayname]
                        },
                        new()
                        {
                            Label = "Birthday",
                            Value = EntityField.Birthday.ToString(),
                            Default = !opts.SortIncludeMatch && opts.IncludeValues[EntityField.Birthday]
                        },
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
                Label = "Show only members with:",
                Component = new()
                {
                    Type = ComponentType.StringSelect,
                    CustomId = "filter_with",
                    Required = false,
                    MaxValues = 8,
                    Options = [
                        new()
                        {
                            Label = "Displayname",
                            Value = EntityField.Displayname.ToString(),
                            Default = opts.WithFilter[EntityField.Displayname]
                        },
                        new()
                        {
                            Label = "Description",
                            Value = EntityField.Description.ToString(),
                            Default = opts.WithFilter[EntityField.Description]
                        },
                        new()
                        {
                            Label = "Avatar",
                            Value = EntityField.Avatar.ToString(),
                            Default = opts.WithFilter[EntityField.Avatar]
                        },
                        new()
                        {
                            Label = "Proxy Tags",
                            Value = EntityField.ProxyTags.ToString(),
                            Default = opts.WithFilter[EntityField.ProxyTags]
                        },
                        new()
                        {
                            Label = "Aliases",
                            Value = EntityField.Aliases.ToString(),
                            Default = opts.WithFilter[EntityField.Aliases]
                        },
                        new()
                        {
                            Label = "Pronouns",
                            Value = EntityField.Pronouns.ToString(),
                            Default = opts.WithFilter[EntityField.Pronouns]
                        },
                        new()
                        {
                            Label = "Color",
                            Value = EntityField.Color.ToString(),
                            Default = opts.WithFilter[EntityField.Color]
                        },
                        new()
                        {
                            Label = "Birthday",
                            Value = EntityField.Birthday.ToString(),
                            Default = opts.WithFilter[EntityField.Birthday]
                        },
                        // new()
                        // {
                        //     Label = "Groups",
                        //     Value = EntityField.Groups.ToString(),
                        //     Default = opts.WithFilter[EntityField.Groups]
                        // },
                    ]
                }
            },
            new()
            {
                Type = ComponentType.Label,
                Label = "Show only members without:",
                Component = new()
                {
                    Type = ComponentType.StringSelect,
                    CustomId = "filter_without",
                    Required = false,
                    MaxValues = 8,
                    Options = [
                        new()
                        {
                            Label = "Displayname",
                            Value = EntityField.Displayname.ToString(),
                            Default = opts.WithOutFilter[EntityField.Displayname]
                        },
                        new()
                        {
                            Label = "Description",
                            Value = EntityField.Description.ToString(),
                            Default = opts.WithOutFilter[EntityField.Description]
                        },
                        new()
                        {
                            Label = "Avatar",
                            Value = EntityField.Avatar.ToString(),
                            Default = opts.WithOutFilter[EntityField.Avatar]
                        },
                        new()
                        {
                            Label = "Proxy Tags",
                            Value = EntityField.ProxyTags.ToString(),
                            Default = opts.WithOutFilter[EntityField.ProxyTags]
                        },
                        new()
                        {
                            Label = "Aliases",
                            Value = EntityField.Aliases.ToString(),
                            Default = opts.WithOutFilter[EntityField.Aliases]
                        },
                        new()
                        {
                            Label = "Pronouns",
                            Value = EntityField.Pronouns.ToString(),
                            Default = opts.WithOutFilter[EntityField.Pronouns]
                        },
                        new()
                        {
                            Label = "Color",
                            Value = EntityField.Color.ToString(),
                            Default = opts.WithOutFilter[EntityField.Color]
                        },
                        new()
                        {
                            Label = "Birthday",
                            Value = EntityField.Birthday.ToString(),
                            Default = opts.WithOutFilter[EntityField.Birthday]
                        },
                        // new()
                        // {
                        //     Label = "Groups",
                        //     Value = EntityField.Groups.ToString(),
                        //     Default = opts.WithOutFilter[EntityField.Groups]
                        // },
                    ]
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

        MessageComponent[] longOptionsModal() => [
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
                            Value = SortProperty.Name.ToString(),
                            Default = opts.SortProperty == SortProperty.Name
                        },
                        new()
                        {
                            Label = "Displayname",
                            Value = SortProperty.DisplayName.ToString(),
                            Default = opts.SortProperty == SortProperty.DisplayName
                        },
                        new()
                        {
                            Label = "ID",
                            Value = SortProperty.Hid.ToString(),
                            Default = opts.SortProperty == SortProperty.Hid
                        },
                        new()
                        {
                            Label = "Message Count",
                            Value = SortProperty.MessageCount.ToString(),
                            Default = opts.SortProperty == SortProperty.MessageCount
                        },
                        new()
                        {
                            Label = "Creation Date",
                            Value = SortProperty.CreationDate.ToString(),
                            Default = opts.SortProperty == SortProperty.CreationDate
                        },
                        new()
                        {
                            Label = "Last Switch",
                            Value = SortProperty.LastSwitch.ToString(),
                            Default = opts.SortProperty == SortProperty.LastSwitch
                        },
                        new()
                        {
                            Label = "Last Message",
                            Value = SortProperty.LastMessage.ToString(),
                            Default = opts.SortProperty == SortProperty.LastMessage
                        },
                        new()
                        {
                            Label = "Birthday",
                            Value = SortProperty.Birthdate.ToString(),
                            Default = opts.SortProperty == SortProperty.Birthdate
                        },
                        new()
                        {
                            Label = "Random",
                            Value = SortProperty.Random.ToString(),
                            Default = opts.SortProperty == SortProperty.Random
                        },
                    ]
                }
            },
            new()
            {
                Type = ComponentType.Label,
                Label = "Extra Info",
                Component = new()
                {
                    Type = ComponentType.CheckboxGroup,
                    CustomId = "info_select",
                    Required = false,
                    Options = [
                        new()
                        {
                            Label = "Proxy Tags",
                            Value = EntityField.ProxyTags.ToString(),
                            Default = opts.IncludeValues[EntityField.ProxyTags]
                        },
                        new()
                        {
                            Label = "Aliases",
                            Value = EntityField.Aliases.ToString(),
                            Default = opts.IncludeValues[EntityField.Aliases]
                        },
                        new()
                        {
                            Label = "Avatar",
                            Value = EntityField.Avatar.ToString(),
                            Default = opts.IncludeValues[EntityField.Avatar]
                        },
                        new()
                        {
                            Label = "Groups",
                            Value = EntityField.Groups.ToString(),
                            Default = opts.IncludeValues[EntityField.Groups]
                        },
                        new()
                        {
                            Label = "Description",
                            Value = EntityField.Description.ToString(),
                            Default = opts.IncludeValues[EntityField.Description]
                        }
                    ]
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
                        }
                    ]
                }
            }
        ];


        int countCharacters(MessageComponent[] components) =>
            components.Sum(com =>
                com.Type switch
                {
                    ComponentType.Text => (com.Content ?? "").Length,
                    ComponentType.Section => countCharacters(com.Components ?? []),
                    _ => 0,
                }
            );
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

                if (opts.IncludeValues[EntityField.MessageCount] && m.MessageCountFor(lookupCtx) is { } count)
                    ret += $"({count} messages)";
                else if (opts.IncludeValues[EntityField.LastSwitch] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                    ret += $"(last switched in: <t:{lastSw.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludeValues[EntityField.LastMessage] && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                    ret += $"(last message: <t:{m.LastMessageTimestamp.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludeValues[EntityField.CreationDate] && m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                    ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                else if (opts.IncludeValues[EntityField.Avatar] && m.AvatarFor(lookupCtx) is { } avatarUrl)
                    ret += $"([avatar URL]({avatarUrl}))";
                else if (opts.IncludeValues[EntityField.Pronouns] && m.PronounsFor(lookupCtx) is { } pronouns)
                    ret += $"({pronouns})";
                else if (opts.IncludeValues[EntityField.Displayname] && m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                    ret += $"({m.DisplayName})";
                else if (opts.IncludeValues[EntityField.Birthday] && m.BirthdayFor(lookupCtx) is { } birthday)
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

                if ((opts.IncludeValues[EntityField.MessageCount] || opts.SortProperty == SortProperty.MessageCount) &&
                    m.MessageCountFor(lookupCtx) is { } count && m.MessageCount > 0)
                    profile.Append($"\n**Message count:** {count}");

                if ((opts.IncludeValues[EntityField.LastMessage] || opts.SortProperty == SortProperty.LastMessage) && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessageTimestamp, out var lastMsg))
                    profile.Append($"\n**Last message:** {m.LastMessageTimestamp.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludeValues[EntityField.LastSwitch] || opts.SortProperty == SortProperty.LastSwitch) &&
                    m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw))
                    profile.Append($"\n**Last switched in:** {lastSw.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludeValues[EntityField.CreationDate] || opts.SortProperty == SortProperty.CreationDate) &&
                    m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludeValues[EntityField.Avatar] && m.AvatarFor(lookupCtx) is { } avatar)
                    profile.Append($"\n**Avatar URL:** {avatar.TryGetCleanCdnUrl()}");

                if (m.DescriptionFor(lookupCtx) is { } desc)
                    profile.Append($"\n\n{desc}");

                if (m.MemberVisibility == PrivacyLevel.Private)
                    profile.Append("\n*(this member is hidden)*");

                eb.Field(new Embed.Field(m.NameFor(ctx), profile.ToString().Truncate(1024)));
            }
        }
    }

    public static async Task RenderCV2GroupList(this Context ctx, LookupContext lookupCtx,
                                SystemId system, string embedTitle, string color, ListOptions opts)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var groups = (await ctx.Database.Execute(conn => conn.QueryGroupList(system, opts.ToQueryOptions())))
            .SortByGroupListOptions(opts, lookupCtx)
            .ToList();

        var footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(groups.Count)}.";
        var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;

        await ctx.PaginateCV2(embedTitle, color, Renderer, itemsPerPage);

        // Base renderer, dispatches based on type
        async Task<(string, List<ContextUtils.PageItem>)> Renderer(ApplicationCommandInteractionData? optsOverride)
        {
            if (opts.Type == ListType.Short)
                return (footer, ShortRenderer(groups).ToList());
            else
                return (footer, LongRenderer(groups).ToList());
        }

        IEnumerable<ContextUtils.PageItem> ShortRenderer(IEnumerable<ListedGroup> groups)
        {
            // if there are both 5 and 6 character Hids they should be padded to align correctly.
            var shouldPad = groups.Any(x => x.Hid.Length > 5);

            return groups.Select(g =>
            {
                var ret = $"[`{g.DisplayHid(ctx.Config, isList: true, shouldPad: shouldPad)}`] **{g.NameFor(ctx)}** ";

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
                            if (opts.IncludeValues[EntityField.CreationDate] &&
                                     g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                            {
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            }
                            else if (opts.IncludeValues[EntityField.Displayname] && g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                            {
                                ret += $"({g.DisplayName})";
                            }
                            else if (opts.IncludeValues[EntityField.Avatar] && g.IconFor(lookupCtx) is { } avatarUrl)
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

                return new ContextUtils.PageItem() { CharacterCount = ret.Length, Text = ret };
            });
        }

        IEnumerable<ContextUtils.PageItem> LongRenderer(IEnumerable<ListedGroup> page)
        {
            return page.Select(g =>
            {
                var profile = new StringBuilder($"## {g.NameFor(ctx)}\n**ID**: {g.DisplayHid(ctx.Config)}");

                if (g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Display name**: {g.DisplayName}");

                if (g.ListPrivacy == PrivacyLevel.Public || lookupCtx == LookupContext.ByOwner)
                {
                    if (ctx.MatchFlag("all", "a") && ctx.DirectLookupContextFor(system) == LookupContext.ByOwner)
                        profile.Append($"\n**Member Count:** {g.TotalMemberCount}");
                    else
                        profile.Append($"\n**Member Count:** {g.PublicMemberCount}");
                }

                if ((opts.IncludeValues[EntityField.CreationDate] || opts.SortProperty == SortProperty.CreationDate) &&
                    g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludeValues[EntityField.Avatar] && g.IconFor(lookupCtx) is { } avatar)
                    profile.Append($"\n**Avatar URL:** {avatar.TryGetCleanCdnUrl()}");

                if (g.DescriptionFor(lookupCtx) is { } desc)
                    profile.Append($"\n\n{desc}");

                if (g.Visibility == PrivacyLevel.Private)
                    profile.Append("\n*(this group is hidden)*");

                return new ContextUtils.PageItem()
                {
                    CharacterCount = profile.ToString().Length,
                    Components = [new MessageComponent()
                    {
                        Type = ComponentType.Text,
                        Content = profile.ToString()
                    }]
                };
            });
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
                            if (opts.IncludeValues[EntityField.CreationDate] &&
                                     g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                            {
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            }
                            else if (opts.IncludeValues[EntityField.Displayname] && g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                            {
                                ret += $"({g.DisplayName})";
                            }
                            else if (opts.IncludeValues[EntityField.Avatar] && g.IconFor(lookupCtx) is { } avatarUrl)
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
                var profile = new StringBuilder($"**ID**: {g.DisplayHid(ctx.Config)}");

                if (g.DisplayName != null && g.NamePrivacy.CanAccess(lookupCtx))
                    profile.Append($"\n**Display name**: {g.DisplayName}");

                if (g.ListPrivacy == PrivacyLevel.Public || lookupCtx == LookupContext.ByOwner)
                {
                    if (ctx.MatchFlag("all", "a") && ctx.DirectLookupContextFor(system) == LookupContext.ByOwner)
                        profile.Append($"\n**Member Count:** {g.TotalMemberCount}");
                    else
                        profile.Append($"\n**Member Count:** {g.PublicMemberCount}");
                }

                if ((opts.IncludeValues[EntityField.CreationDate] || opts.SortProperty == SortProperty.CreationDate) &&
                    g.MetadataPrivacy.TryGet(lookupCtx, g.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludeValues[EntityField.Avatar] && g.IconFor(lookupCtx) is { } avatar)
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
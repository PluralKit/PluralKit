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
        var p = new ListOptions(entityType, directLookupCtx == LookupContext.ByOwner);

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
        if (ctx.MatchFlag("random", "rand")) p.SortProperty = ExtraSortOption.Random;

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
        var components = optsOverride.Components;

        var sortComponent = components.FirstOrDefault(x => x.Component.CustomId == "sort_select")?.Component;
        if (sortComponent != null)
        {
            opts.ResetSort();
            if (SortOption.TryParse(sortComponent.Values[0], out var sortResult))
                opts.SortProperty = sortResult;
        }

        var infoComponent = components.FirstOrDefault((x) => x.Component.CustomId == "info_select")?.Component;
        if (infoComponent != null)
        {
            opts.ResetIncludes();
            if (infoComponent.Values?[0] == "default")
            {
                if (opts.SortProperty == ExtraSortOption.FullBirthdate)
                    infoComponent.Values[0] = ListEntityField.Birthdate.ToString();
                else
                    infoComponent.Values[0] = opts.SortProperty.ToString();
                opts.SortIncludeMatch = true;
            }
            foreach (var include in infoComponent.Values)
                if (Enum.TryParse<ListEntityField>(include, out var infoResult))
                    opts.IncludedFields[infoResult] = true;
        }

        var extraDisplayComponent = components.FirstOrDefault((x) => x.Component.CustomId == "extra_checkboxes_display")?.Component;
        if (extraDisplayComponent != null)
        {
            opts.ShowIds = extraDisplayComponent?.Values.FirstOrDefault(x => x == "id") != null;
            opts.NumberItems = extraDisplayComponent?.Values.FirstOrDefault(x => x == "number") != null;
            opts.Reverse = extraDisplayComponent?.Values.FirstOrDefault(x => x == "reverse") != null;
        }

        var extraFilterComponent = components.FirstOrDefault((x) => x.Component.CustomId == "extra_checkboxes_filter")?.Component;
        if (extraFilterComponent != null)
        {
            opts.SearchDescription = extraFilterComponent?.Values.FirstOrDefault(x => x == "description") != null;
        }

        var withFilterComponent = components.FirstOrDefault(x => x.Component.CustomId == "filter_with")?.Component;
        if (withFilterComponent != null)
        {
            opts.ResetWithFilter();
            foreach (var withFilterEntryStr in withFilterComponent?.Values ?? [])
                if (Enum.TryParse<ListEntityField>(withFilterEntryStr, out var withFilterEntry) && opts.WithFilter.ContainsKey(withFilterEntry))
                    opts.WithFilter[withFilterEntry] = true;
        }

        var withOutFilterComponent = components.FirstOrDefault(x => x.Component.CustomId == "filter_without")?.Component;
        if (withOutFilterComponent != null)
        {
            opts.ResetWithOutFilter();
            foreach (var withOutFilterEntryStr in withOutFilterComponent?.Values ?? [])
                if (Enum.TryParse<ListEntityField>(withOutFilterEntryStr, out var withOutFilterEntry) && opts.WithOutFilter.ContainsKey(withOutFilterEntry))
                    opts.WithOutFilter[withOutFilterEntry] = true;
        }

        var entityPrivacyFilterComponent = components.FirstOrDefault(x => x.Component.CustomId == "entity_privacy_filter")?.Component;
        if (entityPrivacyFilterComponent != null)
            opts.PrivacyFilter = entityPrivacyFilterComponent.Values?[0] switch
            {
                "public" => PrivacyLevel.Public,
                "private" => PrivacyLevel.Private,
                "all" => null,
                _ => throw new ArgumentException()
            };

        var infoPrivacyFilterComponent = components.FirstOrDefault(x => x.Component.CustomId == "info_privacy_filter")?.Component;
        if (infoPrivacyFilterComponent != null)
            opts.Context = infoPrivacyFilterComponent.Values?[0] switch
            {
                "public" => LookupContext.ByNonOwner,
                "all" => LookupContext.ByOwner,
                _ => throw new ArgumentException()
            };

        var searchComponent = components.FirstOrDefault(x => x.Component.CustomId == "search")?.Component;
        if (searchComponent != null)
            opts.Search = searchComponent.Value != "" ? searchComponent.Value : null;

        opts.AssertIsValid();
    }

    public static MessageComponent[] ShortDisplayOptionsModal(ListOptions opts, bool isOwner) => ((IEnumerable<MessageComponent>)[
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
        isOwner ? new()
        {
            Type = ComponentType.Label,
            Label = "Show What Information (not which members):",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "info_privacy_filter",
                Options = [
                    new()
                    {
                        Label = "Public Information Only",
                        Value = "public",
                        Default = opts.Context == LookupContext.ByNonOwner
                    },
                    new()
                    {
                        Label = "All Information",
                        Value = "all",
                        Default = opts.Context == LookupContext.ByOwner
                    }
                ]
            }
        } : null,
        new()
        {
            Type = ComponentType.Label,
            Label = "Extra Options",
            Component = new()
            {
                Type = ComponentType.CheckboxGroup,
                CustomId = "extra_checkboxes_display",
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
    ]).Where(c => c != null).ToArray();

    public static MessageComponent[] ShortFilterOptionsModal(ListOptions opts, bool isOwner, ListEntity entity) => ((IEnumerable<MessageComponent>)[
        isOwner ? new()
        {
            Type = ComponentType.Label,
            Label = $"Show Which {entity}s (not what info):",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "entity_privacy_filter",
                Options = [
                    new()
                    {
                        Label = $"Public {entity}s Only",
                        Value = "public",
                        Default = opts.PrivacyFilter == PrivacyLevel.Public
                    },
                    new()
                    {
                        Label = $"Private {entity}s Only",
                        Value = "private",
                        Default = opts.PrivacyFilter == PrivacyLevel.Private
                    },
                    new()
                    {
                        Label = $"All {entity}s",
                        Value = "all",
                        Default = opts.PrivacyFilter == null
                    }
                ]
            }
        } : null,
        new()
        {
            Type = ComponentType.Label,
            Label = "Search",
            Component = new()
            {
                Type = ComponentType.TextInput,
                CustomId = "search",
                Required = false,
                Style = ButtonStyle.Secondary,
                Value = opts.Search ?? ""
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = $"Show Only {entity}s With:",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "filter_with",
                Required = false,
                MaxValues = (uint?)opts.WithFilter.Count,
                Options = [
                    .. opts.WithFilter.AsEnumerable().Select(static field =>
                    new MessageComponentOption()
                    {
                        Label = field.Key.ToLabel(),
                        Value = field.Key.ToString(),
                        Default = field.Value
                    }
                )],
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = $"Show Only {entity}s Without:",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "filter_without",
                Required = false,
                MaxValues = (uint?)opts.WithOutFilter.Count,
                Options = [
                    .. opts.WithOutFilter.AsEnumerable().Select(static field =>
                    new MessageComponentOption()
                    {
                        Label = field.Key.ToLabel(),
                        Value = field.Key.ToString(),
                        Default = field.Value
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
                CustomId = "extra_checkboxes_filter",
                Required = false,
                Options = [
                    new()
                    {
                        Label = "Search Description",
                        Value = "description",
                        Default = opts.SearchDescription
                    }
                ]
            }
        }
    ]).Where(c => c != null).ToArray();

    public static MessageComponent[] LongDisplayOptionsModal(ListOptions opts, bool isOwner) => ((IEnumerable<MessageComponent>)[
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
        isOwner ? new()
        {
            Type = ComponentType.Label,
            Label = "Show What Information (not which members):",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "info_privacy_filter",
                Options = [
                    new()
                    {
                        Label = "Public Information Only",
                        Value = "public",
                        Default = opts.Context == LookupContext.ByNonOwner
                    },
                    new()
                    {
                        Label = "All Information",
                        Value = "all",
                        Default = opts.Context == LookupContext.ByOwner
                    }
                ]
            }
        } : null,
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
            Label = "Extra Options",
            Component = new()
            {
                Type = ComponentType.CheckboxGroup,
                CustomId = "extra_checkboxes_display",
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
    ]).Where(c => c != null).ToArray();

    public static MessageComponent[] LongFilterOptionsModal(ListOptions opts, bool isOwner, ListEntity entity) => ((IEnumerable<MessageComponent>)[
        isOwner ? new()
        {
            Type = ComponentType.Label,
            Label = $"Show Which {entity}s (not what info):",
            Component = new()
            {
                Type = ComponentType.StringSelect,
                CustomId = "entity_privacy_filter",
                Options = [
                    new()
                    {
                        Label = $"Public {entity}s Only",
                        Value = "public",
                        Default = opts.PrivacyFilter == PrivacyLevel.Public
                    },
                    new()
                    {
                        Label = $"Private {entity}s Only",
                        Value = "private",
                        Default = opts.PrivacyFilter == PrivacyLevel.Private
                    },
                    new()
                    {
                        Label = $"All {entity}s",
                        Value = "all",
                        Default = opts.PrivacyFilter == null
                    }
                ]
            }
        } : null,
        new()
        {
            Type = ComponentType.Label,
            Label = "Search",
            Component = new()
            {
                Type = ComponentType.TextInput,
                CustomId = "search",
                Required = false,
                Style = ButtonStyle.Secondary,
                Value = opts.Search ?? ""
            }
        },
        new()
        {
            Type = ComponentType.Label,
            Label = $"Show Only {entity}s With:",
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
            Label = $"Show Only {entity}s Without:",
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
                CustomId = "extra_checkboxes_filter",
                Required = false,
                Options = [
                    new()
                    {
                        Label = "Search Description",
                        Value = "description",
                        Default = opts.SearchDescription
                    }
                ]
            }
        }
    ]).Where(c => c != null).ToArray();


    public static async Task RenderCV2MemberList(this Context ctx, SystemId system, string title, string color,
                                ListOptions opts, EmbedService embeds)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var members = await ctx.Database.Execute(conn => conn.QueryMemberList(system, opts.GroupFilter, opts.Type == ListType.Long));

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

        var footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(members.Count())}.";

        (string, Func<MessageComponent[]>)[] memberShortOptionsModals = [
            ("Display Options", () => ShortDisplayOptionsModal(opts, ctx.System?.Id == system)),
            ("Filter Options", () => ShortFilterOptionsModal(opts, ctx.System?.Id == system, ListEntity.Member))];
        (string, Func<MessageComponent[]>)[] memberLongOptionsModals = [
            ("Display Options", () => LongDisplayOptionsModal(opts, ctx.System?.Id == system)),
            ("Filter Options", () => LongFilterOptionsModal(opts, ctx.System?.Id == system, ListEntity.Member))];

        var itemsPerPage = 25;
        (string, Func<MessageComponent[]>)[] OptionsModals = opts.Type == ListType.Short ? memberShortOptionsModals : memberLongOptionsModals;
        await ctx.PaginateCV2(title, color, Renderer, itemsPerPage, OptionsModals);

        // Base renderer, dispatches based on type
        (string, List<ContextUtils.PageItem>) Renderer(ApplicationCommandInteractionData? optsOverride)
        {
            if (optsOverride != null)
                ParseListOptionUpdates(opts, optsOverride);

            var filteredMembers = members.SortAndFilterByListOptions(opts);

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

                    if (opts.IncludedFields[ListEntityField.MessageCount] && m.MessageCountFor(opts.Context) is { } count)
                        ret += $"({"message".ToQuantity(count, "n0")})";
                    else if (opts.IncludedFields[ListEntityField.LastSwitch] && m.MetadataPrivacy.TryGet(opts.Context, m.LastSwitchTime, out var lastSw))
                        ret += $"(last switched in: <t:{lastSw.Value.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludedFields[ListEntityField.LastMessage] && m.MetadataPrivacy.TryGet(opts.Context, m.LastMessageTimestamp, out var lastMsg))
                        ret += $"(last message: <t:{m.LastMessageTimestamp.Value.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludedFields[ListEntityField.CreationDate] && m.MetadataPrivacy.TryGet(opts.Context, m.Created, out var created))
                        ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                    else if (opts.IncludedFields[ListEntityField.Avatar] && m.AvatarFor(opts.Context) is { } avatarUrl)
                        ret += $"([avatar URL]({avatarUrl}))";
                    else if (opts.IncludedFields[ListEntityField.Pronouns] && m.PronounsFor(opts.Context) is { } pronouns)
                        ret += $"({pronouns})";
                    else if (opts.IncludedFields[ListEntityField.DisplayName] && m.DisplayName != null && m.NamePrivacy.CanAccess(opts.Context))
                        ret += $"({m.DisplayName})";
                    else if (opts.IncludedFields[ListEntityField.Birthdate] && m.BirthdayFor(opts.Context) is { } birthday)
                        ret += $"(birthday: {m.BirthdayString})";
                    else if (opts.IncludedFields[ListEntityField.ProxyTags] && m.HasProxyTags && m.ProxyPrivacy.CanAccess(opts.Context))
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
                var container = embeds.CreateMemberMessageComponents(null, m, null, ctx.Config, opts.Context, m.Groups.ToList(), opts, true);
                return new ContextUtils.PageItem()
                {
                    CharacterCount = MiscUtils.CountCharacters(container),
                    Components = container
                };
            });

    }

    public static async Task RenderMemberList(this Context ctx, SystemId system, string embedTitle, string color, ListOptions opts)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var members = (await ctx.Database.Execute(conn => conn.QueryMemberList(system, opts.GroupFilter)))
            .SortAndFilterByListOptions(opts);

        var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;
        await ctx.Paginate(members.ToAsyncEnumerable(), members.Count(), itemsPerPage, embedTitle, color, Renderer, true);

        // Base renderer, dispatches based on type
        Task Renderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
        {
            // Add a global footer with the filter/sort string + result count
            eb.Footer(new Embed.EmbedFooter($"{opts.CreateFilterString()} {"result".ToQuantity(members.Count())}."));

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

                if (opts.IncludedFields[ListEntityField.MessageCount] && m.MessageCountFor(opts.Context) is { } count)
                    ret += $"({"message".ToQuantity(count, "n0")})";
                else if (opts.IncludedFields[ListEntityField.LastSwitch] && m.MetadataPrivacy.TryGet(opts.Context, m.LastSwitchTime, out var lastSw))
                    ret += $"(last switched in: <t:{lastSw.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.LastMessage] && m.MetadataPrivacy.TryGet(opts.Context, m.LastMessageTimestamp, out var lastMsg))
                    ret += $"(last message: <t:{m.LastMessageTimestamp.Value.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.CreationDate] && m.MetadataPrivacy.TryGet(opts.Context, m.Created, out var created))
                    ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.Avatar] && m.AvatarFor(opts.Context) is { } avatarUrl)
                    ret += $"([avatar URL]({avatarUrl}))";
                else if (opts.IncludedFields[ListEntityField.Pronouns] && m.PronounsFor(opts.Context) is { } pronouns)
                    ret += $"({pronouns})";
                else if (opts.IncludedFields[ListEntityField.DisplayName] && m.DisplayName != null && m.NamePrivacy.CanAccess(opts.Context))
                    ret += $"({m.DisplayName})";
                else if (opts.IncludedFields[ListEntityField.Birthdate] && m.BirthdayFor(opts.Context) is { } birthday)
                    ret += $"(birthday: {m.BirthdayString})";
                else if (m.HasProxyTags && m.ProxyPrivacy.CanAccess(opts.Context))
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

                if (m.DisplayName != null && m.NamePrivacy.CanAccess(opts.Context))
                    profile.Append($"\n**Display name**: {m.DisplayName}");

                if (m.PronounsFor(opts.Context) is { } pronouns)
                    profile.Append($"\n**Pronouns**: {pronouns}");

                if (m.BirthdayFor(opts.Context) != null)
                    profile.Append($"\n**Birthdate**: {m.BirthdayString}");

                if (m.ProxyTags.Count > 0 && m.ProxyPrivacy.CanAccess(opts.Context))
                    profile.Append($"\n**Proxy tags**: {m.ProxyTagsString()}");

                if ((opts.IncludedFields[ListEntityField.MessageCount] || opts.SortProperty == ListEntityField.MessageCount) &&
                    m.MessageCountFor(opts.Context) is { } count && m.MessageCount > 0)
                    profile.Append($"\n**Message count:** {count:n0}");

                if ((opts.IncludedFields[ListEntityField.LastMessage] || opts.SortProperty == ListEntityField.LastMessage) && m.MetadataPrivacy.TryGet(opts.Context, m.LastMessageTimestamp, out var lastMsg))
                    profile.Append($"\n**Last message:** {m.LastMessageTimestamp.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludedFields[ListEntityField.LastSwitch] || opts.SortProperty == ListEntityField.LastSwitch) &&
                    m.MetadataPrivacy.TryGet(opts.Context, m.LastSwitchTime, out var lastSw))
                    profile.Append($"\n**Last switched in:** {lastSw.Value.FormatZoned(ctx.Zone)}");

                if ((opts.IncludedFields[ListEntityField.CreationDate] || opts.SortProperty == ListEntityField.CreationDate) &&
                    m.MetadataPrivacy.TryGet(opts.Context, m.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludedFields[ListEntityField.Avatar] && m.AvatarFor(opts.Context) is { } avatar)
                    profile.Append($"\n**Avatar URL:** {avatar.TryGetCleanCdnUrl()}");

                if (m.DescriptionFor(opts.Context) is { } desc)
                    profile.Append($"\n\n{desc}");

                if (m.MemberVisibility == PrivacyLevel.Private)
                    profile.Append("\n*(this member is hidden)*");

                eb.Field(new Embed.Field(m.NameFor(ctx), profile.ToString().Truncate(1024)));
            }
        }
    }

    public static async Task RenderCV2GroupList(this Context ctx, SystemId system, string title, string color,
                                ListOptions opts, EmbedService embeds)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var groups = await ctx.Database.Execute(conn => conn.QueryGroupList(system, opts.MemberFilter, true));

        // Make the defaults match the behavior of the non-cv2 list
        if (opts.IncludedCount == 0 || opts.Type == ListType.Long)
            opts.IncludedFields[ListEntityField.MemberCount] = true;
        if (opts.Type == ListType.Long)
        {
            opts.IncludedFields[ListEntityField.DisplayName] = true;
            opts.IncludedFields[ListEntityField.MemberCount] = true;
            opts.IncludedFields[ListEntityField.Description] = true;
        }

        var footer = $"-# {opts.CreateFilterString()} {"result".ToQuantity(groups.Count())}.";
        var itemsPerPage = 25;

        (string, Func<MessageComponent[]>)[] groupShortOptionsModals = [
            ("Display Options", () => ShortDisplayOptionsModal(opts, ctx.System?.Id == system)),
            ("Filter Options", () => ShortFilterOptionsModal(opts, ctx.System?.Id == system, ListEntity.Group))];
        (string, Func<MessageComponent[]>)[] groupLongOptionsModals = [
            ("Display Options", () => LongDisplayOptionsModal(opts, ctx.System?.Id == system)),
            ("Filter Options", () => LongFilterOptionsModal(opts, ctx.System?.Id == system, ListEntity.Group))];
        (string, Func<MessageComponent[]>)[] optionsModals = opts.Type == ListType.Short ? groupShortOptionsModals : groupLongOptionsModals;

        await ctx.PaginateCV2(title, color, Renderer, itemsPerPage, optionsModals);

        // Base renderer, dispatches based on type
        (string, List<ContextUtils.PageItem>) Renderer(ApplicationCommandInteractionData? optsOverride)
        {
            if (optsOverride != null)
                ParseListOptionUpdates(opts, optsOverride);

            var filteredGroups = groups.SortAndFilterByListOptions(opts);

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

                if (opts.IncludedFields[ListEntityField.DisplayName] && g.DisplayName != null && g.NamePrivacy.CanAccess(opts.Context))
                    ret += $"({g.DisplayName})";
                else if (opts.IncludedFields[ListEntityField.CreationDate] &&
                         g.MetadataPrivacy.TryGet(opts.Context, g.Created, out var created))
                    ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                else if (opts.IncludedFields[ListEntityField.Avatar] && g.IconFor(opts.Context) is { } avatarUrl)
                    ret += $"([avatar URL]({avatarUrl}))";
                else if (opts.IncludedFields[ListEntityField.MemberCount] && g.ListPrivacy.CanAccess(opts.Context))
                    // -priv/-pub and listprivacy affects whether count is shown
                    // -all and visibility affects what the count is
                    ret += $"({"member".ToQuantity(g.MemberCount(opts.PrivacyFilter), "n0")})";
                else if (opts.IncludedFields[ListEntityField.MessageCount] && g.ListPrivacy.CanAccess(opts.Context))
                    ret += $"({"message".ToQuantity(g.MessageCount(opts.Context, opts.PrivacyFilter), "n0")})";

                return new ContextUtils.PageItem() { CharacterCount = ret.Length, Text = ret };
            });
        }

        IEnumerable<ContextUtils.PageItem> LongRenderer(IEnumerable<ListedGroup> page) =>
            page.Select(g =>
                {
                    var components = embeds.CreateGroupMessageComponents(ctx, null, g, g.MemberCount(opts.PrivacyFilter), opts.Context, null, opts, true);
                    return new ContextUtils.PageItem()
                    {
                        CharacterCount = MiscUtils.CountCharacters(components),
                        Components = components
                    };
                });

    }

    public static async Task RenderGroupList(this Context ctx, SystemId system, string embedTitle, string color, ListOptions opts)
    {
        // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
        // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
        var groups = (await ctx.Database.Execute(conn => conn.QueryGroupList(system, opts.MemberFilter)))
            .SortAndFilterByListOptions(opts);

        var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;
        await ctx.Paginate(groups.ToAsyncEnumerable(), groups.Count(), itemsPerPage, embedTitle, color, Renderer, true);

        // Base renderer, dispatches based on type
        Task Renderer(EmbedBuilder eb, IEnumerable<ListedGroup> page)
        {
            // Add a global footer with the filter/sort string + result count
            eb.Footer(new Embed.EmbedFooter($"{opts.CreateFilterString()} {"result".ToQuantity(groups.Count())}."));

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

                switch (opts.SortProperty.value)
                {
                    case ListEntityField.DisplayName:
                        {
                            if (g.NamePrivacy.CanAccess(opts.Context) && g.DisplayName != null)
                                ret += $"({g.DisplayName})";
                            break;
                        }
                    case ListEntityField.CreationDate:
                        {
                            if (g.MetadataPrivacy.TryGet(opts.Context, g.Created, out var created))
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            break;
                        }
                    default:
                        {
                            if (opts.IncludedFields[ListEntityField.CreationDate] &&
                                     g.MetadataPrivacy.TryGet(opts.Context, g.Created, out var created))
                            {
                                ret += $"(created at <t:{created.ToUnixTimeSeconds()}>)";
                            }
                            else if (opts.IncludedFields[ListEntityField.DisplayName] && g.DisplayName != null && g.NamePrivacy.CanAccess(opts.Context))
                            {
                                ret += $"({g.DisplayName})";
                            }
                            else if (opts.IncludedFields[ListEntityField.Avatar] && g.IconFor(opts.Context) is { } avatarUrl)
                            {
                                ret += $"([avatar URL]({avatarUrl}))";
                            }
                            else
                            {
                                // -priv/-pub and listprivacy affects whether count is shown
                                // -all and visibility affects what the count is
                                if (g.ListPrivacy.CanAccess(opts.Context))
                                    ret += $"({"member".ToQuantity(g.MemberCount(opts.PrivacyFilter))})";
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

                if (g.DisplayName != null && g.NamePrivacy.CanAccess(opts.Context))
                    profile.Append($"\n**Display name**: {g.DisplayName}");

                if (g.ListPrivacy.CanAccess(opts.Context))
                    profile.Append($"\n**Member Count:** {g.MemberCount(opts.PrivacyFilter)}");

                if ((opts.IncludedFields[ListEntityField.CreationDate] || opts.SortProperty == ListEntityField.CreationDate) &&
                    g.MetadataPrivacy.TryGet(opts.Context, g.Created, out var created))
                    profile.Append($"\n**Created on:** {created.FormatZoned(ctx.Zone)}");

                if (opts.IncludedFields[ListEntityField.Avatar] && g.IconFor(opts.Context) is { } avatar)
                    profile.Append($"\n**Avatar URL:** {avatar.TryGetCleanCdnUrl()}");

                if (g.DescriptionFor(opts.Context) is { } desc)
                    profile.Append($"\n\n{desc}");

                if (g.Visibility == PrivacyLevel.Private)
                    profile.Append("\n*(this group is hidden)*");

                eb.Field(new Embed.Field(g.NameFor(ctx), profile.ToString().Truncate(1024)));
            }
        }
    }
}
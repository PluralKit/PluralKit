using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Humanizer;

using Myriad.Builders;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class ContextListExt
    {
        public static MemberListOptions ParseMemberListOptions(this Context ctx, LookupContext lookupCtx)
        {
            var p = new MemberListOptions();
            
            // Short or long list? (parse this first, as it can potentially take a positional argument)
            var isFull = ctx.Match("f", "full", "big", "details", "long") || ctx.MatchFlag("f", "full");
            p.Type = isFull ? ListType.Long : ListType.Short;
            
            // Search query
            if (ctx.HasNext())
                p.Search = ctx.RemainderOrNull();
            
            // Include description in search?
            if (ctx.MatchFlag("search-description", "filter-description", "in-description", "sd", "description", "desc"))
                p.SearchDescription = true;

            // Sort property (default is by name, but adding a flag anyway, 'cause why not)
            if (ctx.MatchFlag("by-name", "bn")) p.SortProperty = SortProperty.Name;
            if (ctx.MatchFlag("by-display-name", "bdn")) p.SortProperty = SortProperty.DisplayName;
            if (ctx.MatchFlag("by-id", "bid")) p.SortProperty = SortProperty.Hid;
            if (ctx.MatchFlag("by-message-count", "bmc")) p.SortProperty = SortProperty.MessageCount;
            if (ctx.MatchFlag("by-created", "bc", "bcd")) p.SortProperty = SortProperty.CreationDate;
            if (ctx.MatchFlag("by-last-fronted", "by-last-front", "by-last-switch", "blf", "bls")) p.SortProperty = SortProperty.LastSwitch;
            if (ctx.MatchFlag("by-last-message", "blm", "blp")) p.SortProperty = SortProperty.LastMessage;
            if (ctx.MatchFlag("by-birthday", "by-birthdate", "bbd")) p.SortProperty = SortProperty.Birthdate;
            if (ctx.MatchFlag("random")) p.SortProperty = SortProperty.Random;

            // Sort reverse?
            if (ctx.MatchFlag("r", "rev", "reverse"))
                p.Reverse = true;

            // Privacy filter (default is public only)
            if (ctx.MatchFlag("a", "all")) p.PrivacyFilter = null; 
            if (ctx.MatchFlag("private-only", "private", "priv")) p.PrivacyFilter = PrivacyLevel.Private;
            if (ctx.MatchFlag("public-only", "public", "pub")) p.PrivacyFilter = PrivacyLevel.Public;

            // PERM CHECK: If we're trying to access non-public members of another system, error
            if (p.PrivacyFilter != PrivacyLevel.Public && lookupCtx != LookupContext.ByOwner)
                // TODO: should this just return null instead of throwing or something? >.>
                throw new PKError("You cannot look up private members of another system.");
            
            // Additional fields to include in the search results
            if (ctx.MatchFlag("with-last-switch", "with-last-fronted", "with-last-front", "wls", "wlf"))
                p.IncludeLastSwitch = true;
            if (ctx.MatchFlag("with-last-message", "with-last-proxy", "wlm", "wlp"))
                p.IncludeLastMessage = true;
            if (ctx.MatchFlag("with-message-count", "wmc"))
                p.IncludeMessageCount = true;
            if (ctx.MatchFlag("with-created", "wc"))
                p.IncludeCreated = true;
            if (ctx.MatchFlag("with-avatar", "with-image", "wa", "wi", "ia", "ii", "img"))
                p.IncludeAvatar = true;
            if (ctx.MatchFlag("with-pronouns", "wp"))
                p.IncludePronouns = true;
            
            // Always show the sort property, too
            if (p.SortProperty == SortProperty.LastSwitch) p.IncludeLastSwitch = true;
            if (p.SortProperty == SortProperty.LastMessage) p.IncludeLastMessage= true;
            if (p.SortProperty == SortProperty.MessageCount) p.IncludeMessageCount = true;
            if (p.SortProperty == SortProperty.CreationDate) p.IncludeCreated = true;
            
            // Done!
            return p;
        }

        public static async Task RenderMemberList(this Context ctx, LookupContext lookupCtx, IDatabase db, SystemId system, string embedTitle, string color, MemberListOptions opts)
        {
            // We take an IDatabase instead of a IPKConnection so we don't keep the handle open for the entire runtime
            // We wanna release it as soon as the member list is actually *fetched*, instead of potentially minutes later (paginate timeout)
            var members = (await db.Execute(conn => conn.QueryMemberList(system, opts.ToQueryOptions())))
                .SortByMemberListOptions(opts, lookupCtx)
                .ToList();

            var itemsPerPage = opts.Type == ListType.Short ? 25 : 5;
            await ctx.Paginate(members.ToAsyncEnumerable(), members.Count, itemsPerPage, embedTitle, color, Renderer);

            // Base renderer, dispatches based on type
            Task Renderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
            {
                // Add a global footer with the filter/sort string + result count
                eb.Footer(new($"{opts.CreateFilterString()}. {"result".ToQuantity(members.Count)}."));
                
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

                    switch (opts.SortProperty) {
                        case SortProperty.Birthdate: {
                            if (m.Birthday.HasValue) ret += $"(birthday: {m.BirthdayString})";
                            break;
                        }
                        case SortProperty.MessageCount: {
                            ret += $"({m.MessageCount} messages)";
                            break;
                        }
                        case SortProperty.LastSwitch: {
                            if (m.LastSwitchTime != null)
                                ret += $"(last switched in: {m.LastSwitchTime.Value.FormatZoned(ctx.System)})";
                            break;
                        }
                        case SortProperty.LastMessage: {
                            if (m.LastMessage != null)
                                ret += $"(last message: {DiscordUtils.SnowflakeToInstant(m.LastMessage.Value).FormatZoned(ctx.System)})";
                            break;
                        }
                        case SortProperty.CreationDate: {
                            ret += $"(created at {m.Created.FormatZoned(ctx.System)})";
                            break;
                        }
                        default: {
                            if (opts.IncludeMessageCount)
                                ret += $"({m.MessageCount} messages)";
                            else if (opts.IncludeLastSwitch)
                                ret += $"(last switched in: {m.LastSwitchTime?.FormatZoned(ctx.System)})";
                            else if (opts.IncludeLastMessage && m.LastMessage != null)
                                ret += $"(last message: {DiscordUtils.SnowflakeToInstant(m.LastMessage.Value).FormatZoned(ctx.System)})";
                            else if (opts.IncludeCreated)
                                ret += $"(created at {m.Created.FormatZoned(ctx.System)})";
                            else if (opts.IncludePronouns) {
                                if (m.Pronouns != null)
                                    ret += $"({m.Pronouns})";
                                break;
                            }
                            else if (m.HasProxyTags)
                            {
                                var proxyTagsString = m.ProxyTagsString();
                                if (proxyTagsString.Length > 100) // arbitrary threshold for now, tweak?
                                    proxyTagsString = "tags too long, see member card";
                                ret += $"*(*{proxyTagsString}*)*";
                            }
                            break;
                        }
                    }
                    return ret;
                }));
            }
            
            void LongRenderer(EmbedBuilder eb, IEnumerable<ListedMember> page)
            {
                var zone = ctx.System?.Zone ?? DateTimeZone.Utc;
                foreach (var m in page)
                {
                    var profile = new StringBuilder($"**ID**: {m.Hid}");
                    
                    if (m.DisplayName != null && m.NamePrivacy.CanAccess(lookupCtx))
                        profile.Append($"\n**Display name**: {m.DisplayName}");
                    
                    if (m.PronounsFor(lookupCtx) is {} pronouns)
                        profile.Append($"\n**Pronouns**: {pronouns}");
                    
                    if (m.BirthdayFor(lookupCtx) != null) 
                        profile.Append($"\n**Birthdate**: {m.BirthdayString}");
                    
                    if (m.ProxyTags.Count > 0) 
                        profile.Append($"\n**Proxy tags**: {m.ProxyTagsString()}");
                    
                    if ((opts.IncludeMessageCount || opts.SortProperty == SortProperty.MessageCount) && m.MessageCountFor(lookupCtx) is {} count && count > 0)
                        profile.Append($"\n**Message count:** {count}");
                    
                    if ((opts.IncludeLastMessage || opts.SortProperty == SortProperty.LastMessage) && m.MetadataPrivacy.TryGet(lookupCtx, m.LastMessage, out var lastMsg)) 
                        profile.Append($"\n**Last message:** {DiscordUtils.SnowflakeToInstant(lastMsg.Value).FormatZoned(zone)}");
                    
                    if ((opts.IncludeLastSwitch || opts.SortProperty == SortProperty.LastSwitch) && m.MetadataPrivacy.TryGet(lookupCtx, m.LastSwitchTime, out var lastSw)) 
                        profile.Append($"\n**Last switched in:** {lastSw.Value.FormatZoned(zone)}");

                    if ((opts.IncludeCreated || opts.SortProperty == SortProperty.CreationDate) && m.MetadataPrivacy.TryGet(lookupCtx, m.Created, out var created))
                        profile.Append($"\n**Created on:** {created.FormatZoned(zone)}");
                    
                    if (opts.IncludeAvatar && m.AvatarFor(lookupCtx) is {} avatar)
                        profile.Append($"\n**Avatar URL:** {avatar}");

                    if (m.DescriptionFor(lookupCtx) is {} desc) 
                        profile.Append($"\n\n{desc}");
                    
                    if (m.MemberVisibility == PrivacyLevel.Private)
                        profile.Append("\n*(this member is hidden)*");
                    
                    eb.Field(new(m.NameFor(ctx), profile.ToString().Truncate(1024)));
                }
            }
        }
    }
}

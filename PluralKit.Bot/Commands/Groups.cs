using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Dapper;

using Humanizer;

using Myriad.Builders;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Groups
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly EmbedService _embeds;

        public Groups(IDatabase db, ModelRepository repo, EmbedService embeds)
        {
            _db = db;
            _repo = repo;
            _embeds = embeds;
        }

        public async Task CreateGroup(Context ctx)
        {
            ctx.CheckSystem();
            
            // Check group name length
            var groupName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a group name.");
            if (groupName.Length > Limits.MaxGroupNameLength)
                throw new PKError($"Group name too long ({groupName.Length}/{Limits.MaxGroupNameLength} characters).");
            
            await using var conn = await _db.Obtain();
            
            // Check group cap
            var existingGroupCount = await conn.QuerySingleAsync<int>("select count(*) from groups where system = @System", new { System = ctx.System.Id });
            var groupLimit = ctx.System.GroupLimitOverride ?? Limits.MaxGroupCount;
            if (existingGroupCount >= groupLimit)
                throw new PKError($"System has reached the maximum number of groups ({groupLimit}). Please delete unused groups first in order to create new ones.");

            // Warn if there's already a group by this name
            var existingGroup = await _repo.GetGroupByName(conn, ctx.System.Id, groupName);
            if (existingGroup != null) {
                var msg = $"{Emojis.Warn} You already have a group in your system with the name \"{existingGroup.Name}\" (with ID `{existingGroup.Hid}`). Do you want to create another group with the same name?";
                if (!await ctx.PromptYesNo(msg))
                    throw new PKError("Group creation cancelled.");
            }
            
            var newGroup = await _repo.CreateGroup(conn, ctx.System.Id, groupName);
            
            var eb = new EmbedBuilder()
                .Description($"Your new group, **{groupName}**, has been created, with the group ID **`{newGroup.Hid}`**.\nBelow are a couple of useful commands:")
                .Field(new("View the group card", $"> pk;group **{newGroup.Reference()}**"))
                .Field(new("Add members to the group", $"> pk;group **{newGroup.Reference()}** add **MemberName**\n> pk;group **{newGroup.Reference()}** add **Member1** **Member2** **Member3** (and so on...)"))
                .Field(new("Set the description", $"> pk;group **{newGroup.Reference()}** description **This is my new group, and here is the description!**"))
                .Field(new("Set the group icon", $"> pk;group **{newGroup.Reference()}** icon\n*(with an image attached)*"));
            await ctx.Reply($"{Emojis.Success} Group created!", eb.Build());
        }

        public async Task RenameGroup(Context ctx, PKGroup target)
        {
            ctx.CheckOwnGroup(target);
            
            // Check group name length
            var newName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a new group name.");
            if (newName.Length > Limits.MaxGroupNameLength)
                throw new PKError($"New group name too long ({newName.Length}/{Limits.MaxMemberNameLength} characters).");
            
            await using var conn = await _db.Obtain();
            
            // Warn if there's already a group by this name
            var existingGroup = await _repo.GetGroupByName(conn, ctx.System.Id, newName);
            if (existingGroup != null && existingGroup.Id != target.Id) {
                var msg = $"{Emojis.Warn} You already have a group in your system with the name \"{existingGroup.Name}\" (with ID `{existingGroup.Hid}`). Do you want to rename this member to that name too?";
                if (!await ctx.PromptYesNo(msg))
                    throw new PKError("Group creation cancelled.");
            }

            await _repo.UpdateGroup(conn, target.Id, new GroupPatch {Name = newName});

            await ctx.Reply($"{Emojis.Success} Group name changed from **{target.Name}** to **{newName}**.");
        }

        public async Task GroupDisplayName(Context ctx, PKGroup target)
        {
            if (await ctx.MatchClear("this group's display name"))
            {
                ctx.CheckOwnGroup(target);
                
                var patch = new GroupPatch {DisplayName = Partial<string>.Null()};
                await _db.Execute(conn => _repo.UpdateGroup(conn, target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Group display name cleared.");
            }
            else if (!ctx.HasNext())
            {
                // No perms check, display name isn't covered by member privacy 
                var eb = new EmbedBuilder()
                    .Field(new("Name", target.Name))
                    .Field(new("Display Name", target.DisplayName ?? "*(none)*"));
                
                if (ctx.System?.Id == target.System)
                    eb.Description($"To change display name, type `pk;group {target.Reference()} displayname <display name>`.\nTo clear it, type `pk;group {target.Reference()} displayname -clear`.");
                
                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                ctx.CheckOwnGroup(target);
                
                var newDisplayName = ctx.RemainderOrNull();
                
                var patch = new GroupPatch {DisplayName = Partial<string>.Present(newDisplayName)};
                await _db.Execute(conn => _repo.UpdateGroup(conn, target.Id, patch));

                await ctx.Reply($"{Emojis.Success} Group display name changed.");
            }
        }
        
        public async Task GroupDescription(Context ctx, PKGroup target)
        {
            if (await ctx.MatchClear("this group's description"))
            {
                ctx.CheckOwnGroup(target);

                var patch = new GroupPatch {Description = Partial<string>.Null()};
                await _db.Execute(conn => _repo.UpdateGroup(conn, target.Id, patch));
                await ctx.Reply($"{Emojis.Success} Group description cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (target.Description == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This group does not have a description set. To set one, type `pk;group {target.Reference()} description <description>`.");
                    else
                        await ctx.Reply("This group does not have a description set.");
                else if (ctx.MatchFlag("r", "raw"))
                    await ctx.Reply($"```\n{target.Description}\n```");
                else
                    await ctx.Reply(embed: new EmbedBuilder()
                        .Title("Group description")
                        .Description(target.Description)
                        .Field(new("\u200B", $"To print the description with formatting, type `pk;group {target.Reference()} description -raw`." 
                                    + (ctx.System?.Id == target.System ? $" To clear it, type `pk;group {target.Reference()} description -clear`." : "")))
                        .Build());
            }
            else
            {
                ctx.CheckOwnGroup(target);

                var description = ctx.RemainderOrNull().NormalizeLineEndSpacing();
                if (description.IsLongerThan(Limits.MaxDescriptionLength))
                    throw Errors.DescriptionTooLongError(description.Length);
        
                var patch = new GroupPatch {Description = Partial<string>.Present(description)};
                await _db.Execute(conn => _repo.UpdateGroup(conn, target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Group description changed.");
            }
        }

        public async Task GroupIcon(Context ctx, PKGroup target)
        {
            async Task ClearIcon()
            {
                ctx.CheckOwnGroup(target);
                
                await _db.Execute(c => _repo.UpdateGroup(c, target.Id, new GroupPatch {Icon = null}));
                await ctx.Reply($"{Emojis.Success} Group icon cleared.");
            }

            async Task SetIcon(ParsedImage img)
            {
                ctx.CheckOwnGroup(target);
                
                await AvatarUtils.VerifyAvatarOrThrow(img.Url);

                await _db.Execute(c => _repo.UpdateGroup(c, target.Id, new GroupPatch {Icon = img.Url}));
            
                var msg = img.Source switch
                {
                    AvatarSource.User => $"{Emojis.Success} Group icon changed to {img.SourceUser?.Username}'s avatar!\n{Emojis.Warn} If {img.SourceUser?.Username} changes their avatar, the group icon will need to be re-set.",
                    AvatarSource.Url => $"{Emojis.Success} Group icon changed to the image at the given URL.",
                    AvatarSource.Attachment => $"{Emojis.Success} Group icon changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the group icon will stop working.",
                    _ => throw new ArgumentOutOfRangeException()
                };
            
                // The attachment's already right there, no need to preview it.
                var hasEmbed = img.Source != AvatarSource.Attachment;
                await (hasEmbed 
                    ? ctx.Reply(msg, embed: new EmbedBuilder().Image(new(img.Url)).Build()) 
                    : ctx.Reply(msg));
            }

            async Task ShowIcon()
            {
                if ((target.Icon?.Trim() ?? "").Length > 0)
                {
                    var eb = new EmbedBuilder()
                        .Title("Group icon")
                        .Image(new(target.Icon));
                    
                    if (target.System == ctx.System?.Id)
                    {
                        eb.Description($"To clear, use `pk;group {target.Reference()} icon -clear`.");
                    }

                    await ctx.Reply(embed: eb.Build());
                }
                else
                    throw new PKSyntaxError("This group does not have an icon set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
            }

            if (await ctx.MatchClear("this group's icon"))
                await ClearIcon();
            else if (await ctx.MatchImage() is {} img)
                await SetIcon(img);
            else
                await ShowIcon();
        }

        public async Task GroupBannerImage(Context ctx, PKGroup target)
        {
            async Task ClearBannerImage()
            {
                ctx.CheckOwnGroup(target);

                await _db.Execute(c => _repo.UpdateGroup(c, target.Id, new GroupPatch {BannerImage = null}));
                await ctx.Reply($"{Emojis.Success} Group banner image cleared.");
            }

            async Task SetBannerImage(ParsedImage img)
            {
                ctx.CheckOwnGroup(target);

                await AvatarUtils.VerifyAvatarOrThrow(img.Url, true);

                await _db.Execute(c => _repo.UpdateGroup(c, target.Id, new GroupPatch {BannerImage = img.Url}));

                var msg = img.Source switch
                {
                    AvatarSource.Url => $"{Emojis.Success} Group banner image changed to the image at the given URL.",
                    AvatarSource.Attachment => $"{Emojis.Success} Group banner image changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the banner image will stop working.",
                    _ => throw new ArgumentOutOfRangeException()
                };

                // The attachment's already right there, no need to preview it.
                var hasEmbed = img.Source != AvatarSource.Attachment;
                await (hasEmbed 
                    ? ctx.Reply(msg, embed: new EmbedBuilder().Image(new(img.Url)).Build()) 
                    : ctx.Reply(msg));
            }

            async Task ShowBannerImage()
            {
                if ((target.BannerImage?.Trim() ?? "").Length > 0)
                {
                    var eb = new EmbedBuilder()
                        .Title("Group banner image")
                        .Image(new(target.BannerImage));

                    if (target.System == ctx.System?.Id)
                    {
                        eb.Description($"To clear, use `pk;group {target.Reference()} banner clear`.");
                    }

                    await ctx.Reply(embed: eb.Build());
                }
                else
                    throw new PKSyntaxError("This group does not have a banner image set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
            }

            if (await ctx.MatchClear("this group's banner image"))
                await ClearBannerImage();
            else if (await ctx.MatchImage(true) is {} img)
                await SetBannerImage(img);
            else
                await ShowBannerImage();
        }

        public async Task GroupColor(Context ctx, PKGroup target)
        {
            var color = ctx.RemainderOrNull();
            if (await ctx.MatchClear())
            {
                ctx.CheckOwnGroup(target);
                
                var patch = new GroupPatch {Color = Partial<string>.Null()};
                await _db.Execute(conn => _repo.UpdateGroup(conn, target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Group color cleared.");
            }
            else if (!ctx.HasNext())
            {

                if (target.Color == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply(
                            $"This group does not have a color set. To set one, type `pk;group {target.Reference()} color <color>`.");
                    else
                        await ctx.Reply("This group does not have a color set.");
                else
                    await ctx.Reply(embed: new EmbedBuilder()
                        .Title("Group color")
                        .Color(target.Color.ToDiscordColor())
                        .Thumbnail(new($"https://fakeimg.pl/256x256/{target.Color}/?text=%20"))
                        .Description($"This group's color is **#{target.Color}**."
                                         + (ctx.System?.Id == target.System ? $" To clear it, type `pk;group {target.Reference()} color -clear`." : ""))
                        .Build());
            }
            else
            {
                ctx.CheckOwnGroup(target);

                if (color.StartsWith("#")) color = color.Substring(1);
                if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);
                
                var patch = new GroupPatch {Color = Partial<string>.Present(color.ToLowerInvariant())};
                await _db.Execute(conn => _repo.UpdateGroup(conn, target.Id, patch));

                await ctx.Reply(embed: new EmbedBuilder()
                    .Title($"{Emojis.Success} Group color changed.")
                    .Color(color.ToDiscordColor())
                    .Thumbnail(new($"https://fakeimg.pl/256x256/{color}/?text=%20"))
                    .Build());
            }
        }

        public async Task ListSystemGroups(Context ctx, PKSystem system)
        {
            if (system == null)
            {
                ctx.CheckSystem();
                system = ctx.System;
            }
            
            ctx.CheckSystemPrivacy(system, system.GroupListPrivacy);
            
            // TODO: integrate with the normal "search" system
            await using var conn = await _db.Obtain();

            var pctx = LookupContext.ByNonOwner;
            if (ctx.MatchFlag("a", "all"))
            {
                if (system.Id == ctx.System.Id)
                    pctx = LookupContext.ByOwner;
                else
                    throw new PKError("You do not have permission to access this information.");
            }
            
            var groups = (await conn.QueryGroupList(system.Id))
                .Where(g => g.Visibility.CanAccess(pctx))
                .OrderBy(g => g.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToList();
            
            if (groups.Count == 0)
            {
                if (system.Id == ctx.System?.Id)
                    await ctx.Reply("This system has no groups. To create one, use the command `pk;group new <name>`.");
                else
                    await ctx.Reply("This system has no groups.");
                
                return;
            }

            var title = system.Name != null ? $"Groups of {system.Name} (`{system.Hid}`)" : $"Groups of `{system.Hid}`";
            await ctx.Paginate(groups.ToAsyncEnumerable(), groups.Count, 25, title, ctx.System.Color, Renderer);
            
            Task Renderer(EmbedBuilder eb, IEnumerable<ListedGroup> page)
            {
                eb.WithSimpleLineContent(page.Select(g =>
                {
                    if (g.DisplayName != null)
                        return $"[`{g.Hid}`] **{g.Name.EscapeMarkdown()}** ({g.DisplayName.EscapeMarkdown()}) ({"member".ToQuantity(g.MemberCount)})";
                    else
                        return $"[`{g.Hid}`] **{g.Name.EscapeMarkdown()}** ({"member".ToQuantity(g.MemberCount)})";
                }));
                eb.Footer(new($"{groups.Count} total."));
                return Task.CompletedTask;
            }
        }

        public async Task ShowGroupCard(Context ctx, PKGroup target)
        {
            await using var conn = await _db.Obtain();
            var system = await GetGroupSystem(ctx, target, conn);
            await ctx.Reply(embed: await _embeds.CreateGroupEmbed(ctx, system, target));
        }

        public async Task AddRemoveMembers(Context ctx, PKGroup target, AddRemoveOperation op)
        {
            ctx.CheckOwnGroup(target);

            var members = (await ctx.ParseMemberList(ctx.System.Id))
                .Select(m => m.Id)
                .Distinct()
                .ToList();
            
            await using var conn = await _db.Obtain();
            
            var existingMembersInGroup = (await conn.QueryMemberList(target.System,
                new DatabaseViewsExt.MemberListQueryOptions {GroupFilter = target.Id}))
                .Select(m => m.Id.Value)
                .Distinct()
                .ToHashSet();
            
            List<MemberId> toAction;

            if (op == AddRemoveOperation.Add)
            {
                toAction = members
                    .Where(m => !existingMembersInGroup.Contains(m.Value))
                    .ToList();
                await _repo.AddMembersToGroup(conn, target.Id, toAction);
            }
            else if (op == AddRemoveOperation.Remove)
            {
                toAction = members
                    .Where(m => existingMembersInGroup.Contains(m.Value))
                    .ToList();
                await _repo.RemoveMembersFromGroup(conn, target.Id, toAction);
            }
            else return; // otherwise toAction "may be undefined"

            await ctx.Reply(MiscUtils.GroupAddRemoveResponse<MemberId>(members, toAction, op));
        }

        public async Task ListGroupMembers(Context ctx, PKGroup target)
        {
            await using var conn = await _db.Obtain();
            
            var targetSystem = await GetGroupSystem(ctx, target, conn);
            ctx.CheckSystemPrivacy(targetSystem, target.ListPrivacy);
            
            var opts = ctx.ParseMemberListOptions(ctx.LookupContextFor(target.System));
            opts.GroupFilter = target.Id;

            var title = new StringBuilder($"Members of {target.DisplayName ?? target.Name} (`{target.Hid}`) in ");
            if (targetSystem.Name != null) 
                title.Append($"{targetSystem.Name} (`{targetSystem.Hid}`)");
            else
                title.Append($"`{targetSystem.Hid}`");
            if (opts.Search != null) 
                title.Append($" matching **{opts.Search}**");
            
            await ctx.RenderMemberList(ctx.LookupContextFor(target.System), _db, target.System, title.ToString(), target.Color, opts);
        }

        public enum AddRemoveOperation
        {
            Add,
            Remove
        }

        public async Task GroupPrivacy(Context ctx, PKGroup target, PrivacyLevel? newValueFromCommand)
        {
            ctx.CheckSystem().CheckOwnGroup(target);
            // Display privacy settings
            if (!ctx.HasNext() && newValueFromCommand == null)
            {
                await ctx.Reply(embed: new EmbedBuilder()
                    .Title($"Current privacy settings for {target.Name}")
                    .Field(new("Description", target.DescriptionPrivacy.Explanation()) )
                    .Field(new("Icon", target.IconPrivacy.Explanation()))
                    .Field(new("Member list", target.ListPrivacy.Explanation()))
                    .Field(new("Visibility", target.Visibility.Explanation()))
                    .Description($"To edit privacy settings, use the command:\n> pk;group **{target.Reference()}** privacy **<subject>** **<level>**\n\n- `subject` is one of `description`, `icon`, `members`, `visibility`, or `all`\n- `level` is either `public` or `private`.")
                    .Build()); 
                return;
            }

            async Task SetAll(PrivacyLevel level)
            {
                await _db.Execute(c => _repo.UpdateGroup(c, target.Id, new GroupPatch().WithAllPrivacy(level)));
                
                if (level == PrivacyLevel.Private)
                    await ctx.Reply($"{Emojis.Success} All {target.Name}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see nothing on the group card.");
                else 
                    await ctx.Reply($"{Emojis.Success} All {target.Name}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see everything on the group card.");
            }

            async Task SetLevel(GroupPrivacySubject subject, PrivacyLevel level)
            {
                await _db.Execute(c => _repo.UpdateGroup(c, target.Id, new GroupPatch().WithPrivacy(subject, level)));
                
                var subjectName = subject switch
                {
                    GroupPrivacySubject.Description => "description privacy",
                    GroupPrivacySubject.Icon => "icon privacy",
                    GroupPrivacySubject.List => "member list",
                    GroupPrivacySubject.Visibility => "visibility",
                    _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
                };
                
                var explanation = (subject, level) switch
                {
                    (GroupPrivacySubject.Description, PrivacyLevel.Private) => "This group's description is now hidden from other systems.",
                    (GroupPrivacySubject.Icon, PrivacyLevel.Private) => "This group's icon is now hidden from other systems.",
                    (GroupPrivacySubject.Visibility, PrivacyLevel.Private) => "This group is now hidden from group lists and member cards.",
                    (GroupPrivacySubject.List, PrivacyLevel.Private) => "This group's member list is now hidden from other systems.",
                    
                    (GroupPrivacySubject.Description, PrivacyLevel.Public) => "This group's description is no longer hidden from other systems.",
                    (GroupPrivacySubject.Icon, PrivacyLevel.Public) => "This group's icon is no longer hidden from other systems.",
                    (GroupPrivacySubject.Visibility, PrivacyLevel.Public) => "This group is no longer hidden from group lists and member cards.",
                    (GroupPrivacySubject.List, PrivacyLevel.Public) => "This group's member list is no longer hidden from other systems.",
                    
                    _ => throw new InvalidOperationException($"Invalid subject/level tuple ({subject}, {level})")
                };
                
                await ctx.Reply($"{Emojis.Success} {target.Name}'s **{subjectName}** has been set to **{level.LevelName()}**. {explanation}");
            }

            if (ctx.Match("all") || newValueFromCommand != null)
                await SetAll(newValueFromCommand ?? ctx.PopPrivacyLevel());
            else
                await SetLevel(ctx.PopGroupPrivacySubject(), ctx.PopPrivacyLevel());
        }

        public async Task DeleteGroup(Context ctx, PKGroup target)
        {
            ctx.CheckOwnGroup(target);

            await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete this group? If so, reply to this message with the group's ID (`{target.Hid}`).\n**Note: this action is permanent.**");
            if (!await ctx.ConfirmWithReply(target.Hid))
                throw new PKError($"Group deletion cancelled. Note that you must reply with your group ID (`{target.Hid}`) *verbatim*.");

            await _db.Execute(conn => _repo.DeleteGroup(conn, target.Id));
            
            await ctx.Reply($"{Emojis.Success} Group deleted.");
        }

        private async Task<PKSystem> GetGroupSystem(Context ctx, PKGroup target, IPKConnection conn)
        {
            var system = ctx.System;
            if (system?.Id == target.System)
                return system;
            return await _repo.GetSystem(conn, target.System)!;
        }
    }
}
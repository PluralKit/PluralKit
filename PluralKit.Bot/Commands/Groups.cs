using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dapper;

using DSharpPlus.Entities;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Groups
    {
        private readonly IDatabase _db;

        public Groups(IDatabase db)
        {
            _db = db;
        }

        public async Task CreateGroup(Context ctx)
        {
            ctx.CheckSystem();
            
            var groupName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a group name.");
            if (groupName.Length > Limits.MaxGroupNameLength)
                throw new PKError($"Group name too long ({groupName.Length}/{Limits.MaxMemberNameLength} characters).");
            
            await using var conn = await _db.Obtain();

            var existingGroupCount = await conn.QuerySingleAsync<int>("select count(*) from groups where system = @System", ctx.System.Id);
            if (existingGroupCount >= Limits.MaxGroupCount)
                throw new PKError($"System has reached the maximum number of groups ({Limits.MaxGroupCount}). Please delete unused groups first in order to create new ones.");
            
            var newGroup = await conn.CreateGroup(ctx.System.Id, groupName);
            
            var eb = new DiscordEmbedBuilder()
                .WithDescription($"Your new group, **{groupName}**, has been created, with the group ID **`{newGroup.Hid}`**.\nBelow are a couple of useful commands:")
                .AddField("View the group card", $"> pk;group **{newGroup.Hid}**")
                .AddField("Add members to the group", $"> pk;group **{newGroup.Hid}** add **MemberName**\n> pk;group **{newGroup.Hid}** add **Member1** **Member2** **Member3** (and so on...)")
                .AddField("Set the description", $"> pk;group **{newGroup.Hid}** description **This is my new group, and here is the description!**")
                .AddField("Set the group icon", $"> pk;group **{newGroup.Hid}** icon\n*(with an image attached)*");
            await ctx.Reply($"{Emojis.Success} Group created!", eb.Build());
        }

        public async Task RenameGroup(Context ctx, PKGroup target)
        {
            ctx.CheckOwnGroup(target);
            
            var newName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a new group name.");
            if (newName.Length > Limits.MaxGroupNameLength)
                throw new PKError($"New group name too long ({newName.Length}/{Limits.MaxMemberNameLength} characters).");

            await using var conn = await _db.Obtain();
            await conn.UpdateGroup(target.Id, new GroupPatch {Name = newName});

            await ctx.Reply($"{Emojis.Success} Group name changed from \"**{target.Name}**\" to \"**{newName}**\".");
        }
        
        public async Task GroupDescription(Context ctx, PKGroup target)
        {
            if (ctx.MatchClear())
            {
                ctx.CheckOwnGroup(target);

                var patch = new GroupPatch {Description = Partial<string>.Null()};
                await _db.Execute(conn => conn.UpdateGroup(target.Id, patch));
                await ctx.Reply($"{Emojis.Success} Group description cleared.");
            } 
            else if (!ctx.HasNext())
            {
                if (target.Description == null)
                    if (ctx.System?.Id == target.System)
                        await ctx.Reply($"This group does not have a description set. To set one, type `pk;group {target.Hid} description <description>`.");
                    else
                        await ctx.Reply("This group does not have a description set.");
                else if (ctx.MatchFlag("r", "raw"))
                    await ctx.Reply($"```\n{target.Description}\n```");
                else
                    await ctx.Reply(embed: new DiscordEmbedBuilder()
                        .WithTitle("Group description")
                        .WithDescription(target.Description)
                        .AddField("\u200B", $"To print the description with formatting, type `pk;group {target.Hid} description -raw`." 
                                    + (ctx.System?.Id == target.System ? $" To clear it, type `pk;group {target.Hid} description -clear`." : ""))
                        .Build());
            }
            else
            {
                ctx.CheckOwnGroup(target);

                var description = ctx.RemainderOrNull().NormalizeLineEndSpacing();
                if (description.IsLongerThan(Limits.MaxDescriptionLength))
                    throw Errors.DescriptionTooLongError(description.Length);
        
                var patch = new GroupPatch {Description = Partial<string>.Present(description)};
                await _db.Execute(conn => conn.UpdateGroup(target.Id, patch));
                
                await ctx.Reply($"{Emojis.Success} Group description changed.");
            }
        }

        public async Task ListSystemGroups(Context ctx, PKSystem system)
        {
            if (system == null)
            {
                ctx.CheckSystem();
                system = ctx.System;
            }
            
            // TODO: integrate with the normal "search" system
            await using var conn = await _db.Obtain();

            var pctx = LookupContext.ByNonOwner;
            if (ctx.MatchFlag("a", "all") && system.Id == ctx.System.Id)
                pctx = LookupContext.ByOwner;

            var groups = (await conn.QueryGroupsInSystem(system.Id)).Where(g => g.Visibility.CanAccess(pctx)).ToList();
            if (groups.Count == 0)
            {
                if (system.Id == ctx.System?.Id)
                    await ctx.Reply($"This system has no groups. To create one, use the command `pk;group new <name>`.");
                else
                    await ctx.Reply($"This system has no groups.");
                return;
            }

            var title = system.Name != null ? $"Groups of {system.Name} (`{system.Hid}`)" : $"Groups of `{system.Hid}`";
            await ctx.Paginate(groups.ToAsyncEnumerable(), groups.Count, 25, title, Renderer);
            
            Task Renderer(DiscordEmbedBuilder eb, IEnumerable<PKGroup> page)
            {
                var sb = new StringBuilder();
                foreach (var g in page)
                {
                    sb.Append($"[`{g.Hid}`] **{g.Name}**\n");
                }

                eb.WithDescription(sb.ToString());
                eb.WithFooter($"{groups.Count} total");
                return Task.CompletedTask;
            }
        }

        public async Task ShowGroupCard(Context ctx, PKGroup target)
        {
            await using var conn = await _db.Obtain();
            
            var system = await GetGroupSystem(ctx, target, conn);
            var pctx = ctx.LookupContextFor(system);
            var memberCount = await conn.QueryGroupMemberCount(target.Id, PrivacyLevel.Public);

            var nameField = target.Name;
            if (system.Name != null)
                nameField = $"{nameField} ({system.Name})";

            var eb = new DiscordEmbedBuilder()
                .WithAuthor(nameField, iconUrl: DiscordUtils.WorkaroundForUrlBug(target.IconFor(pctx)))
                .WithFooter($"System ID: {system.Hid} | Group ID: {target.Hid} | Created on {target.Created.FormatZoned(system)}");

            if (memberCount == 0)
                eb.AddField("Members (0)", $"Add one with `pk;group {target.Hid} add <member>`!", true);
            else
                eb.AddField($"Members ({memberCount})", $"(see `pk;group {target.Hid} list`)", true);

            if (target.DescriptionFor(pctx) is {} desc)
                eb.AddField("Description", desc);

            if (target.IconFor(pctx) is {} icon)
                eb.WithThumbnail(icon);

            await ctx.Reply(embed: eb.Build());
        }

        public async Task AddRemoveMembers(Context ctx, PKGroup target, AddRemoveOperation op)
        {
            ctx.CheckOwnGroup(target);

            var members = await ParseMemberList(ctx);
            
            await using var conn = await _db.Obtain();
            if (op == AddRemoveOperation.Add)
            {
                await conn.AddMembersToGroup(target.Id, members.Select(m => m.Id));
                await ctx.Reply($"{Emojis.Success} Members added to group.");
            }
            else if (op == AddRemoveOperation.Remove)
            {
                await conn.RemoveMembersFromGroup(target.Id, members.Select(m => m.Id));
                await ctx.Reply($"{Emojis.Success} Members removed from group.");
            }
        }

        public async Task ListGroupMembers(Context ctx, PKGroup target)
        {
            await using var conn = await _db.Obtain();
            var targetSystem = await GetGroupSystem(ctx, target, conn);
            ctx.CheckSystemPrivacy(targetSystem, targetSystem.MemberListPrivacy);
            
            var opts = ctx.ParseMemberListOptions(ctx.LookupContextFor(target.System));
            opts.GroupFilter = target.Id;

            var title = new StringBuilder($"Members of {target.Name} (`{target.Hid}`) in ");
            if (targetSystem.Name != null) 
                title.Append($"{targetSystem.Name} (`{targetSystem.Hid}`)");
            else
                title.Append($"`{targetSystem.Hid}`");
            if (opts.Search != null) 
                title.Append($" matching **{opts.Search}**");
            
            await ctx.RenderMemberList(ctx.LookupContextFor(target.System), _db, target.System, title.ToString(), opts);
        }

        public enum AddRemoveOperation
        {
            Add,
            Remove
        }

        private static async Task<List<PKMember>> ParseMemberList(Context ctx)
        {
            // TODO: move this to a context extension and share with the switch command somewhere, after branch merge?
            
            var members = new List<PKMember>();
            while (ctx.HasNext())
            {
                var member = await ctx.MatchMember();
                if (member == null)
                    throw new PKSyntaxError(ctx.CreateMemberNotFoundError(ctx.PopArgument()));;
                if (member.System != ctx.System.Id)
                    throw new PKError($"Member **{member.Name}** (`{member.Hid}`) is not in your own system, so you can't add it to a group.");
                members.Add(member);
            }

            if (members.Count == 0)
                throw new PKSyntaxError("You must pass one or more members.");
            return members;
        }
        
        public async Task GroupPrivacy(Context ctx, PKGroup target, PrivacyLevel? newValueFromCommand)
        {
            ctx.CheckSystem().CheckOwnGroup(target);
            // Display privacy settings
            if (!ctx.HasNext() && newValueFromCommand == null)
            {
                await ctx.Reply(embed: new DiscordEmbedBuilder()
                    .WithTitle($"Current privacy settings for {target.Name}")
                    .AddField("Description", target.DescriptionPrivacy.Explanation())
                    .AddField("Icon", target.IconPrivacy.Explanation())
                    .AddField("Visibility", target.Visibility.Explanation())
                    .WithDescription("To edit privacy settings, use the command:\n`pk;group <group> privacy <subject> <level>`\n\n- `subject` is one of `description`, `icon`, `visibility`, or `all`\n- `level` is either `public` or `private`.")
                    .Build()); 
                return;
            }

            async Task SetAll(PrivacyLevel level)
            {
                await _db.Execute(c => c.UpdateGroup(target.Id, new GroupPatch().WithAllPrivacy(level)));
                
                if (level == PrivacyLevel.Private)
                    await ctx.Reply($"{Emojis.Success} All {target.Name}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see nothing on the member card.");
                else 
                    await ctx.Reply($"{Emojis.Success} All {target.Name}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see everything on the member card.");
            }

            async Task SetLevel(GroupPrivacySubject subject, PrivacyLevel level)
            {
                await _db.Execute(c => c.UpdateGroup(target.Id, new GroupPatch().WithPrivacy(subject, level)));
                
                var subjectName = subject switch
                {
                    GroupPrivacySubject.Description => "description privacy",
                    GroupPrivacySubject.Icon => "icon privacy",
                    GroupPrivacySubject.Visibility => "visibility",
                    _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
                };
                
                var explanation = (subject, level) switch
                {
                    (GroupPrivacySubject.Description, PrivacyLevel.Private) => "This group's description is now hidden from other systems.",
                    (GroupPrivacySubject.Icon, PrivacyLevel.Private) => "This group's icon is now hidden from other systems.",
                    (GroupPrivacySubject.Visibility, PrivacyLevel.Private) => "This group is now hidden from group lists and member cards.",
                    
                    (GroupPrivacySubject.Description, PrivacyLevel.Public) => "This group's description is no longer hidden from other systems.",
                    (GroupPrivacySubject.Icon, PrivacyLevel.Public) => "This group's icon is no longer hidden from other systems.",
                    (GroupPrivacySubject.Visibility, PrivacyLevel.Public) => "This group is no longer hidden from group lists and member cards.",
                    
                    _ => throw new InvalidOperationException($"Invalid subject/level tuple ({subject}, {level})")
                };
                
                await ctx.Reply($"{Emojis.Success} {target.Name}'s **{subjectName}** has been set to **{level.LevelName()}**. {explanation}");
            }

            if (ctx.Match("all") || newValueFromCommand != null)
                await SetAll(newValueFromCommand ?? ctx.PopPrivacyLevel());
            else
                await SetLevel(ctx.PopGroupPrivacySubject(), ctx.PopPrivacyLevel());
        }

        private static async Task<PKSystem> GetGroupSystem(Context ctx, PKGroup target, IPKConnection conn)
        {
            var system = ctx.System;
            if (system?.Id == target.System)
                return system;
            return await conn.QuerySystem(target.System)!;
        }
    }
}
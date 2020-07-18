using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var newGroup = await conn.CreateGroup(ctx.System.Id, groupName);

            await ctx.Reply($"{Emojis.Success} Group \"**{groupName}**\" (`{newGroup.Hid}`) registered!\nYou can now start adding members to the group like this:\n> **pk;group `{newGroup.Hid}` add `member1` `member2...`**");
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

            var groups = (await conn.QueryGroupsInSystem(system.Id)).ToList();
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
            var memberCount = await conn.QueryGroupMemberCount(target.Id, PrivacyLevel.Public);

            var nameField = target.Name;
            if (system.Name != null)
                nameField = $"{nameField} ({system.Name})";

            var eb = new DiscordEmbedBuilder()
                .WithAuthor(nameField)
                .WithFooter($"System ID: {system.Hid} | Group ID: {target.Hid} | Created on {target.Created.FormatZoned(system)}");

            if (memberCount == 0)
                eb.AddField("Members (0)", $"Add one with `pk;group {target.Hid} add <member>`!", true);
            else
                eb.AddField($"Members ({memberCount})", $"(see `pk;group {target.Hid} list`)", true);

            if (target.Description != null)
                eb.AddField("Description", target.Description);

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

        private static async Task<PKSystem> GetGroupSystem(Context ctx, PKGroup target, IPKConnection conn)
        {
            var system = ctx.System;
            if (system?.Id == target.System)
                return system;
            return await conn.QuerySystem(target.System)!;
        }
    }
}
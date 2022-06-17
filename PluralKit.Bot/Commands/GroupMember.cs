using System.Text;

using Humanizer;

using Myriad.Builders;

using PluralKit.Core;

namespace PluralKit.Bot;

public class GroupMember
{
    public async Task AddRemoveGroups(Context ctx, PKMember target, Groups.AddRemoveOperation op)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var groups = (await ctx.ParseGroupList(ctx.System.Id))
            .Select(g => g.Id)
            .Distinct()
            .ToList();

        var existingGroups = (await ctx.Repository.GetMemberGroups(target.Id).ToListAsync())
            .Select(g => g.Id)
            .Distinct()
            .ToList();

        List<GroupId> toAction;

        if (op == Groups.AddRemoveOperation.Add)
        {
            toAction = groups
                .Where(group => !existingGroups.Contains(group))
                .ToList();

            await ctx.Repository.AddGroupsToMember(target.Id, toAction);
        }
        else if (op == Groups.AddRemoveOperation.Remove)
        {
            toAction = groups
                .Where(group => existingGroups.Contains(group))
                .ToList();

            await ctx.Repository.RemoveGroupsFromMember(target.Id, toAction);
        }
        else
        {
            return; // otherwise toAction "may be unassigned"
        }

        await ctx.Reply(GroupMemberUtils.GenerateResponse(op, 1, groups.Count, toAction.Count,
            groups.Count - toAction.Count));
    }

    public async Task ListMemberGroups(Context ctx, PKMember target)
    {
        var pctx = ctx.DirectLookupContextFor(target.System);

        var groups = await ctx.Repository.GetMemberGroups(target.Id)
            .Where(g => g.Visibility.CanAccess(pctx))
            .OrderBy(g => (g.DisplayName ?? g.Name), StringComparer.InvariantCultureIgnoreCase)
            .ToListAsync();

        var description = "";
        var msg = "";

        if (groups.Count == 0)
            description = "This member has no groups.";
        else
            description = string.Join("\n", groups.Select(g => $"[`{g.Hid}`] **{g.DisplayName ?? g.Name}**"));

        if (pctx == LookupContext.ByOwner)
        {
            msg +=
                $"\n\nTo add this member to one or more groups, use `sp;m {target.Reference(ctx)} group add <group> [group 2] [group 3...]`";
            if (groups.Count > 0)
                msg +=
                    $"\nTo remove this member from one or more groups, use `sp;m {target.Reference(ctx)} group remove <group> [group 2] [group 3...]`";
        }

        await ctx.Reply(msg, new EmbedBuilder().Title($"{target.Name}'s groups").Description(description).Build());
    }

    public async Task AddRemoveMembers(Context ctx, PKGroup target, Groups.AddRemoveOperation op)
    {
        ctx.CheckOwnGroup(target);

        var members = (await ctx.ParseMemberList(ctx.System.Id))
            .Select(m => m.Id)
            .Distinct()
            .ToList();

        var existingMembersInGroup = (await ctx.Database.Execute(conn => conn.QueryMemberList(target.System,
                new DatabaseViewsExt.ListQueryOptions { GroupFilter = target.Id })))
            .Select(m => m.Id.Value)
            .Distinct()
            .ToHashSet();

        List<MemberId> toAction;

        if (op == Groups.AddRemoveOperation.Add)
        {
            toAction = members
                .Where(m => !existingMembersInGroup.Contains(m.Value))
                .ToList();
            await ctx.Repository.AddMembersToGroup(target.Id, toAction);
        }
        else if (op == Groups.AddRemoveOperation.Remove)
        {
            toAction = members
                .Where(m => existingMembersInGroup.Contains(m.Value))
                .ToList();
            await ctx.Repository.RemoveMembersFromGroup(target.Id, toAction);
        }
        else
        {
            return; // otherwise toAction "may be undefined"
        }

        await ctx.Reply(GroupMemberUtils.GenerateResponse(op, members.Count, 1, toAction.Count,
            members.Count - toAction.Count));
    }

    public async Task ListGroupMembers(Context ctx, PKGroup target)
    {
        // see global system list for explanation of how privacy settings are used here

        var targetSystem = await GetGroupSystem(ctx, target);
        ctx.CheckSystemPrivacy(targetSystem.Id, target.ListPrivacy);

        var opts = ctx.ParseListOptions(ctx.DirectLookupContextFor(target.System));
        opts.GroupFilter = target.Id;

        var title = new StringBuilder($"Members of {target.DisplayName ?? target.Name} (`{target.Hid}`) in ");
        if (targetSystem.Name != null)
            title.Append($"{targetSystem.Name} (`{targetSystem.Hid}`)");
        else
            title.Append($"`{targetSystem.Hid}`");
        if (opts.Search != null)
            title.Append($" matching **{opts.Search.Truncate(100)}**");

        await ctx.RenderMemberList(ctx.LookupContextFor(target.System), target.System, title.ToString(),
            target.Color, opts);
    }

    private async Task<PKSystem> GetGroupSystem(Context ctx, PKGroup target)
    {
        var system = ctx.System;
        if (system?.Id == target.System)
            return system;
        return await ctx.Repository.GetSystem(target.System)!;
    }
}
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
        var targetSystem = await ctx.Repository.GetSystem(target.System);
        var opts = ctx.ParseListOptions(ctx.DirectLookupContextFor(target.System));
        opts.MemberFilter = target.Id;

        var title = new StringBuilder($"Groups containing {target.Name} (`{target.Hid}`) in ");
        if (ctx.Guild != null)
        {
            var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, targetSystem.Id);
            if (guildSettings.DisplayName != null)
                title.Append($"{guildSettings.DisplayName} (`{targetSystem.Hid}`)");
            else if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.Hid}`)");
            else
                title.Append($"`{targetSystem.Hid}`");
        }
        else
        {
            if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.Hid}`)");
            else
                title.Append($"`{targetSystem.Hid}`");
        }
        if (opts.Search != null)
            title.Append($" matching **{opts.Search.Truncate(100)}**");

        await ctx.RenderGroupList(ctx.LookupContextFor(target.System), target.System, title.ToString(),
            target.Color, opts);
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
        if (ctx.Guild != null)
        {
            var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, targetSystem.Id);
            if (guildSettings.DisplayName != null)
                title.Append($"{guildSettings.DisplayName} (`{targetSystem.Hid}`)");
            else if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.Hid}`)");
            else
                title.Append($"`{targetSystem.Hid}`");
        }
        else
        {
            if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.Hid}`)");
            else
                title.Append($"`{targetSystem.Hid}`");
        }
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
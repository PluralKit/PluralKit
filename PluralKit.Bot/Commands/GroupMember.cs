using System.Text;

using Humanizer;

using Myriad.Builders;

using PluralKit.Core;

namespace PluralKit.Bot;

public class GroupMember
{
    public async Task AddRemoveGroups(Context ctx, PKMember target, List<PKGroup> _groups, Groups.AddRemoveOperation op)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var groups = _groups.FindAll(g => g.System == ctx.System.Id)
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

    public async Task ListMemberGroups(Context ctx, PKMember target, string? query, IHasListOptions flags, bool all)
    {
        var targetSystem = await ctx.Repository.GetSystem(target.System);
        var opts = flags.GetListOptions(ctx, target.System);
        opts.MemberFilter = target.Id;
        opts.Search = query;

        var title = new StringBuilder($"Groups containing {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`) in ");
        if (ctx.Guild != null)
        {
            var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, targetSystem.Id);
            if (guildSettings.DisplayName != null)
                title.Append($"{guildSettings.DisplayName} (`{targetSystem.DisplayHid(ctx.Config)}`)");
            else if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.DisplayHid(ctx.Config)}`)");
            else
                title.Append($"`{targetSystem.DisplayHid(ctx.Config)}`");
        }
        else
        {
            if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.DisplayHid(ctx.Config)}`)");
            else
                title.Append($"`{targetSystem.DisplayHid(ctx.Config)}`");
        }
        if (opts.Search != null)
            title.Append($" matching **{opts.Search.Truncate(100)}**");

        await ctx.RenderGroupList(ctx.LookupContextFor(target.System), target.System, title.ToString(),
            target.Color, opts, all);
    }

    public async Task AddRemoveMembers(Context ctx, PKGroup target, List<PKMember>? _members, Groups.AddRemoveOperation op, bool all, bool confirmYes)
    {
        ctx.CheckOwnGroup(target);

        List<MemberId> members;
        if (all)
        {
            members = (await ctx.Database.Execute(conn => conn.QueryMemberList(target.System,
                    new DatabaseViewsExt.ListQueryOptions { })))
                .Select(m => m.Id)
                .Distinct()
                .ToList();
        }
        else
        {
            if (_members == null)
                throw new PKError("Please provide a list of members to add/remove.");

            members = _members
                .FindAll(m => m.System == ctx.System.Id)
                .Select(m => m.Id)
                .Distinct()
                .ToList();
        }

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

            if (all && !await ctx.PromptYesNo($"Are you sure you want to remove all members from group {target.Reference(ctx)}?", "Empty Group", flagValue: confirmYes)) throw Errors.GenericCancelled();

            await ctx.Repository.RemoveMembersFromGroup(target.Id, toAction);
        }
        else
        {
            return; // otherwise toAction "may be undefined"
        }

        await ctx.Reply(GroupMemberUtils.GenerateResponse(op, members.Count, 1, toAction.Count,
            members.Count - toAction.Count));
    }

    public async Task ListGroupMembers(Context ctx, PKGroup target, string? query, IHasListOptions flags)
    {
        // see global system list for explanation of how privacy settings are used here

        var targetSystem = await GetGroupSystem(ctx, target);
        ctx.CheckSystemPrivacy(targetSystem.Id, target.ListPrivacy);

        var opts = flags.GetListOptions(ctx, target.System);
        opts.GroupFilter = target.Id;
        opts.Search = query;

        var title = new StringBuilder($"Members of {target.DisplayName ?? target.Name} (`{target.DisplayHid(ctx.Config)}`) in ");
        if (ctx.Guild != null)
        {
            var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, targetSystem.Id);
            if (guildSettings.DisplayName != null)
                title.Append($"{guildSettings.DisplayName} (`{targetSystem.DisplayHid(ctx.Config)}`)");
            else if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.DisplayHid(ctx.Config)}`)");
            else
                title.Append($"`{targetSystem.DisplayHid(ctx.Config)}`");
        }
        else
        {
            if (targetSystem.NameFor(ctx) != null)
                title.Append($"{targetSystem.NameFor(ctx)} (`{targetSystem.DisplayHid(ctx.Config)}`)");
            else
                title.Append($"`{targetSystem.DisplayHid(ctx.Config)}`");
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
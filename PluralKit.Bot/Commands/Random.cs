using PluralKit.Core;

namespace PluralKit.Bot;

public class Random
{
    private readonly EmbedService _embeds;

    private readonly global::System.Random randGen = new();

    public Random(EmbedService embeds)
    {
        _embeds = embeds;
    }

    // todo: get postgresql to return one random member/group instead of querying all members/groups

    public async Task Member(Context ctx, PKSystem target)
    {
        if (target == null)
            throw Errors.NoSystemError;

        ctx.CheckSystemPrivacy(target.Id, target.MemberListPrivacy);

        var members = await ctx.Repository.GetSystemMembers(target.Id).ToListAsync();

        var privacyFilter = ctx.GetPrivacyFilter(await ctx.DirectLookupContextFor(target.Id));
        if (privacyFilter != 0)
            members = members.Where(m => ((int)m.MemberVisibility & (int)privacyFilter) > 0).ToList();

        if (members == null || !members.Any())
            throw new PKError(
                ctx.System?.Id == target.Id ?
                "Your system has no members! Please create at least one member before using this command." :
                "This system has no members!");

        var randInt = randGen.Next(members.Count);
        await ctx.Reply(embed: await _embeds.CreateMemberEmbed(target, members[randInt], ctx.Guild,
            await ctx.LookupContextFor(target.Id), ctx.Zone));
    }

    public async Task Group(Context ctx, PKSystem target)
    {
        if (target == null)
            throw Errors.NoSystemError;

        ctx.CheckSystemPrivacy(target.Id, target.GroupListPrivacy);

        var groups = await ctx.Repository.GetSystemGroups(target.Id).ToListAsync();
        var privacyFilter = ctx.GetPrivacyFilter(await ctx.DirectLookupContextFor(target.Id));
        if (privacyFilter != 0)
            groups = groups.Where(g => ((int)g.Visibility & (int)privacyFilter) > 0).ToList();

        if (groups == null || !groups.Any())
            throw new PKError(
                ctx.System?.Id == target.Id ?
                    "Your system has no groups! Please create at least one group before using this command." :
                    $"This system has no groups!");

        var randInt = randGen.Next(groups.Count());
        await ctx.Reply(embed: await _embeds.CreateGroupEmbed(ctx, target, groups.ToArray()[randInt]));
    }

    public async Task GroupMember(Context ctx, PKGroup group)
    {
        ctx.CheckSystemPrivacy(group.System, group.ListPrivacy);

        var opts = ctx.ParseListOptions(await ctx.DirectLookupContextFor(group.System));
        opts.GroupFilter = group.Id;

        var members = await ctx.Database.Execute(conn => conn.QueryMemberList(group.System, opts.ToQueryOptions()));

        if (members == null || !members.Any())
            throw new PKError(
                "This group has no members!"
                + (ctx.System?.Id == group.System ? " Please add at least one member to this group before using this command." : ""));

        var privacyFilter = ctx.GetPrivacyFilter(await ctx.DirectLookupContextFor(group.System));
        if (privacyFilter != 0)
            members = members.Where(g => ((int)g.MemberVisibility & (int)privacyFilter) > 0).ToList();

        var ms = members.ToList();

        PKSystem system;
        if (ctx.System?.Id == group.System)
            system = ctx.System;
        else
            system = await ctx.Repository.GetSystem(group.System);

        var randInt = randGen.Next(ms.Count);
        await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, ms[randInt], ctx.Guild,
            await ctx.LookupContextFor(group.System), ctx.Zone));
    }
}
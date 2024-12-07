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
            throw Errors.NoSystemError(ctx.DefaultPrefix);

        ctx.CheckSystemPrivacy(target.Id, target.MemberListPrivacy);

        var members = await ctx.Repository.GetSystemMembers(target.Id).ToListAsync();

        if (!ctx.MatchFlag("all", "a"))
            members = members.Where(m => m.MemberVisibility == PrivacyLevel.Public).ToList();
        else
            ctx.CheckOwnSystem(target);

        if (members == null || !members.Any())
            throw new PKError(
                ctx.System?.Id == target.Id ?
                "Your system has no members! Please create at least one member before using this command." :
                "This system has no members!");

        var randInt = randGen.Next(members.Count);
        await ctx.Reply(embed: await _embeds.CreateMemberEmbed(target, members[randInt], ctx.Guild,
            ctx.Config, ctx.LookupContextFor(target.Id), ctx.Zone));
    }

    public async Task Group(Context ctx, PKSystem target)
    {
        if (target == null)
            throw Errors.NoSystemError(ctx.DefaultPrefix);

        ctx.CheckSystemPrivacy(target.Id, target.GroupListPrivacy);

        var groups = await ctx.Repository.GetSystemGroups(target.Id).ToListAsync();
        if (!ctx.MatchFlag("all", "a"))
            groups = groups.Where(g => g.Visibility == PrivacyLevel.Public).ToList();
        else
            ctx.CheckOwnSystem(target);

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

        var opts = ctx.ParseListOptions(ctx.DirectLookupContextFor(group.System), ctx.LookupContextFor(group.System));
        opts.GroupFilter = group.Id;

        var members = await ctx.Database.Execute(conn => conn.QueryMemberList(group.System, opts.ToQueryOptions()));

        if (members == null || !members.Any())
            throw new PKError(
                "This group has no members!"
                + (ctx.System?.Id == group.System ? " Please add at least one member to this group before using this command." : ""));

        if (!ctx.MatchFlag("all", "a"))
            members = members.Where(g => g.MemberVisibility == PrivacyLevel.Public);
        else
            ctx.CheckOwnGroup(group);


        var ms = members.ToList();

        PKSystem system;
        if (ctx.System?.Id == group.System)
            system = ctx.System;
        else
            system = await ctx.Repository.GetSystem(group.System);

        var randInt = randGen.Next(ms.Count);
        await ctx.Reply(embed: await _embeds.CreateMemberEmbed(system, ms[randInt], ctx.Guild,
            ctx.Config, ctx.LookupContextFor(group.System), ctx.Zone));
    }
}
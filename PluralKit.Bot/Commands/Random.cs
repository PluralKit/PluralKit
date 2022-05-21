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

    public async Task Member(Context ctx)
    {
        ctx.CheckSystem();

        var members = await ctx.Repository.GetSystemMembers(ctx.System.Id).ToListAsync();

        if (!ctx.MatchFlag("all", "a"))
            members = members.Where(m => m.MemberVisibility == PrivacyLevel.Public).ToList();

        if (members == null || !members.Any())
            throw new PKError(
                "Your system has no members! Please create at least one member before using this command.");

        var randInt = randGen.Next(members.Count);
        await ctx.Reply(embed: await _embeds.CreateMemberEmbed(ctx.System, members[randInt], ctx.Guild,
            ctx.LookupContextFor(ctx.System.Id), ctx.Zone));
    }

    public async Task Group(Context ctx)
    {
        ctx.CheckSystem();

        var groups = await ctx.Repository.GetSystemGroups(ctx.System.Id).ToListAsync();
        if (!ctx.MatchFlag("all", "a"))
            groups = groups.Where(g => g.Visibility == PrivacyLevel.Public).ToList();

        if (groups == null || !groups.Any())
            throw new PKError(
                "Your system has no groups! Please create at least one group before using this command.");

        var randInt = randGen.Next(groups.Count());
        await ctx.Reply(embed: await _embeds.CreateGroupEmbed(ctx, ctx.System, groups.ToArray()[randInt]));
    }

    public async Task GroupMember(Context ctx, PKGroup group)
    {
        ctx.CheckOwnGroup(group);

        var opts = ctx.ParseListOptions(ctx.DirectLookupContextFor(group.System));
        opts.GroupFilter = group.Id;

        var members = await ctx.Database.Execute(conn => conn.QueryMemberList(ctx.System.Id, opts.ToQueryOptions()));

        if (members == null || !members.Any())
            throw new PKError(
                "This group has no members! Please add at least one member to this group before using this command.");

        if (!ctx.MatchFlag("all", "a"))
            members = members.Where(g => g.MemberVisibility == PrivacyLevel.Public);

        var ms = members.ToList();

        var randInt = randGen.Next(ms.Count);
        await ctx.Reply(embed: await _embeds.CreateMemberEmbed(ctx.System, ms[randInt], ctx.Guild,
            ctx.LookupContextFor(ctx.System.Id), ctx.Zone));
    }
}
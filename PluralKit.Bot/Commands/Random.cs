using PluralKit.Core;

namespace PluralKit.Bot;

public class Random
{
    private readonly IDatabase _db;
    private readonly EmbedService _embeds;
    private readonly ModelRepository _repo;

    private readonly global::System.Random randGen = new();

    public Random(EmbedService embeds, IDatabase db, ModelRepository repo)
    {
        _embeds = embeds;
        _db = db;
        _repo = repo;
    }

    // todo: get postgresql to return one random member/group instead of querying all members/groups

    public async Task Member(Context ctx)
    {
        ctx.CheckSystem();

        var members = await _repo.GetSystemMembers(ctx.System.Id).ToListAsync();

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

        var groups = await _db.Execute(c => c.QueryGroupList(ctx.System.Id));
        if (!ctx.MatchFlag("all", "a"))
            groups = groups.Where(g => g.Visibility == PrivacyLevel.Public);

        if (groups == null || !groups.Any())
            throw new PKError(
                "Your system has no groups! Please create at least one group before using this command.");

        var randInt = randGen.Next(groups.Count());
        await ctx.Reply(embed: await _embeds.CreateGroupEmbed(ctx, ctx.System, groups.ToArray()[randInt]));
    }

    public async Task GroupMember(Context ctx, PKGroup group)
    {
        var opts = ctx.ParseMemberListOptions(ctx.DirectLookupContextFor(group.System));
        opts.GroupFilter = group.Id;

        await using var conn = await _db.Obtain();
        var members = await conn.QueryMemberList(ctx.System.Id, opts.ToQueryOptions());

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
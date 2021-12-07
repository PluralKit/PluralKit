using System.Text;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemFront
{
    private readonly IDatabase _db;
    private readonly EmbedService _embeds;
    private readonly ModelRepository _repo;

    public SystemFront(EmbedService embeds, IDatabase db, ModelRepository repo)
    {
        _embeds = embeds;
        _db = db;
        _repo = repo;
    }

    public async Task SystemFronter(Context ctx, PKSystem system)
    {
        if (system == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(system.Id, system.FrontPrivacy);

        var sw = await _repo.GetLatestSwitch(system.Id);
        if (sw == null) throw Errors.NoRegisteredSwitches;

        await ctx.Reply(embed: await _embeds.CreateFronterEmbed(sw, ctx.Zone, ctx.LookupContextFor(system.Id)));
    }

    public async Task SystemFrontHistory(Context ctx, PKSystem system)
    {
        if (system == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(system.Id, system.FrontHistoryPrivacy);

        var totalSwitches = await _repo.GetSwitchCount(system.Id);
        if (totalSwitches == 0) throw Errors.NoRegisteredSwitches;

        var sws = _repo.GetSwitches(system.Id)
            .Scan(new FrontHistoryEntry(null, null),
                (lastEntry, newSwitch) => new FrontHistoryEntry(lastEntry.ThisSwitch?.Timestamp, newSwitch));

        var embedTitle = system.Name != null
            ? $"Front history of {system.Name} (`{system.Hid}`)"
            : $"Front history of `{system.Hid}`";

        await ctx.Paginate(
            sws,
            totalSwitches,
            10,
            embedTitle,
            system.Color,
            async (builder, switches) =>
            {
                var sb = new StringBuilder();
                foreach (var entry in switches)
                {
                    var lastSw = entry.LastTime;

                    var sw = entry.ThisSwitch;

                    // Fetch member list and format

                    var members = await _db.Execute(c => _repo.GetSwitchMembers(c, sw.Id)).ToListAsync();
                    var membersStr = members.Any()
                        ? string.Join(", ", members.Select(m => m.NameFor(ctx)))
                        : "no fronter";

                    var switchSince = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;

                    // If this isn't the latest switch, we also show duration
                    string stringToAdd;
                    if (lastSw != null)
                    {
                        // Calculate the time between the last switch (that we iterated - ie. the next one on the timeline) and the current one
                        var switchDuration = lastSw.Value - sw.Timestamp;
                        stringToAdd =
                            $"**{membersStr}** ({sw.Timestamp.FormatZoned(ctx.Zone)}, {switchSince.FormatDuration()} ago, for {switchDuration.FormatDuration()})\n";
                    }
                    else
                    {
                        stringToAdd =
                            $"**{membersStr}** ({sw.Timestamp.FormatZoned(ctx.Zone)}, {switchSince.FormatDuration()} ago)\n";
                    }

                    if (sb.Length + stringToAdd.Length >= 4096)
                        break;
                    sb.Append(stringToAdd);
                }

                builder.Description(sb.ToString());
            }
        );
    }

    public async Task SystemFrontPercent(Context ctx, PKSystem system)
    {
        if (system == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(system.Id, system.FrontHistoryPrivacy);

        var totalSwitches = await _repo.GetSwitchCount(system.Id);
        if (totalSwitches == 0) throw Errors.NoRegisteredSwitches;

        var durationStr = ctx.RemainderOrNull() ?? "30d";

        // Picked the UNIX epoch as a random date
        // even though we don't store switch timestamps in UNIX time
        // I assume most people won't have switches logged previously to that (?)
        if (durationStr == "full")
            durationStr = "1970-01-01";

        var now = SystemClock.Instance.GetCurrentInstant();

        var rangeStart = DateUtils.ParseDateTime(durationStr, true, ctx.Zone);
        if (rangeStart == null) throw Errors.InvalidDateTime(durationStr);
        if (rangeStart.Value.ToInstant() > now) throw Errors.FrontPercentTimeInFuture;

        var title = new StringBuilder("Frontpercent of ");
        if (system.Name != null)
            title.Append($"{system.Name} (`{system.Hid}`)");
        else
            title.Append($"`{system.Hid}`");

        var ignoreNoFronters = ctx.MatchFlag("fo", "fronters-only");
        var showFlat = ctx.MatchFlag("flat");
        var frontpercent = await _db.Execute(c =>
            _repo.GetFrontBreakdown(c, system.Id, null, rangeStart.Value.ToInstant(), now));
        await ctx.Reply(embed: await _embeds.CreateFrontPercentEmbed(frontpercent, system, null, ctx.Zone,
            ctx.LookupContextFor(system.Id), title.ToString(), ignoreNoFronters, showFlat));
    }

    private struct FrontHistoryEntry
    {
        public readonly Instant? LastTime;
        public readonly PKSwitch ThisSwitch;

        public FrontHistoryEntry(Instant? lastTime, PKSwitch thisSwitch)
        {
            LastTime = lastTime;
            ThisSwitch = thisSwitch;
        }
    }
}
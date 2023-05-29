using System.Text;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemFront
{
    private readonly EmbedService _embeds;

    public SystemFront(EmbedService embeds)
    {
        _embeds = embeds;
    }

    public async Task SystemFronter(Context ctx, PKSystem system)
    {
        if (system == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(system.Id, system.FrontPrivacy);

        var sw = await ctx.Repository.GetLatestSwitch(system.Id);
        if (sw == null) throw Errors.NoRegisteredSwitches;

        await ctx.Reply(embed: await _embeds.CreateFronterEmbed(sw, ctx.Zone, ctx.LookupContextFor(system.Id)));
    }

    public async Task SystemFrontHistory(Context ctx, PKSystem system)
    {
        if (system == null) throw Errors.NoSystemError;
        ctx.CheckSystemPrivacy(system.Id, system.FrontHistoryPrivacy);

        var totalSwitches = await ctx.Repository.GetSwitchCount(system.Id);
        if (totalSwitches == 0) throw Errors.NoRegisteredSwitches;

        var sws = ctx.Repository.GetSwitches(system.Id)
            .Scan(new FrontHistoryEntry(null, null),
                (lastEntry, newSwitch) => new FrontHistoryEntry(lastEntry.ThisSwitch?.Timestamp, newSwitch));

        var embedTitle = system.NameFor(ctx) != null
            ? $"Front history of {system.NameFor(ctx)} (`{system.Hid}`)"
            : $"Front history of `{system.Hid}`";

        if (ctx.Guild != null)
        {
            var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, system.Id);
            if (guildSettings.DisplayName != null)
                embedTitle = $"Front history of {guildSettings.DisplayName} (`{system.Hid}`)";
        }

        var showMemberId = ctx.MatchFlag("with-id", "wid");

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

                    var members = await ctx.Database.Execute(c => ctx.Repository.GetSwitchMembers(c, sw.Id)).ToListAsync();
                    var membersStr = members.Any()
                        ? string.Join(", ", members.Select(m => $"**{m.NameFor(ctx)}**{(showMemberId ? $" (`{m.Hid}`)" : "")}"))
                        : "**no fronter**";

                    var switchSince = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;

                    // If this isn't the latest switch, we also show duration
                    string stringToAdd;
                    if (lastSw != null)
                    {
                        // Calculate the time between the last switch (that we iterated - ie. the next one on the timeline) and the current one
                        var switchDuration = lastSw.Value - sw.Timestamp;
                        stringToAdd =
                            $"{membersStr} ({sw.Timestamp.FormatZoned(ctx.Zone)}, {switchSince.FormatDuration()} ago, for {switchDuration.FormatDuration()})\n";
                    }
                    else
                    {
                        stringToAdd =
                            $"{membersStr} ({sw.Timestamp.FormatZoned(ctx.Zone)}, {switchSince.FormatDuration()} ago)\n";
                    }

                    if (sb.Length + stringToAdd.Length >= 4096)
                        break;
                    sb.Append(stringToAdd);
                }

                builder.Description(sb.ToString());
            }
        );
    }

    public async Task FrontPercent(Context ctx, PKSystem? system = null, PKGroup? group = null)
    {
        if (system == null && group == null) throw Errors.NoSystemError;
        if (system == null) system = await GetGroupSystem(ctx, group);

        ctx.CheckSystemPrivacy(system.Id, system.FrontHistoryPrivacy);

        var totalSwitches = await ctx.Repository.GetSwitchCount(system.Id);
        if (totalSwitches == 0) throw Errors.NoRegisteredSwitches;

        var ignoreNoFronters = ctx.MatchFlag("fo", "fronters-only");
        var showFlat = ctx.MatchFlag("flat");

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
        var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, system.Id);
        if (group != null)
            title.Append($"{group.NameFor(ctx)} (`{group.Hid}`)");
        else if (guildSettings.DisplayName != null)
            title.Append($"{guildSettings.DisplayName} (`{system.Hid}`)");
        else if (system.NameFor(ctx) != null)
            title.Append($"{system.NameFor(ctx)} (`{system.Hid}`)");
        else
            title.Append($"`{system.Hid}`");

        var frontpercent = await ctx.Database.Execute(c => ctx.Repository.GetFrontBreakdown(c, system.Id, group?.Id, rangeStart.Value.ToInstant(), now));
        await ctx.Reply(embed: await _embeds.CreateFrontPercentEmbed(frontpercent, system, group, ctx.Zone,
            ctx.LookupContextFor(system.Id), title.ToString(), ignoreNoFronters, showFlat));
    }

    private async Task<PKSystem> GetGroupSystem(Context ctx, PKGroup target)
    {
        var system = ctx.System;
        if (system?.Id == target.System)
            return system;
        return await ctx.Repository.GetSystem(target.System)!;
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
using System;
using System.Linq;
using System.Threading.Tasks;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SystemFront
    {
        private IDataStore _data;
        private EmbedService _embeds;

        public SystemFront(IDataStore data, EmbedService embeds)
        {
            _data = data;
            _embeds = embeds;
        }
        
        struct FrontHistoryEntry
        {
            public Instant? LastTime;
            public PKSwitch ThisSwitch;

            public FrontHistoryEntry(Instant? lastTime, PKSwitch thisSwitch)
            {
                LastTime = lastTime;
                ThisSwitch = thisSwitch;
            }
        }

        public async Task SystemFronter(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(system, system.FrontPrivacy);
            
            var sw = await _data.GetLatestSwitch(system.Id);
            if (sw == null) throw Errors.NoRegisteredSwitches;
            
            await ctx.Reply(embed: await _embeds.CreateFronterEmbed(sw, system.Zone, ctx.LookupContextFor(system)));
        }

        public async Task SystemFrontHistory(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(system, system.FrontHistoryPrivacy);

            var sws = _data.GetSwitches(system.Id)
                .Scan(new FrontHistoryEntry(null, null), (lastEntry, newSwitch) => new FrontHistoryEntry(lastEntry.ThisSwitch?.Timestamp, newSwitch));
            var totalSwitches = await _data.GetSwitchCount(system);
            if (totalSwitches == 0) throw Errors.NoRegisteredSwitches;
            
            var embedTitle = system.Name != null ? $"Front history of {system.Name} (`{system.Hid}`)" : $"Front history of `{system.Hid}`";

            await ctx.Paginate(
                sws,
                totalSwitches,
                10,
                embedTitle,
                async (builder, switches) =>
                {
                    foreach (var entry in switches)
                    {
                        var lastSw = entry.LastTime;

                        var sw = entry.ThisSwitch;
                        // Fetch member list and format
                        var members = await _data.GetSwitchMembers(sw).ToListAsync();
                        var membersStr = members.Any() ? string.Join(", ", members.Select(m => m.NameFor(ctx))) : "no fronter";

                        var switchSince = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;

                        // If this isn't the latest switch, we also show duration
                        string stringToAdd;
                        if (lastSw != null)
                        {
                            // Calculate the time between the last switch (that we iterated - ie. the next one on the timeline) and the current one
                            var switchDuration = lastSw.Value - sw.Timestamp;
                            stringToAdd =
                                $"**{membersStr}** ({sw.Timestamp.FormatZoned(system.Zone)}, {switchSince.FormatDuration()} ago, for {switchDuration.FormatDuration()})\n";
                        }
                        else
                        {
                            stringToAdd =
                                $"**{membersStr}** ({sw.Timestamp.FormatZoned(system.Zone)}, {switchSince.FormatDuration()} ago)\n";
                        }
                        try // Unfortunately the only way to test DiscordEmbedBuilder.Description max length is this
                        {
                            builder.Description += stringToAdd;
                        }
                        catch (ArgumentException)
                        {
                            break;
                        }// TODO: Make sure this works
                    }
                }
            );
        }
        
        public async Task SystemFrontPercent(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;
            ctx.CheckSystemPrivacy(system, system.FrontHistoryPrivacy);

            string durationStr = ctx.RemainderOrNull() ?? "30d";
            
            var now = SystemClock.Instance.GetCurrentInstant();

            var rangeStart = DateUtils.ParseDateTime(durationStr, true, system.Zone);
            if (rangeStart == null) throw Errors.InvalidDateTime(durationStr);
            if (rangeStart.Value.ToInstant() > now) throw Errors.FrontPercentTimeInFuture;
            
            var frontpercent = await _data.GetFrontBreakdown(system, rangeStart.Value.ToInstant(), now);
            await ctx.Reply(embed: await _embeds.CreateFrontPercentEmbed(frontpercent, system.Zone, ctx.LookupContextFor(system)));
        }
    }
}
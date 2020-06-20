using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus.Entities;

using NodaTime;
using NodaTime.TimeZones;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class Switch
    {
        private IDataStore _data;

        public Switch(IDataStore data)
        {
            _data = data;
        }

        public async Task SwitchDo(Context ctx)
        {
            ctx.CheckSystem();
            var members = new List<PKMember>();

            // Loop through all the given arguments
            while (ctx.HasNext())
            {
                // and attempt to match a member 
                var member = await ctx.MatchMember();
                if (member == null)
                    // if we can't, big error. Every member name must be valid.
                    throw new PKError(ctx.CreateMemberNotFoundError(ctx.PopArgument()));
                
                ctx.CheckOwnMember(member); // Ensure they're in our own system
                members.Add(member); // Then add to the final output list
            }

            // Finally, do the actual switch
            await DoSwitchCommand(ctx, members);
        }
        public async Task SwitchOut(Context ctx)
        {
            ctx.CheckSystem();
            
            // Switch with no members = switch-out
            await DoSwitchCommand(ctx, new PKMember[] { });
        }

        private async Task DoSwitchCommand(Context ctx, ICollection<PKMember> members)
        {
            // Make sure there are no dupes in the list
            // We do this by checking if removing duplicate member IDs results in a list of different length 
            if (members.Select(m => m.Id).Distinct().Count() != members.Count) throw Errors.DuplicateSwitchMembers;

            // Find the last switch and its members if applicable
            var lastSwitch = await _data.GetLatestSwitch(ctx.System.Id);
            if (lastSwitch != null)
            {
                var lastSwitchMembers = _data.GetSwitchMembers(lastSwitch);
                // Make sure the requested switch isn't identical to the last one
                if (await lastSwitchMembers.Select(m => m.Id).SequenceEqualAsync(members.Select(m => m.Id).ToAsyncEnumerable()))
                    throw Errors.SameSwitch(members, ctx.LookupContextFor(ctx.System));
            }

            await _data.AddSwitch(ctx.System.Id, members);

            if (members.Count == 0)
                await ctx.Reply($"{Emojis.Success} Switch-out registered.");
            else
                await ctx.Reply($"{Emojis.Success} Switch registered. Current fronter is now {string.Join(", ", members.Select(m => m.NameFor(ctx)))}.");
        }
        
        public async Task SwitchMove(Context ctx)
        {
            ctx.CheckSystem();
            
            var timeToMove = ctx.RemainderOrNull() ?? throw new PKSyntaxError("Must pass a date or time to move the switch to.");
            var tz = TzdbDateTimeZoneSource.Default.ForId(ctx.System.UiTz ?? "UTC");
            
            var result = DateUtils.ParseDateTime(timeToMove, true, tz);
            if (result == null) throw Errors.InvalidDateTime(timeToMove);
            
            var time = result.Value;
            if (time.ToInstant() > SystemClock.Instance.GetCurrentInstant()) throw Errors.SwitchTimeInFuture;

            // Fetch the last two switches for the system to do bounds checking on
            var lastTwoSwitches = await _data.GetSwitches(ctx.System.Id).Take(2).ToListAsync();
            
            // If we don't have a switch to move, don't bother
            if (lastTwoSwitches.Count == 0) throw Errors.NoRegisteredSwitches;
            
            // If there's a switch *behind* the one we move, we check to make srue we're not moving the time further back than that
            if (lastTwoSwitches.Count == 2)
            {
                if (lastTwoSwitches[1].Timestamp > time.ToInstant())
                    throw Errors.SwitchMoveBeforeSecondLast(lastTwoSwitches[1].Timestamp.InZone(tz));
            }
            
            // Now we can actually do the move, yay!
            // But, we do a prompt to confirm.
            var lastSwitchMembers = _data.GetSwitchMembers(lastTwoSwitches[0]);
            var lastSwitchMemberStr = string.Join(", ", await lastSwitchMembers.Select(m => m.NameFor(ctx)).ToListAsync());
            var lastSwitchTimeStr = DateTimeFormats.ZonedDateTimeFormat.Format(lastTwoSwitches[0].Timestamp.InZone(ctx.System.Zone));
            var lastSwitchDeltaStr = DateTimeFormats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp);
            var newSwitchTimeStr = DateTimeFormats.ZonedDateTimeFormat.Format(time);
            var newSwitchDeltaStr = DateTimeFormats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - time.ToInstant());
            
            // yeet
            var msg = await ctx.Reply($"{Emojis.Warn} This will move the latest switch ({lastSwitchMemberStr}) from {lastSwitchTimeStr} ({lastSwitchDeltaStr} ago) to {newSwitchTimeStr} ({newSwitchDeltaStr} ago). Is this OK?");
            if (!await ctx.PromptYesNo(msg)) throw Errors.SwitchMoveCancelled;
            
            // aaaand *now* we do the move
            await _data.MoveSwitch(lastTwoSwitches[0], time.ToInstant());
            await ctx.Reply($"{Emojis.Success} Switch moved.");
        }
        
        public async Task SwitchDelete(Context ctx)
        {
            ctx.CheckSystem();

            if (ctx.Match("all", "clear"))
            {
                // Subcommand: "delete all"
                var purgeMsg = await ctx.Reply($"{Emojis.Warn} This will delete *all registered switches* in your system. Are you sure you want to proceed?");
                if (!await ctx.PromptYesNo(purgeMsg))
                    throw Errors.GenericCancelled();
                await _data.DeleteAllSwitches(ctx.System);
                await ctx.Reply($"{Emojis.Success} Cleared system switches!");
                return;
            }
            
            // Fetch the last two switches for the system to do bounds checking on
            var lastTwoSwitches = await _data.GetSwitches(ctx.System.Id).Take(2).ToListAsync();
            if (lastTwoSwitches.Count == 0) throw Errors.NoRegisteredSwitches;

            var lastSwitchMembers = _data.GetSwitchMembers(lastTwoSwitches[0]);
            var lastSwitchMemberStr = string.Join(", ", await lastSwitchMembers.Select(m => m.NameFor(ctx)).ToListAsync());
            var lastSwitchDeltaStr = DateTimeFormats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp);

            DiscordMessage msg;
            if (lastTwoSwitches.Count == 1)
            {
                msg = await ctx.Reply(
                    $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr}, {lastSwitchDeltaStr} ago). You have no other switches logged. Is this okay?");
            }
            else
            {
                var secondSwitchMembers = _data.GetSwitchMembers(lastTwoSwitches[1]);
                var secondSwitchMemberStr = string.Join(", ", await secondSwitchMembers.Select(m => m.NameFor(ctx)).ToListAsync());
                var secondSwitchDeltaStr = DateTimeFormats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[1].Timestamp);
                msg = await ctx.Reply(
                    $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr}, {lastSwitchDeltaStr} ago). The next latest switch is {secondSwitchMemberStr} ({secondSwitchDeltaStr} ago). Is this okay?");
            }

            if (!await ctx.PromptYesNo(msg)) throw Errors.SwitchDeleteCancelled;
            await _data.DeleteSwitch(lastTwoSwitches[0]);
            
            await ctx.Reply($"{Emojis.Success} Switch deleted.");
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;

using NodaTime;
using NodaTime.TimeZones;

using PluralKit.Bot.CommandSystem;

namespace PluralKit.Bot.Commands
{
    public class SwitchCommands
    {
        private IDataStore _data;

        public SwitchCommands(IDataStore data)
        {
            _data = data;
        }

        public async Task Switch(Context ctx)
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
            var lastSwitch = await _data.GetLatestSwitch(ctx.System);
            if (lastSwitch != null)
            {
                var lastSwitchMembers = await _data.GetSwitchMembers(lastSwitch);
                // Make sure the requested switch isn't identical to the last one
                if (lastSwitchMembers.Select(m => m.Id).SequenceEqual(members.Select(m => m.Id)))
                    throw Errors.SameSwitch(members);
            }

            await _data.AddSwitch(ctx.System, members);

            if (members.Count == 0)
                await ctx.Reply($"{Emojis.Success} Switch-out registered.");
            else
                await ctx.Reply($"{Emojis.Success} Switch registered. Current fronter is now {string.Join(", ", members.Select(m => m.Name)).SanitizeMentions()}.");
        }
        
        public async Task SwitchMove(Context ctx)
        {
            ctx.CheckSystem();
            
            var timeToMove = ctx.RemainderOrNull() ?? throw new PKSyntaxError("Must pass a date or time to move the switch to.");
            var tz = TzdbDateTimeZoneSource.Default.ForId(ctx.System.UiTz ?? "UTC");
            
            var result = PluralKit.Utils.ParseDateTime(timeToMove, true, tz);
            if (result == null) throw Errors.InvalidDateTime(timeToMove);
            
            var time = result.Value;
            if (time.ToInstant() > SystemClock.Instance.GetCurrentInstant()) throw Errors.SwitchTimeInFuture;

            // Fetch the last two switches for the system to do bounds checking on
            var lastTwoSwitches = (await _data.GetSwitches(ctx.System, 2)).ToArray();
            
            // If we don't have a switch to move, don't bother
            if (lastTwoSwitches.Length == 0) throw Errors.NoRegisteredSwitches;
            
            // If there's a switch *behind* the one we move, we check to make srue we're not moving the time further back than that
            if (lastTwoSwitches.Length == 2)
            {
                if (lastTwoSwitches[1].Timestamp > time.ToInstant())
                    throw Errors.SwitchMoveBeforeSecondLast(lastTwoSwitches[1].Timestamp.InZone(tz));
            }
            
            // Now we can actually do the move, yay!
            // But, we do a prompt to confirm.
            var lastSwitchMembers = await _data.GetSwitchMembers(lastTwoSwitches[0]);
            var lastSwitchMemberStr = string.Join(", ", lastSwitchMembers.Select(m => m.Name));
            var lastSwitchTimeStr = Formats.ZonedDateTimeFormat.Format(lastTwoSwitches[0].Timestamp.InZone(ctx.System.Zone));
            var lastSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp);
            var newSwitchTimeStr = Formats.ZonedDateTimeFormat.Format(time);
            var newSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - time.ToInstant());
            
            // yeet
            var msg = await ctx.Reply($"{Emojis.Warn} This will move the latest switch ({lastSwitchMemberStr.SanitizeMentions()}) from {lastSwitchTimeStr} ({lastSwitchDeltaStr} ago) to {newSwitchTimeStr} ({newSwitchDeltaStr} ago). Is this OK?");
            if (!await ctx.PromptYesNo(msg)) throw Errors.SwitchMoveCancelled;
            
            // aaaand *now* we do the move
            await _data.MoveSwitch(lastTwoSwitches[0], time.ToInstant());
            await ctx.Reply($"{Emojis.Success} Switch moved.");
        }
        
        public async Task SwitchDelete(Context ctx)
        {
            ctx.CheckSystem();
            
            // Fetch the last two switches for the system to do bounds checking on
            var lastTwoSwitches = (await _data.GetSwitches(ctx.System, 2)).ToArray();
            if (lastTwoSwitches.Length == 0) throw Errors.NoRegisteredSwitches;

            var lastSwitchMembers = await _data.GetSwitchMembers(lastTwoSwitches[0]);
            var lastSwitchMemberStr = string.Join(", ", lastSwitchMembers.Select(m => m.Name));
            var lastSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp);

            IUserMessage msg;
            if (lastTwoSwitches.Length == 1)
            {
                msg = await ctx.Reply(
                    $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr.SanitizeMentions()}, {lastSwitchDeltaStr} ago). You have no other switches logged. Is this okay?");
            }
            else
            {
                var secondSwitchMembers = await _data.GetSwitchMembers(lastTwoSwitches[1]);
                var secondSwitchMemberStr = string.Join(", ", secondSwitchMembers.Select(m => m.Name));
                var secondSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[1].Timestamp);
                msg = await ctx.Reply(
                    $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr.SanitizeMentions()}, {lastSwitchDeltaStr} ago). The next latest switch is {secondSwitchMemberStr.SanitizeMentions()} ({secondSwitchDeltaStr} ago). Is this okay?");
            }

            if (!await ctx.PromptYesNo(msg)) throw Errors.SwitchDeleteCancelled;
            await _data.DeleteSwitch(lastTwoSwitches[0]);
            
            await ctx.Reply($"{Emojis.Success} Switch deleted.");
        }
    }
}
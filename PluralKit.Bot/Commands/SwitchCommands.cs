using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using NodaTime;
using NodaTime.TimeZones;

namespace PluralKit.Bot.Commands
{
    [Group("switch")]
    [Alias("sw")]
    public class SwitchCommands: ModuleBase<PKCommandContext>
    {
        public SystemStore Systems { get; set; }
        public SwitchStore Switches { get; set; }

        [Command]
        [Remarks("switch <member> [member...]")]
        [MustHaveSystem]
        public async Task Switch(params PKMember[] members) => await DoSwitchCommand(members);

        [Command("out")]
        [Alias("none")]
        [Remarks("switch out")]
        [MustHaveSystem]
        public async Task SwitchOut() => await DoSwitchCommand(new PKMember[] { });

        private async Task DoSwitchCommand(ICollection<PKMember> members)
        {
            // Make sure all the members *are actually in the system*
            // PKMember parameters won't let this happen if they resolve by name
            // but they can if they resolve with ID
            if (members.Any(m => m.System != Context.SenderSystem.Id)) throw Errors.SwitchMemberNotInSystem;
            
            // Make sure there are no dupes in the list
            // We do this by checking if removing duplicate member IDs results in a list of different length 
            if (members.Select(m => m.Id).Distinct().Count() != members.Count) throw Errors.DuplicateSwitchMembers;

            // Find the last switch and its members if applicable
            var lastSwitch = await Switches.GetLatestSwitch(Context.SenderSystem);
            if (lastSwitch != null)
            {
                var lastSwitchMembers = await Switches.GetSwitchMembers(lastSwitch);
                // Make sure the requested switch isn't identical to the last one
                if (lastSwitchMembers.Select(m => m.Id).SequenceEqual(members.Select(m => m.Id)))
                    throw Errors.SameSwitch(members);
            }

            await Switches.RegisterSwitch(Context.SenderSystem, members);

            if (members.Count == 0)
                await Context.Channel.SendMessageAsync($"{Emojis.Success} Switch-out registered.");
            else
                await Context.Channel.SendMessageAsync($"{Emojis.Success} Switch registered. Current fronter is now {string.Join(", ", members.Select(m => m.Name)).Sanitize()}.");
        }

        [Command("move")]
        [Alias("shift")]
        [Remarks("switch move <date/time>")]
        [MustHaveSystem]
        public async Task SwitchMove([Remainder] string str)
        {
            var tz = TzdbDateTimeZoneSource.Default.ForId(Context.SenderSystem.UiTz ?? "UTC");
            
            var result = PluralKit.Utils.ParseDateTime(str, true, tz);
            if (result == null) throw Errors.InvalidDateTime(str);
            
            var time = result.Value;
            if (time.ToInstant() > SystemClock.Instance.GetCurrentInstant()) throw Errors.SwitchTimeInFuture;

            // Fetch the last two switches for the system to do bounds checking on
            var lastTwoSwitches = (await Switches.GetSwitches(Context.SenderSystem, 2)).ToArray();
            
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
            var lastSwitchMembers = await Switches.GetSwitchMembers(lastTwoSwitches[0]);
            var lastSwitchMemberStr = string.Join(", ", lastSwitchMembers.Select(m => m.Name));
            var lastSwitchTimeStr = Formats.ZonedDateTimeFormat.Format(lastTwoSwitches[0].Timestamp.InZone(Context.SenderSystem.Zone));
            var lastSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp);
            var newSwitchTimeStr = Formats.ZonedDateTimeFormat.Format(time);
            var newSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - time.ToInstant());
            
            // yeet
            var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} This will move the latest switch ({lastSwitchMemberStr.Sanitize()}) from {lastSwitchTimeStr} ({lastSwitchDeltaStr} ago) to {newSwitchTimeStr} ({newSwitchDeltaStr} ago). Is this OK?");
            if (!await Context.PromptYesNo(msg)) throw Errors.SwitchMoveCancelled;
            
            // aaaand *now* we do the move
            await Switches.MoveSwitch(lastTwoSwitches[0], time.ToInstant());
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Switch moved.");
        }

        [Command("delete")]
        [Remarks("switch delete")]
        [Alias("remove", "erase", "cancel", "yeet")]
        [MustHaveSystem]
        public async Task SwitchDelete()
        {
            // Fetch the last two switches for the system to do bounds checking on
            var lastTwoSwitches = (await Switches.GetSwitches(Context.SenderSystem, 2)).ToArray();
            if (lastTwoSwitches.Length == 0) throw Errors.NoRegisteredSwitches;

            var lastSwitchMembers = await Switches.GetSwitchMembers(lastTwoSwitches[0]);
            var lastSwitchMemberStr = string.Join(", ", lastSwitchMembers.Select(m => m.Name));
            var lastSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp);

            IUserMessage msg;
            if (lastTwoSwitches.Length == 1)
            {
                msg = await Context.Channel.SendMessageAsync(
                    $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr.Sanitize()}, {lastSwitchDeltaStr} ago). You have no other switches logged. Is this okay?");
            }
            else
            {
                var secondSwitchMembers = await Switches.GetSwitchMembers(lastTwoSwitches[1]);
                var secondSwitchMemberStr = string.Join(", ", secondSwitchMembers.Select(m => m.Name));
                var secondSwitchDeltaStr = Formats.DurationFormat.Format(SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[1].Timestamp);
                msg = await Context.Channel.SendMessageAsync(
                    $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr.Sanitize()}, {lastSwitchDeltaStr} ago). The next latest switch is {secondSwitchMemberStr.Sanitize()} ({secondSwitchDeltaStr} ago). Is this okay?");
            }

            if (!await Context.PromptYesNo(msg)) throw Errors.SwitchDeleteCancelled;
            await Switches.DeleteSwitch(lastTwoSwitches[0]);
            
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Switch deleted.");
        }
    }
}
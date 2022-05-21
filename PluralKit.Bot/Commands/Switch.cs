using NodaTime;
using NodaTime.TimeZones;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Switch
{
    public async Task SwitchDo(Context ctx)
    {
        ctx.CheckSystem();

        var members = await ctx.ParseMemberList(ctx.System.Id);
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
        if (members.Count > Limits.MaxSwitchMemberCount)
            throw new PKError(
                $"Switch contains too many members ({members.Count} > {Limits.MaxSwitchMemberCount} members).");

        // Find the last switch and its members if applicable
        await using var conn = await ctx.Database.Obtain();
        var lastSwitch = await ctx.Repository.GetLatestSwitch(ctx.System.Id);
        if (lastSwitch != null)
        {
            var lastSwitchMembers = ctx.Repository.GetSwitchMembers(conn, lastSwitch.Id);
            // Make sure the requested switch isn't identical to the last one
            if (await lastSwitchMembers.Select(m => m.Id)
                    .SequenceEqualAsync(members.Select(m => m.Id).ToAsyncEnumerable()))
                throw Errors.SameSwitch(members, ctx.LookupContextFor(ctx.System.Id));
        }

        await ctx.Repository.AddSwitch(conn, ctx.System.Id, members.Select(m => m.Id).ToList());

        if (members.Count == 0)
            await ctx.Reply($"{Emojis.Success} Switch-out registered.");
        else
            await ctx.Reply(
                $"{Emojis.Success} Switch registered. Current fronter is now {string.Join(", ", members.Select(m => m.NameFor(ctx)))}.");
    }

    public async Task SwitchMove(Context ctx)
    {
        ctx.CheckSystem();

        var timeToMove = ctx.RemainderOrNull() ??
                         throw new PKSyntaxError("Must pass a date or time to move the switch to.");
        var tz = TzdbDateTimeZoneSource.Default.ForId(ctx.Config?.UiTz ?? "UTC");

        var result = DateUtils.ParseDateTime(timeToMove, true, tz);
        if (result == null) throw Errors.InvalidDateTime(timeToMove);


        var time = result.Value;
        if (time.ToInstant() > SystemClock.Instance.GetCurrentInstant()) throw Errors.SwitchTimeInFuture;

        // Fetch the last two switches for the system to do bounds checking on
        var lastTwoSwitches = await ctx.Repository.GetSwitches(ctx.System.Id).Take(2).ToListAsync();

        // If we don't have a switch to move, don't bother
        if (lastTwoSwitches.Count == 0) throw Errors.NoRegisteredSwitches;

        // If there's a switch *behind* the one we move, we check to make sure we're not moving the time further back than that
        if (lastTwoSwitches.Count == 2)
            if (lastTwoSwitches[1].Timestamp > time.ToInstant())
                throw Errors.SwitchMoveBeforeSecondLast(lastTwoSwitches[1].Timestamp.InZone(tz));

        // Now we can actually do the move, yay!
        // But, we do a prompt to confirm.
        var lastSwitchMembers = ctx.Database.Execute(conn => ctx.Repository.GetSwitchMembers(conn, lastTwoSwitches[0].Id));
        var lastSwitchMemberStr =
            string.Join(", ", await lastSwitchMembers.Select(m => m.NameFor(ctx)).ToListAsync());
        var lastSwitchTime = lastTwoSwitches[0].Timestamp.ToUnixTimeSeconds(); // .FormatZoned(ctx.System)
        var lastSwitchDeltaStr =
            (SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp).FormatDuration();
        var newSwitchTime = time.ToInstant().ToUnixTimeSeconds();
        var newSwitchDeltaStr = (SystemClock.Instance.GetCurrentInstant() - time.ToInstant()).FormatDuration();

        // yeet
        var msg =
            $"{Emojis.Warn} This will move the latest switch ({lastSwitchMemberStr}) from <t:{lastSwitchTime}> ({lastSwitchDeltaStr} ago) to <t:{newSwitchTime}> ({newSwitchDeltaStr} ago). Is this OK?";
        if (!await ctx.PromptYesNo(msg, "Move Switch")) throw Errors.SwitchMoveCancelled;

        // aaaand *now* we do the move
        await ctx.Repository.MoveSwitch(lastTwoSwitches[0].Id, time.ToInstant());
        await ctx.Reply($"{Emojis.Success} Switch moved to <t:{newSwitchTime}> ({newSwitchDeltaStr} ago).");
    }

    public async Task SwitchEdit(Context ctx)
    {
        ctx.CheckSystem();

        var members = await ctx.ParseMemberList(ctx.System.Id);
        await DoEditCommand(ctx, members);
    }

    public async Task SwitchEditOut(Context ctx)
    {
        ctx.CheckSystem();
        await DoEditCommand(ctx, new PKMember[] { });
    }

    public async Task DoEditCommand(Context ctx, ICollection<PKMember> members)
    {
        // Make sure there are no dupes in the list
        // We do this by checking if removing duplicate member IDs results in a list of different length
        if (members.Select(m => m.Id).Distinct().Count() != members.Count) throw Errors.DuplicateSwitchMembers;

        // Find the switch to edit
        await using var conn = await ctx.Database.Obtain();
        var lastSwitch = await ctx.Repository.GetLatestSwitch(ctx.System.Id);
        // Make sure there's at least one switch
        if (lastSwitch == null) throw Errors.NoRegisteredSwitches;
        var lastSwitchMembers = ctx.Repository.GetSwitchMembers(conn, lastSwitch.Id);
        // Make sure switch isn't being edited to have the members it already does
        if (await lastSwitchMembers.Select(m => m.Id)
                .SequenceEqualAsync(members.Select(m => m.Id).ToAsyncEnumerable()))
            throw Errors.SameSwitch(members, ctx.LookupContextFor(ctx.System.Id));

        // Send a prompt asking the user to confirm the switch
        var lastSwitchDeltaStr = (SystemClock.Instance.GetCurrentInstant() - lastSwitch.Timestamp).FormatDuration();
        var lastSwitchMemberStr =
            string.Join(", ", await lastSwitchMembers.Select(m => m.NameFor(ctx)).ToListAsync());
        var newSwitchMemberStr = string.Join(", ", members.Select(m => m.NameFor(ctx)));

        string msg;
        if (members.Count == 0)
            msg = $"{Emojis.Warn} This will turn the latest switch ({lastSwitchMemberStr}, {lastSwitchDeltaStr} ago) into a switch-out. Is this okay?";
        else
            msg = $"{Emojis.Warn} This will change the latest switch ({lastSwitchMemberStr}, {lastSwitchDeltaStr} ago) to {newSwitchMemberStr}. Is this okay?";
        if (!await ctx.PromptYesNo(msg, "Edit")) throw Errors.SwitchEditCancelled;

        // Actually edit the switch
        await ctx.Repository.EditSwitch(conn, lastSwitch.Id, members.Select(m => m.Id).ToList());

        // Tell the user the edit suceeded
        if (members.Count == 0)
            await ctx.Reply($"{Emojis.Success} Switch edited. The latest switch is now a switch-out.");
        else
            await ctx.Reply($"{Emojis.Success} Switch edited. Current fronter is now {newSwitchMemberStr}.");
    }

    public async Task SwitchDelete(Context ctx)
    {
        ctx.CheckSystem();

        if (ctx.Match("all", "clear") || ctx.MatchFlag("all", "clear"))
        {
            // Subcommand: "delete all"
            var purgeMsg =
                $"{Emojis.Warn} This will delete *all registered switches* in your system. Are you sure you want to proceed?";
            if (!await ctx.PromptYesNo(purgeMsg, "Clear Switches"))
                throw Errors.GenericCancelled();
            await ctx.Repository.DeleteAllSwitches(ctx.System.Id);
            await ctx.Reply($"{Emojis.Success} Cleared system switches!");
            return;
        }

        // Fetch the last two switches for the system to do bounds checking on
        var lastTwoSwitches = await ctx.Repository.GetSwitches(ctx.System.Id).Take(2).ToListAsync();
        if (lastTwoSwitches.Count == 0) throw Errors.NoRegisteredSwitches;

        var lastSwitchMembers = ctx.Database.Execute(conn => ctx.Repository.GetSwitchMembers(conn, lastTwoSwitches[0].Id));
        var lastSwitchMemberStr =
            string.Join(", ", await lastSwitchMembers.Select(m => m.NameFor(ctx)).ToListAsync());
        var lastSwitchDeltaStr =
            (SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[0].Timestamp).FormatDuration();

        string msg;
        if (lastTwoSwitches.Count == 1)
        {
            msg = $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr}, {lastSwitchDeltaStr} ago). You have no other switches logged. Is this okay?";
        }
        else
        {
            var secondSwitchMembers = ctx.Database.Execute(conn => ctx.Repository.GetSwitchMembers(conn, lastTwoSwitches[1].Id));
            var secondSwitchMemberStr =
                string.Join(", ", await secondSwitchMembers.Select(m => m.NameFor(ctx)).ToListAsync());
            var secondSwitchDeltaStr = (SystemClock.Instance.GetCurrentInstant() - lastTwoSwitches[1].Timestamp)
                .FormatDuration();
            msg = $"{Emojis.Warn} This will delete the latest switch ({lastSwitchMemberStr}, {lastSwitchDeltaStr} ago). The next latest switch is {secondSwitchMemberStr} ({secondSwitchDeltaStr} ago). Is this okay?";
        }

        if (!await ctx.PromptYesNo(msg, "Delete Switch")) throw Errors.SwitchDeleteCancelled;
        await ctx.Repository.DeleteSwitch(lastTwoSwitches[0].Id);

        await ctx.Reply($"{Emojis.Success} Switch deleted.");
    }
}
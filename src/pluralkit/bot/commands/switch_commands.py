import dateparser
import humanize
from datetime import datetime, timezone
from typing import List

import pluralkit.utils
from pluralkit.bot import help
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")


async def switch_member(ctx: CommandContext):
    system = await ctx.ensure_system()

    if not ctx.has_next():
        return CommandError("You must pass at least one member name or ID to register a switch to.",
                            help=help.switch_register)

    members: List[Member] = []
    for member_name in ctx.remaining().split(" "):
        # Find the member
        member = await utils.get_member_fuzzy(ctx.conn, system.id, member_name)
        if not member:
            return CommandError("Couldn't find member \"{}\".".format(member_name))
        members.append(member)

    # Compare requested switch IDs and existing fronter IDs to check for existing switches
    # Lists, because order matters, it makes sense to just swap fronters
    member_ids = [member.id for member in members]
    fronter_ids = (await pluralkit.utils.get_fronter_ids(ctx.conn, system.id))[0]
    if member_ids == fronter_ids:
        if len(members) == 1:
            return CommandError("{} is already fronting.".format(members[0].name))
        return CommandError("Members {} are already fronting.".format(", ".join([m.name for m in members])))

    # Also make sure there aren't any duplicates
    if len(set(member_ids)) != len(member_ids):
        return CommandError("Duplicate members in member list.")

    # Log the switch
    async with ctx.conn.transaction():
        switch_id = await db.add_switch(ctx.conn, system_id=system.id)
        for member in members:
            await db.add_switch_member(ctx.conn, switch_id=switch_id, member_id=member.id)

    if len(members) == 1:
        return CommandSuccess("Switch registered. Current fronter is now {}.".format(members[0].name))
    else:
        return CommandSuccess(
            "Switch registered. Current fronters are now {}.".format(", ".join([m.name for m in members])))


async def switch_out(ctx: CommandContext):
    system = await ctx.ensure_system()

    # Get current fronters
    fronters, _ = await pluralkit.utils.get_fronter_ids(ctx.conn, system_id=system.id)
    if not fronters:
        return CommandError("There's already no one in front.")

    # Log it, and don't log any members
    await db.add_switch(ctx.conn, system_id=system.id)
    return CommandSuccess("Switch-out registered.")


async def switch_move(ctx: CommandContext):
    system = await ctx.ensure_system()
    if not ctx.has_next():
        return CommandError("You must pass a time to move the switch to.", help=help.switch_move)

    # Parse the time to move to
    new_time = dateparser.parse(ctx.remaining(), languages=["en"], settings={
        "TO_TIMEZONE": "UTC",
        "RETURN_AS_TIMEZONE_AWARE": False
    })
    if not new_time:
        return CommandError("'{}' can't be parsed as a valid time.".format(ctx.remaining()), help=help.switch_move)

    # Make sure the time isn't in the future
    if new_time > datetime.utcnow():
        return CommandError("Can't move switch to a time in the future.", help=help.switch_move)

    # Make sure it all runs in a big transaction for atomicity
    async with ctx.conn.transaction():
        # Get the last two switches to make sure the switch to move isn't before the second-last switch
        last_two_switches = await pluralkit.utils.get_front_history(ctx.conn, system.id, count=2)
        if len(last_two_switches) == 0:
            return CommandError("There are no registered switches for this system.")

        last_timestamp, last_fronters = last_two_switches[0]
        if len(last_two_switches) > 1:
            second_last_timestamp, _ = last_two_switches[1]

            if new_time < second_last_timestamp:
                time_str = humanize.naturaltime(pluralkit.utils.fix_time(second_last_timestamp))
                return CommandError(
                    "Can't move switch to before last switch time ({}), as it would cause conflicts.".format(time_str))

        # Display the confirmation message w/ humanized times
        members = ", ".join([member.name for member in last_fronters]) or "nobody"
        last_absolute = last_timestamp.isoformat(sep=" ", timespec="seconds")
        last_relative = humanize.naturaltime(pluralkit.utils.fix_time(last_timestamp))
        new_absolute = new_time.isoformat(sep=" ", timespec="seconds")
        new_relative = humanize.naturaltime(pluralkit.utils.fix_time(new_time))
        embed = embeds.status(
            "This will move the latest switch ({}) from {} ({}) to {} ({}). Is this OK?".format(members, last_absolute,
                                                                                                last_relative,
                                                                                                new_absolute,
                                                                                                new_relative))

        # Await and handle confirmation reactions
        confirm_msg = await ctx.reply(embed=embed)
        await ctx.client.add_reaction(confirm_msg, "✅")
        await ctx.client.add_reaction(confirm_msg, "❌")

        reaction = await ctx.client.wait_for_reaction(emoji=["✅", "❌"], message=confirm_msg, user=ctx.message.author,
                                                      timeout=60.0 * 5)
        if not reaction:
            return CommandError("Switch move timed out.")

        if reaction.reaction.emoji == "❌":
            return CommandError("Switch move cancelled.")

        # DB requires the actual switch ID which our utility method above doesn't return, do this manually
        switch_id = (await db.front_history(ctx.conn, system.id, count=1))[0]["id"]

        # Change the switch in the DB
        await db.move_last_switch(ctx.conn, system.id, switch_id, new_time)
        return CommandSuccess("Switch moved.")

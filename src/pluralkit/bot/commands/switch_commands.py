import dateparser
from datetime import datetime
from typing import List

import pluralkit.utils
from pluralkit.bot import help
from pluralkit.bot.commands import *
from pluralkit.member import Member
from pluralkit.utils import display_relative


async def switch_root(ctx: CommandContext):
    if not ctx.has_next():
        raise CommandError("You must use a subcommand. For a list of subcommands, type `pk;switch help`.")

    if ctx.match("out"):
        await switch_out(ctx)
    elif ctx.match("move"):
        await switch_move(ctx)
    elif ctx.match("delete") or ctx.match("remove") or ctx.match("erase") or ctx.match("cancel"):
        await switch_delete(ctx)
    elif ctx.match("help"):
        await ctx.reply(help.member_commands)
    else:
        await switch_member(ctx)


async def switch_member(ctx: CommandContext):
    system = await ctx.ensure_system()

    if not ctx.has_next():
        raise CommandError("You must pass at least one member name or ID to register a switch to.")

    members: List[Member] = []
    while ctx.has_next():
        members.append(await ctx.pop_member())

    # Compare requested switch IDs and existing fronter IDs to check for existing switches
    # Lists, because order matters, it makes sense to just swap fronters
    member_ids = [member.id for member in members]
    fronter_ids = (await pluralkit.utils.get_fronter_ids(ctx.conn, system.id))[0]
    if member_ids == fronter_ids:
        if len(members) == 1:
            raise CommandError("{} is already fronting.".format(members[0].name))
        raise CommandError("Members {} are already fronting.".format(", ".join([m.name for m in members])))

    # Also make sure there aren't any duplicates
    if len(set(member_ids)) != len(member_ids):
        raise CommandError("Duplicate members in member list.")

    # Log the switch
    await system.add_switch(ctx.conn, members)

    if len(members) == 1:
        await ctx.reply_ok("Switch registered. Current fronter is now {}.".format(members[0].name))
    else:
        await ctx.reply_ok(
            "Switch registered. Current fronters are now {}.".format(", ".join([m.name for m in members])))


async def switch_out(ctx: CommandContext):
    system = await ctx.ensure_system()

    switch = await system.get_latest_switch(ctx.conn)
    if switch and not switch.members:
        raise CommandError("There's already no one in front.")

    # Log it, and don't log any members
    await system.add_switch(ctx.conn, [])
    await ctx.reply_ok("Switch-out registered.")


async def switch_delete(ctx: CommandContext):
    system = await ctx.ensure_system()

    last_two_switches = await system.get_switches(ctx.conn, 2)
    if not last_two_switches:
        raise CommandError("You do not have a logged switch to delete.")

    last_switch = last_two_switches[0]
    next_last_switch = last_two_switches[1] if len(last_two_switches) > 1 else None

    last_switch_members = ", ".join([member.name for member in await last_switch.fetch_members(ctx.conn)])
    last_switch_time = display_relative(last_switch.timestamp)

    if next_last_switch:
        next_last_switch_members = ", ".join([member.name for member in await next_last_switch.fetch_members(ctx.conn)])
        next_last_switch_time = display_relative(next_last_switch.timestamp)
        msg = await ctx.reply_warn("This will delete the latest switch ({}, {} ago). The next latest switch is {} ({} ago). Is this okay?".format(last_switch_members, last_switch_time, next_last_switch_members, next_last_switch_time))
    else:
        msg = await ctx.reply_warn("This will delete the latest switch ({}, {} ago). You have no other switches logged. Is this okay?".format(last_switch_members, last_switch_time))

    if not await ctx.confirm_react(ctx.message.author, msg):
        raise CommandError("Switch deletion cancelled.")

    await last_switch.delete(ctx.conn)

    if next_last_switch:
        # lol block scope amirite
        # but yeah this is fine
        await ctx.reply_ok("Switch deleted. Next latest switch is now {} ({} ago).".format(next_last_switch_members, next_last_switch_time))
    else:
        await ctx.reply_ok("Switch deleted. You now have no logged switches.")


async def switch_move(ctx: CommandContext):
    system = await ctx.ensure_system()
    if not ctx.has_next():
        raise CommandError("You must pass a time to move the switch to.")

    # Parse the time to move to
    new_time = dateparser.parse(ctx.remaining(), languages=["en"], settings={
        "TO_TIMEZONE": "UTC",
        "RETURN_AS_TIMEZONE_AWARE": False
    })
    if not new_time:
        raise CommandError("'{}' can't be parsed as a valid time.".format(ctx.remaining()))

    # Make sure the time isn't in the future
    if new_time > datetime.utcnow():
        raise CommandError("Can't move switch to a time in the future.")

    # Make sure it all runs in a big transaction for atomicity
    async with ctx.conn.transaction():
        # Get the last two switches to make sure the switch to move isn't before the second-last switch
        last_two_switches = await pluralkit.utils.get_front_history(ctx.conn, system.id, count=2)
        if len(last_two_switches) == 0:
            raise CommandError("There are no registered switches for this system.")

        last_timestamp, last_fronters = last_two_switches[0]
        if len(last_two_switches) > 1:
            second_last_timestamp, _ = last_two_switches[1]

            if new_time < second_last_timestamp:
                time_str = display_relative(second_last_timestamp)
                raise CommandError(
                    "Can't move switch to before last switch time ({} ago), as it would cause conflicts.".format(time_str))

        # Display the confirmation message w/ humanized times
        members = ", ".join([member.name for member in last_fronters]) or "nobody"
        last_absolute = last_timestamp.isoformat(sep=" ", timespec="seconds")
        last_relative = display_relative(last_timestamp)
        new_absolute = new_time.isoformat(sep=" ", timespec="seconds")
        new_relative = display_relative(new_time)

        # Confirm with user
        switch_confirm_message = await ctx.reply(
            "This will move the latest switch ({}) from {} ({} ago) to {} ({} ago). Is this OK?".format(members,
                                                                                                        last_absolute,
                                                                                                        last_relative,
                                                                                                        new_absolute,
                                                                                                        new_relative))

        if not await ctx.confirm_react(ctx.message.author, switch_confirm_message):
            raise CommandError("Switch move cancelled.")

        # DB requires the actual switch ID which our utility method above doesn't return, do this manually
        switch_id = (await db.front_history(ctx.conn, system.id, count=1))[0]["id"]

        # Change the switch in the DB
        await db.move_last_switch(ctx.conn, system.id, switch_id, new_time)
        await ctx.reply_ok("Switch moved.")

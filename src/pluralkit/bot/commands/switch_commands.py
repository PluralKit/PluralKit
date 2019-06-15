from datetime import datetime
from typing import List

import dateparser
import pytz

from pluralkit.bot.commands import *
from pluralkit.bot.commands.system_commands import system_fronthistory
from pluralkit.member import Member
from pluralkit.utils import display_relative


async def switch_root(ctx: CommandContext):
    if not ctx.has_next():
        # We could raise an error here, but we display the system front history instead as a shortcut
        #raise CommandError("You must use a subcommand. For a list of subcommands, type `pk;help member`.")
        await system_fronthistory(ctx, await ctx.ensure_system())
        return
    if ctx.match("out"):
        await switch_out(ctx)
    elif ctx.match("move"):
        await switch_move(ctx)
    elif ctx.match("delete") or ctx.match("remove") or ctx.match("erase") or ctx.match("cancel"):
        await switch_delete(ctx)
    else:
        await switch_member(ctx)


async def switch_member(ctx: CommandContext):
    system = await ctx.ensure_system()

    members: List[Member] = []
    while ctx.has_next():
        members.append(await ctx.pop_member())

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
        # Tell it to default to the system's given time zone
        # If no time zone was given *explicitly in the string* it'll return as naive
        "TIMEZONE": system.ui_tz
    })

    if not new_time:
        raise CommandError("'{}' can't be parsed as a valid time.".format(ctx.remaining()))

    tz = pytz.timezone(system.ui_tz)
    # So we default to putting the system's time zone in the tzinfo
    if not new_time.tzinfo:
        new_time = tz.localize(new_time)

    # Now that we have a system-time datetime, convert this to UTC and make it naive since that's what we deal with
    new_time = pytz.utc.normalize(new_time).replace(tzinfo=None)

    # Make sure the time isn't in the future
    if new_time > datetime.utcnow():
        raise CommandError("Can't move switch to a time in the future.")

    # Make sure it all runs in a big transaction for atomicity
    async with ctx.conn.transaction():
        # Get the last two switches to make sure the switch to move isn't before the second-last switch
        last_two_switches = await system.get_switches(ctx.conn, 2)
        if len(last_two_switches) == 0:
            raise CommandError("There are no registered switches for this system.")

        last_switch = last_two_switches[0]
        if len(last_two_switches) > 1:
            second_last_switch = last_two_switches[1]

            if new_time < second_last_switch.timestamp:
                time_str = display_relative(second_last_switch.timestamp)
                raise CommandError(
                    "Can't move switch to before last switch time ({} ago), as it would cause conflicts.".format(time_str))

        # Display the confirmation message w/ humanized times
        last_fronters = await last_switch.fetch_members(ctx.conn)

        members = ", ".join([member.name for member in last_fronters]) or "nobody"
        last_absolute = ctx.format_time(last_switch.timestamp)
        last_relative = display_relative(last_switch.timestamp)
        new_absolute = ctx.format_time(new_time)
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

        # Actually move the switch
        await last_switch.move(ctx.conn, new_time)
        await ctx.reply_ok("Switch moved.")

from datetime import datetime
import logging
from typing import List

import dateparser
import humanize

import pluralkit.utils
from pluralkit import Member
from pluralkit.bot import utils, embeds, help
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")

@command(cmd="switch", usage="<name|id> [name|id]...", description="Registers a switch and changes the current fronter.", category="Switching commands")
async def switch_member(ctx: MemberCommandContext, args: List[str]):
    if len(args) == 0:
        return embeds.error("You must pass at least one member name or ID to register a switch to.", help=help.switch_register)

    members: List[Member] = []
    for member_name in args:
        # Find the member
        member = await utils.get_member_fuzzy(ctx.conn, ctx.system.id, member_name)
        if not member:
            return embeds.error("Couldn't find member \"{}\".".format(member_name))
        members.append(member)

    # Compare requested switch IDs and existing fronter IDs to check for existing switches
    # Lists, because order matters, it makes sense to just swap fronters
    member_ids = [member.id for member in members]
    fronter_ids = (await pluralkit.utils.get_fronter_ids(ctx.conn, ctx.system.id))[0]
    if member_ids == fronter_ids:
        if len(members) == 1:
            return embeds.error("{} is already fronting.".format(members[0].name))
        return embeds.error("Members {} are already fronting.".format(", ".join([m.name for m in members])))

    # Also make sure there aren't any duplicates
    if len(set(member_ids)) != len(member_ids):
        return embeds.error("Duplicate members in member list.")

    # Log the switch
    async with ctx.conn.transaction():
        switch_id = await db.add_switch(ctx.conn, system_id=ctx.system.id)
        for member in members:
            await db.add_switch_member(ctx.conn, switch_id=switch_id, member_id=member.id)

    if len(members) == 1:
        return embeds.success("Switch registered. Current fronter is now {}.".format(members[0].name))
    else:
        return embeds.success("Switch registered. Current fronters are now {}.".format(", ".join([m.name for m in members])))

@command(cmd="switch out", description="Registers a switch with no one in front.", category="Switching commands")
async def switch_out(ctx: MemberCommandContext, args: List[str]):
    # Get current fronters
    fronters, _ = await pluralkit.utils.get_fronter_ids(ctx.conn, system_id=ctx.system.id)
    if not fronters:
        return embeds.error("There's already no one in front.")

    # Log it, and don't log any members
    await db.add_switch(ctx.conn, system_id=ctx.system.id)
    return embeds.success("Switch-out registered.")

@command(cmd="switch move", usage="<time>", description="Moves the most recent switch to a different point in time.", category="Switching commands")
async def switch_move(ctx: MemberCommandContext, args: List[str]):
    if len(args) == 0:
        return embeds.error("You must pass a time to move the switch to.", help=help.switch_move)

    # Parse the time to move to
    new_time = dateparser.parse(" ".join(args), languages=["en"], settings={
        "TO_TIMEZONE": "UTC",
        "RETURN_AS_TIMEZONE_AWARE": False
    })
    if not new_time:
        return embeds.error("{} can't be parsed as a valid time.".format(" ".join(args)))

    # Make sure the time isn't in the future
    if new_time > datetime.now():
        return embeds.error("Can't move switch to a time in the future.")

    # Make sure it all runs in a big transaction for atomicity
    async with ctx.conn.transaction():
        # Get the last two switches to make sure the switch to move isn't before the second-last switch
        last_two_switches = await pluralkit.utils.get_front_history(ctx.conn, ctx.system.id, count=2)
        if len(last_two_switches) == 0:
            return embeds.error("There are no registered switches for this system.")

        last_timestamp, last_fronters = last_two_switches[0]
        if len(last_two_switches) > 1:
            second_last_timestamp, _ = last_two_switches[1]

            if new_time < second_last_timestamp:
                time_str = humanize.naturaltime(second_last_timestamp)
                return embeds.error("Can't move switch to before last switch time ({}), as it would cause conflicts.".format(time_str))
        
        # Display the confirmation message w/ humanized times
        members = ", ".join([member.name for member in last_fronters]) or "nobody"
        last_absolute = last_timestamp.isoformat(sep=" ", timespec="seconds")
        last_relative = humanize.naturaltime(last_timestamp)
        new_absolute = new_time.isoformat(sep=" ", timespec="seconds")
        new_relative = humanize.naturaltime(new_time)
        embed = utils.make_default_embed("This will move the latest switch ({}) from {} ({}) to {} ({}). Is this OK?".format(members, last_absolute, last_relative, new_absolute, new_relative))
        
        # Await and handle confirmation reactions
        confirm_msg = await ctx.reply(embed=embed)
        await ctx.client.add_reaction(confirm_msg, "✅")
        await ctx.client.add_reaction(confirm_msg, "❌")

        reaction = await ctx.client.wait_for_reaction(emoji=["✅", "❌"], message=confirm_msg, user=ctx.message.author, timeout=60.0)
        if not reaction:
            return embeds.error("Switch move timed out.")

        if reaction.reaction.emoji == "❌":
            return embeds.error("Switch move cancelled.")

        # DB requires the actual switch ID which our utility method above doesn't return, do this manually
        switch_id = (await db.front_history(ctx.conn, ctx.system.id, count=1))[0]["id"]

        # Change the switch in the DB
        await db.move_last_switch(ctx.conn, ctx.system.id, switch_id, new_time)
        return embeds.success("Switch moved.")

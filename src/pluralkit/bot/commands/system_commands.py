import dateparser
import humanize
from datetime import datetime
from urllib.parse import urlparse

import pluralkit.utils
from pluralkit.bot import help
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")


async def system_info(ctx: CommandContext):
    if ctx.has_next():
        system = await ctx.pop_system()
    else:
        system = await ctx.ensure_system()

    await ctx.reply(embed=await utils.generate_system_info_card(ctx.conn, ctx.client, system))


async def new_system(ctx: CommandContext):
    system = await ctx.get_system()
    if system:
        return CommandError(
            "You already have a system registered. To delete your system, use `pk;system delete`, or to unlink your system from this account, use `pk;system unlink`.")

    system_name = ctx.remaining() or None

    async with ctx.conn.transaction():
        # TODO: figure out what to do if this errors out on collision on generate_hid
        hid = utils.generate_hid()

        system = await db.create_system(ctx.conn, system_name=system_name, system_hid=hid)

        # Link account
        await db.link_account(ctx.conn, system_id=system.id, account_id=ctx.message.author.id)
        return CommandSuccess("System registered! To begin adding members, use `pk;member new <name>`.")


async def system_set(ctx: CommandContext):
    system = await ctx.ensure_system()

    prop = ctx.pop_str(CommandError("You must pass a property name to set.", help=help.edit_system))

    allowed_properties = ["name", "description", "tag", "avatar"]
    db_properties = {
        "name": "name",
        "description": "description",
        "tag": "tag",
        "avatar": "avatar_url"
    }

    if prop not in allowed_properties:
        return CommandError(
            "Unknown property {}. Allowed properties are {}.".format(prop, ", ".join(allowed_properties)),
            help=help.edit_system)

    if ctx.has_next():
        value = ctx.remaining()
        # Sanity checking
        if prop == "description":
            if len(value) > 1024:
                return CommandError("You can't have a description longer than 1024 characters.")

        if prop == "tag":
            if len(value) > 32:
                return CommandError("You can't have a system tag longer than 32 characters.")

            if re.search("<a?:\w+:\d+>", value):
                return CommandError("Due to a Discord limitation, custom emojis aren't supported. Please use a standard emoji instead.")

            # Make sure there are no members which would make the combined length exceed 32
            members_exceeding = await db.get_members_exceeding(ctx.conn, system_id=system.id,
                                                               length=32 - len(value) - 1)
            if len(members_exceeding) > 0:
                # If so, error out and warn
                member_names = ", ".join([member.name
                                          for member in members_exceeding])
                logger.debug("Members exceeding combined length with tag '{}': {}".format(value, member_names))
                return CommandError(
                    "The maximum length of a name plus the system tag is 32 characters. The following members would exceed the limit: {}. Please reduce the length of the tag, or rename the members.".format(
                        member_names))

        if prop == "avatar":
            user = await utils.parse_mention(ctx.client, value)
            if user:
                # Set the avatar to the mentioned user's avatar
                # Discord doesn't like webp, but also hosts png alternatives
                value = user.avatar_url.replace(".webp", ".png")
            else:
                # Validate URL
                u = urlparse(value)
                if u.scheme in ["http", "https"] and u.netloc and u.path:
                    value = value
                else:
                    return CommandError("Invalid image URL.")
    else:
        # Clear from DB
        value = None

    db_prop = db_properties[prop]
    await db.update_system_field(ctx.conn, system_id=system.id, field=db_prop, value=value)

    response = CommandSuccess("{} system {}.".format("Updated" if value else "Cleared", prop))
    #if prop == "avatar" and value:
    #    response.set_image(url=value)
    return response


async def system_link(ctx: CommandContext):
    system = await ctx.ensure_system()
    account_name = ctx.pop_str(CommandError("You must pass an account to link this system to.", help=help.link_account))

    # Find account to link
    linkee = await utils.parse_mention(ctx.client, account_name)
    if not linkee:
        return CommandError("Account not found.")

    # Make sure account doesn't already have a system
    account_system = await db.get_system_by_account(ctx.conn, linkee.id)
    if account_system:
        return CommandError("The mentioned account is already linked to a system (`{}`)".format(account_system.hid))

    # Send confirmation message
    msg = await ctx.reply(
        "{}, please confirm the link by clicking the ✅ reaction on this message.".format(linkee.mention))
    await ctx.client.add_reaction(msg, "✅")
    await ctx.client.add_reaction(msg, "❌")

    reaction = await ctx.client.wait_for_reaction(emoji=["✅", "❌"], message=msg, user=linkee, timeout=60.0 * 5)
    # If account to be linked confirms...
    if not reaction:
        return CommandError("Account link timed out.")
    if not reaction.reaction.emoji == "✅":
        return CommandError("Account link cancelled.")

    await db.link_account(ctx.conn, system_id=system.id, account_id=linkee.id)
    return CommandSuccess("Account linked to system.")


async def system_unlink(ctx: CommandContext):
    system = await ctx.ensure_system()

    # Make sure you can't unlink every account
    linked_accounts = await db.get_linked_accounts(ctx.conn, system_id=system.id)
    if len(linked_accounts) == 1:
        return CommandError("This is the only account on your system, so you can't unlink it.")

    await db.unlink_account(ctx.conn, system_id=system.id, account_id=ctx.message.author.id)
    return CommandSuccess("Account unlinked.")


async def system_fronter(ctx: CommandContext):
    if ctx.has_next():
        system = await ctx.pop_system()
    else:
        system = await ctx.ensure_system()

    fronters, timestamp = await pluralkit.utils.get_fronters(ctx.conn, system_id=system.id)
    fronter_names = [member.name for member in fronters]

    embed = embeds.status("")

    if len(fronter_names) == 0:
        embed.add_field(name="Current fronter", value="(no fronter)")
    elif len(fronter_names) == 1:
        embed.add_field(name="Current fronter", value=fronter_names[0])
    else:
        embed.add_field(name="Current fronters", value=", ".join(fronter_names))

    if timestamp:
        embed.add_field(name="Since", value="{} ({})".format(timestamp.isoformat(sep=" ", timespec="seconds"),
                                                             humanize.naturaltime(pluralkit.utils.fix_time(timestamp))))
    await ctx.reply(embed=embed)


async def system_fronthistory(ctx: CommandContext):
    if ctx.has_next():
        system = await ctx.pop_system()
    else:
        system = await ctx.ensure_system()

    lines = []
    front_history = await pluralkit.utils.get_front_history(ctx.conn, system.id, count=10)
    for i, (timestamp, members) in enumerate(front_history):
        # Special case when no one's fronting
        if len(members) == 0:
            name = "(no fronter)"
        else:
            name = ", ".join([member.name for member in members])

        # Make proper date string
        time_text = timestamp.isoformat(sep=" ", timespec="seconds")
        rel_text = humanize.naturaltime(pluralkit.utils.fix_time(timestamp))

        delta_text = ""
        if i > 0:
            last_switch_time = front_history[i - 1][0]
            delta_text = ", for {}".format(humanize.naturaldelta(timestamp - last_switch_time))
        lines.append("**{}** ({}, {}{})".format(name, time_text, rel_text, delta_text))

    embed = embeds.status("\n".join(lines) or "(none)")
    embed.title = "Past switches"
    await ctx.reply(embed=embed)


async def system_delete(ctx: CommandContext):
    system = await ctx.ensure_system()

    delete_confirm_msg = "Are you sure you want to delete your system? If so, reply to this message with the system's ID (`{}`).".format(system.hid)
    if not await ctx.confirm_text(ctx.message.author, ctx.message.channel, system.hid, delete_confirm_msg):
        return CommandError("System deletion cancelled.")

    await db.remove_system(ctx.conn, system_id=system.id)
    return CommandSuccess("System deleted.")


async def system_frontpercent(ctx: CommandContext):
    system = await ctx.ensure_system()

    # Parse the time limit (will go this far back)
    before = dateparser.parse(ctx.remaining(), languages=["en"], settings={
        "TO_TIMEZONE": "UTC",
        "RETURN_AS_TIMEZONE_AWARE": False
    })

    # If time is in the future, just kinda discard
    if before and before > datetime.utcnow():
        before = None

    # Fetch list of switches
    all_switches = await pluralkit.utils.get_front_history(ctx.conn, system.id, 99999)
    if not all_switches:
        return CommandError("No switches registered to this system.")

    # Cull the switches *ending* before the limit, if given
    # We'll need to find the first switch starting before the limit, then cut off every switch *before* that
    if before:
        for last_stamp, _ in all_switches:
            if last_stamp < before:
                break

        all_switches = [(stamp, members) for stamp, members in all_switches if stamp >= last_stamp]

    start_times = [stamp for stamp, _ in all_switches]
    end_times = [datetime.utcnow()] + start_times
    switch_members = [members for _, members in all_switches]

    # Gonna save a list of members by ID for future lookup too
    members_by_id = {}

    # Using the ID as a key here because it's a simple number that can be hashed and used as a key
    member_times = {}
    for start_time, end_time, members in zip(start_times, end_times, switch_members):
        # Cut off parts of the switch that occurs before the time limit (will only happen if this is the last switch)
        if before and start_time < before:
            start_time = before

        # Calculate length of the switch
        switch_length = end_time - start_time

        def add_switch(id, length):
            if id not in member_times:
                member_times[id] = length
            else:
                member_times[id] += length

        for member in members:
            # Add the switch length to the currently registered time for that member
            add_switch(member.id, switch_length)

            # Also save the member in the ID map for future reference
            members_by_id[member.id] = member

        # Also register a no-fronter switch with the key None
        if not members:
            add_switch(None, switch_length)

    # Find the total timespan of the range
    span_start = max(start_times[-1], before) if before else start_times[-1]
    total_time = datetime.utcnow() - span_start

    embed = embeds.status("")
    for member_id, front_time in sorted(member_times.items(), key=lambda x: x[1], reverse=True):
        member = members_by_id[member_id] if member_id else None

        # Calculate percent
        fraction = front_time / total_time
        percent = int(fraction * 100)

        embed.add_field(name=member.name if member else "(no fronter)",
                        value="{}% ({})".format(percent, humanize.naturaldelta(front_time)))

    embed.set_footer(text="Since {}".format(span_start.isoformat(sep=" ", timespec="seconds")))
    await ctx.reply(embed=embed)

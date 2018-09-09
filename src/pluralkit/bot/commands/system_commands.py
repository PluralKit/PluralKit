import dateparser
import humanize
from datetime import datetime

import pluralkit.utils
from pluralkit.bot import help
from pluralkit.bot.commands import *
from pluralkit.errors import ExistingSystemError, DescriptionTooLongError, TagTooLongError, TagTooLongWithMembersError, \
    InvalidAvatarURLError, UnlinkingLastAccountError

logger = logging.getLogger("pluralkit.commands")


async def system_info(ctx: CommandContext):
    if ctx.has_next():
        system = await ctx.pop_system()
    else:
        system = await ctx.ensure_system()

    await ctx.reply(embed=await utils.generate_system_info_card(ctx.conn, ctx.client, system))


async def new_system(ctx: CommandContext):
    system_name = ctx.remaining() or None

    try:
        await System.create_system(ctx.conn, ctx.message.author.id, system_name)
    except ExistingSystemError:
        return CommandError(
            "You already have a system registered. To delete your system, use `pk;system delete`, or to unlink your system from this account, use `pk;system unlink`.")

    return CommandSuccess("System registered! To begin adding members, use `pk;member new <name>`.")


async def system_set(ctx: CommandContext):
    system = await ctx.ensure_system()

    property_name = ctx.pop_str(CommandError("You must pass a property name to set.", help=help.edit_system))

    async def avatar_setter(conn, url):
        user = await utils.parse_mention(ctx.client, url)
        if user:
            # Set the avatar to the mentioned user's avatar
            # Discord pushes webp by default, which isn't supported by webhooks, but also hosts png alternatives
            url = user.avatar_url.replace(".webp", ".png")

        await system.set_avatar(conn, url)

    properties = {
        "name": system.set_name,
        "description": system.set_description,
        "tag": system.set_tag,
        "avatar": avatar_setter
    }

    if property_name not in properties:
        return CommandError(
            "Unknown property {}. Allowed properties are {}.".format(property_name, ", ".join(allowed_properties)),
            help=help.edit_system)

    value = ctx.remaining() or None

    try:
        await properties[property_name](ctx.conn, value)
    except DescriptionTooLongError:
        return CommandError("You can't have a description longer than 1024 characters.")
    except TagTooLongError:
        return CommandError("You can't have a system tag longer than 32 characters.")
    except TagTooLongWithMembersError as e:
        return CommandError("The maximum length of a name plus the system tag is 32 characters. The following members would exceed the limit: {}. Please reduce the length of the tag, or rename the members.".format(", ".join(e.member_names)))
    except InvalidAvatarURLError:
        return CommandError("Invalid image URL.")

    response = CommandSuccess("{} system {}.".format("Updated" if value else "Cleared", property_name))
    # if prop == "avatar" and value:
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
    account_system = await System.get_by_account(ctx.conn, linkee.id)
    if account_system:
        return CommandError("The mentioned account is already linked to a system (`{}`)".format(account_system.hid))

    if not await ctx.confirm_react(linkee, "{}, please confirm the link by clicking the ✅ reaction on this message.".format(linkee.mention)):
        return CommandError("Account link cancelled.")

    await system.link_account(ctx.conn, linkee.id)
    return CommandSuccess("Account linked to system.")


async def system_unlink(ctx: CommandContext):
    system = await ctx.ensure_system()

    try:
        await system.unlink_account(ctx.conn, ctx.message.author.id)
    except UnlinkingLastAccountError:
        return CommandError("This is the only account on your system, so you can't unlink it.")

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

    delete_confirm_msg = "Are you sure you want to delete your system? If so, reply to this message with the system's ID (`{}`).".format(
        system.hid)
    if not await ctx.confirm_text(ctx.message.author, ctx.message.channel, system.hid, delete_confirm_msg):
        return CommandError("System deletion cancelled.")

    await system.delete(ctx.conn)
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

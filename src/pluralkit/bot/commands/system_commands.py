import dateparser
import humanize
from datetime import datetime, timedelta

import pluralkit.bot.embeds
import pluralkit.utils
from pluralkit.bot import help
from pluralkit.bot.commands import *
from pluralkit.errors import ExistingSystemError, UnlinkingLastAccountError, PluralKitError, AccountAlreadyLinkedError

logger = logging.getLogger("pluralkit.commands")


async def system_info(ctx: CommandContext):
    if ctx.has_next():
        system = await ctx.pop_system()
    else:
        system = await ctx.ensure_system()

    await ctx.reply(embed=await pluralkit.bot.embeds.system_card(ctx.conn, ctx.client, system))


async def new_system(ctx: CommandContext):
    system_name = ctx.remaining() or None

    try:
        await System.create_system(ctx.conn, ctx.message.author.id, system_name)
    except ExistingSystemError as e:
        raise CommandError(e.message)

    await ctx.reply_ok("System registered! To begin adding members, use `pk;member new <name>`.")


async def system_set(ctx: CommandContext):
    raise CommandError(
        "`pk;system set` has been retired. Please use the new member modifying commands: `pk;system [name|description|avatar|tag]`.")


async def system_name(ctx: CommandContext):
    system = await ctx.ensure_system()
    new_name = ctx.remaining() or None

    await system.set_name(ctx.conn, new_name)
    await ctx.reply_ok("System name {}.".format("updated" if new_name else "cleared"))


async def system_description(ctx: CommandContext):
    system = await ctx.ensure_system()
    new_description = ctx.remaining() or None

    await system.set_description(ctx.conn, new_description)
    await ctx.reply_ok("System description {}.".format("updated" if new_description else "cleared"))


async def system_tag(ctx: CommandContext):
    system = await ctx.ensure_system()
    new_tag = ctx.remaining() or None

    await system.set_tag(ctx.conn, new_tag)
    await ctx.reply_ok("System tag {}.".format("updated" if new_tag else "cleared"))

    # System class is immutable, update the tag so get_member_name_limit works
    system = system._replace(tag=new_tag)
    members = await system.get_members(ctx.conn)

    # Certain members might not be able to be proxied with this new tag, show a warning for those
    members_exceeding = [member for member in members if
                         len(member.name) > system.get_member_name_limit()]
    if members_exceeding:
        member_names = ", ".join([member.name for member in members_exceeding])
        await ctx.reply_warn(
            "Due to the length of this tag, the following members will not be able to be proxied: {}. Please use a shorter tag to prevent this.".format(
                member_names))

    # Edge case: members with name length 1 and no new tag
    if not new_tag:
        one_length_members = [member for member in members if len(member.name) == 1]
        if one_length_members:
            member_names = ", ".join([member.name for member in one_length_members])
            await ctx.reply_warn(
                "Without a system tag, you will not be able to proxy members with a one-character name: {}. To prevent this, please add a system tag or lengthen their name.".format(
                    member_names))


async def system_avatar(ctx: CommandContext):
    system = await ctx.ensure_system()
    new_avatar_url = ctx.remaining() or None

    if new_avatar_url:
        user = await utils.parse_mention(ctx.client, new_avatar_url)
        if user:
            new_avatar_url = user.avatar_url_as(format="png")

    await system.set_avatar(ctx.conn, new_avatar_url)
    await ctx.reply_ok("System avatar {}.".format("updated" if new_avatar_url else "cleared"))


async def system_link(ctx: CommandContext):
    system = await ctx.ensure_system()
    account_name = ctx.pop_str(CommandError("You must pass an account to link this system to.", help=help.link_account))

    # Do the sanity checking here too (despite it being done in System.link_account)
    # Because we want it to be done before the confirmation dialog is shown

    # Find account to link
    linkee = await utils.parse_mention(ctx.client, account_name)
    if not linkee:
        raise CommandError("Account not found.")

    # Make sure account doesn't already have a system
    account_system = await System.get_by_account(ctx.conn, linkee.id)
    if account_system:
        raise CommandError(AccountAlreadyLinkedError(account_system).message)

    if not await ctx.confirm_react(linkee,
                                   "{}, please confirm the link by clicking the ✅ reaction on this message.".format(
                                       linkee.mention)):
        raise CommandError("Account link cancelled.")

    await system.link_account(ctx.conn, linkee.id)
    await ctx.reply_ok("Account linked to system.")


async def system_unlink(ctx: CommandContext):
    system = await ctx.ensure_system()

    try:
        await system.unlink_account(ctx.conn, ctx.message.author.id)
    except UnlinkingLastAccountError as e:
        raise CommandError(e.message)

    await ctx.reply_ok("Account unlinked.")


async def system_fronter(ctx: CommandContext):
    if ctx.has_next():
        system = await ctx.pop_system()
    else:
        system = await ctx.ensure_system()

    embed = await embeds.front_status(await system.get_latest_switch(ctx.conn), ctx.conn)
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
        raise CommandError("System deletion cancelled.")

    await system.delete(ctx.conn)
    await ctx.reply_ok("System deleted.")


async def system_frontpercent(ctx: CommandContext):
    system = await ctx.ensure_system()

    # Parse the time limit (will go this far back)
    if ctx.remaining():
        before = dateparser.parse(ctx.remaining(), languages=["en"], settings={
            "TO_TIMEZONE": "UTC",
            "RETURN_AS_TIMEZONE_AWARE": False
        })

        if not before:
            raise CommandError("Could not parse '{}' as a valid time.".format(ctx.remaining()))

        # If time is in the future, just kinda discard
        if before and before > datetime.utcnow():
            before = None
    else:
        before = datetime.utcnow() - timedelta(days=30)

    # Fetch list of switches
    all_switches = await pluralkit.utils.get_front_history(ctx.conn, system.id, 99999)
    if not all_switches:
        raise CommandError("No switches registered to this system.")

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
        percent = round(fraction * 100)

        embed.add_field(name=member.name if member else "(no fronter)",
                        value="{}% ({})".format(percent, humanize.naturaldelta(front_time)))

    embed.set_footer(text="Since {} ({})".format(span_start.isoformat(sep=" ", timespec="seconds"),
                                                 humanize.naturaltime(pluralkit.utils.fix_time(span_start))))
    await ctx.reply(embed=embed)

from datetime import datetime, timedelta

import aiohttp
import dateparser
import humanize
import math
import timezonefinder
import pytz

import pluralkit.bot.embeds
from pluralkit.bot.commands import *
from pluralkit.errors import ExistingSystemError, UnlinkingLastAccountError, AccountAlreadyLinkedError
from pluralkit.utils import display_relative

# This needs to load from the timezone file so we're preloading this so we
# don't have to do it on every invocation
tzf = timezonefinder.TimezoneFinder()

async def system_root(ctx: CommandContext):
    # Commands that operate without a specified system (usually defaults to the executor's own system)
    if ctx.match("name") or ctx.match("rename"):
        await system_name(ctx)
    elif ctx.match("description") or ctx.match("desc"):
        await system_description(ctx)
    elif ctx.match("avatar") or ctx.match("icon"):
        await system_avatar(ctx)
    elif ctx.match("tag"):
        await system_tag(ctx)
    elif ctx.match("new") or ctx.match("register") or ctx.match("create") or ctx.match("init"):
        await system_new(ctx)
    elif ctx.match("delete") or ctx.match("remove") or ctx.match("destroy") or ctx.match("erase"):
        await system_delete(ctx)
    elif ctx.match("front") or ctx.match("fronter") or ctx.match("fronters"):
        await system_fronter(ctx, await ctx.ensure_system())
    elif ctx.match("fronthistory"):
        await system_fronthistory(ctx, await ctx.ensure_system())
    elif ctx.match("frontpercent") or ctx.match("frontbreakdown") or ctx.match("frontpercentage"):
        await system_frontpercent(ctx, await ctx.ensure_system())
    elif ctx.match("timezone") or ctx.match("tz"):
        await system_timezone(ctx)
    elif ctx.match("set"):
        await system_set(ctx)
    elif ctx.match("list") or ctx.match("members"):
        await system_list(ctx, await ctx.ensure_system())
    elif not ctx.has_next():
        # (no argument, command ends here, default to showing own system)
        await system_info(ctx, await ctx.ensure_system())
    else:
        # If nothing matches, the next argument is likely a system name/ID, so delegate
        # to the specific system root
        await specified_system_root(ctx)


async def specified_system_root(ctx: CommandContext):
    # Commands that operate on a specified system (ie. not necessarily the command executor's)
    system_name = ctx.pop_str()
    system = await utils.get_system_fuzzy(ctx.conn, ctx.client, system_name)
    if not system:
        raise CommandError(
            "Unable to find system `{}`. If you meant to run a command, type `pk;help system` for a list of system commands.".format(
                system_name))

    if ctx.match("front") or ctx.match("fronter"):
        await system_fronter(ctx, system)
    elif ctx.match("fronthistory"):
        await system_fronthistory(ctx, system)
    elif ctx.match("frontpercent") or ctx.match("frontbreakdown") or ctx.match("frontpercentage"):
        await system_frontpercent(ctx, system)
    elif ctx.match("list") or ctx.match("members"):
        await system_list(ctx, system)
    else:
        await system_info(ctx, system)


async def system_info(ctx: CommandContext, system: System):
    this_system = await ctx.get_system()
    await ctx.reply(embed=await pluralkit.bot.embeds.system_card(ctx.conn, ctx.client, system, this_system and this_system.id == system.id))


async def system_new(ctx: CommandContext):
    new_name = ctx.remaining() or None

    try:
        await System.create_system(ctx.conn, ctx.message.author.id, new_name)
    except ExistingSystemError as e:
        raise CommandError(e.message)

    await ctx.reply_ok("System registered! To begin adding members, use `pk;member new <name>`.")


async def system_set(ctx: CommandContext):
    raise CommandError(
        "`pk;system set` has been retired. Please use the new system modifying commands. Type `pk;help system` for a list.")


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


async def system_timezone(ctx: CommandContext):
    system = await ctx.ensure_system()
    city_query = ctx.remaining() or None

    msg = await ctx.reply("\U0001F50D Searching '{}' (may take a while)...".format(city_query))

    # Look up the city on Overpass (OpenStreetMap)
    async with aiohttp.ClientSession() as sess:
        # OverpassQL is weird, but this basically searches for every node of type city with name [input].
        async with sess.get("https://nominatim.openstreetmap.org/search?city=novosibirsk&format=json&limit=1", params={"city": city_query, "format": "json", "limit": "1"}) as r:
            if r.status != 200:
                raise CommandError("OSM Nominatim API returned error. Try again.")
            data = await r.json()

    # If we didn't find a city, complain
    if not data:
        raise CommandError("City '{}' not found.".format(city_query))

    # Take the lat/long given by Overpass and put it into timezonefinder
    lat, lng = (float(data[0]["lat"]), float(data[0]["lon"]))
    timezone_name = tzf.timezone_at(lng=lng, lat=lat)

    # Also delete the original searching message
    await msg.delete()

    if not timezone_name:
        raise CommandError("Time zone for city '{}' not found. This should never happen.".format(data[0]["display_name"]))

    # This should hopefully result in a valid time zone name
    # (if not, something went wrong)
    tz = await system.set_time_zone(ctx.conn, timezone_name)
    offset = tz.utcoffset(datetime.utcnow())
    offset_str = "UTC{:+02d}:{:02d}".format(int(offset.total_seconds() // 3600), int(offset.total_seconds() // 60 % 60))

    await ctx.reply_ok("System time zone set to {} ({}, {}).\n*Data from OpenStreetMap, queried using Nominatim.*".format(tz.tzname(datetime.utcnow()), offset_str, tz.zone))


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
    
    if not new_avatar_url and ctx.message.attachments[0]:
        new_avatar_url = ctx.message.attachments[0].url

    await system.set_avatar(ctx.conn, new_avatar_url)
    await ctx.reply_ok("System avatar {}.".format("updated" if new_avatar_url else "cleared"))


async def account_link(ctx: CommandContext):
    system = await ctx.ensure_system()
    account_name = ctx.pop_str(CommandError(
        "You must pass an account to link this system to. You can either use a \\@mention, or a raw account ID."))

    # Do the sanity checking here too (despite it being done in System.link_account)
    # Because we want it to be done before the confirmation dialog is shown

    # Find account to link
    linkee = await utils.parse_mention(ctx.client, account_name)
    if not linkee:
        raise CommandError("Account `{}` not found.".format(account_name))

    # Make sure account doesn't already have a system
    account_system = await System.get_by_account(ctx.conn, linkee.id)
    if account_system:
        raise CommandError(AccountAlreadyLinkedError(account_system).message)

    msg = await ctx.reply(
        "{}, please confirm the link by clicking the \u2705 reaction on this message.".format(linkee.mention))
    if not await ctx.confirm_react(linkee, msg):
        raise CommandError("Account link cancelled.")

    await system.link_account(ctx.conn, linkee.id)
    await ctx.reply_ok("Account linked to system.")


async def account_unlink(ctx: CommandContext):
    system = await ctx.ensure_system()

    msg = await ctx.reply("Are you sure you want to unlink this account from your system?")
    if not await ctx.confirm_react(ctx.message.author, msg):
        raise CommandError("Account unlink cancelled.")

    try:
        await system.unlink_account(ctx.conn, ctx.message.author.id)
    except UnlinkingLastAccountError as e:
        raise CommandError(e.message)

    await ctx.reply_ok("Account unlinked.")


async def system_fronter(ctx: CommandContext, system: System):
    embed = await embeds.front_status(ctx, await system.get_latest_switch(ctx.conn))
    await ctx.reply(embed=embed)


async def system_fronthistory(ctx: CommandContext, system: System):
    lines = []
    front_history = await pluralkit.utils.get_front_history(ctx.conn, system.id, count=10)

    if not front_history:
        raise CommandError("You have no logged switches. Use `pk;switch´ to start logging.")

    for i, (timestamp, members) in enumerate(front_history):
        # Special case when no one's fronting
        if len(members) == 0:
            name = "(no fronter)"
        else:
            name = ", ".join([member.name for member in members])

        # Make proper date string
        time_text = ctx.format_time(timestamp)
        rel_text = display_relative(timestamp)

        delta_text = ""
        if i > 0:
            last_switch_time = front_history[i - 1][0]
            delta_text = ", for {}".format(display_relative(timestamp - last_switch_time))
        lines.append("**{}** ({}, {} ago{})".format(name, time_text, rel_text, delta_text))

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


async def system_frontpercent(ctx: CommandContext, system: System):
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

    embed.set_footer(text="Since {} ({} ago)".format(ctx.format_time(span_start),
                                                     display_relative(span_start)))
    await ctx.reply(embed=embed)

async def system_list(ctx: CommandContext, system: System):
    # TODO: refactor this

    all_members = sorted(await system.get_members(ctx.conn), key=lambda m: m.name.lower())
    if ctx.match("full"):
        page_size = 8
        if len(all_members) <= page_size:
            # If we have less than 8 members, don't bother paginating
            await ctx.reply(embed=embeds.member_list_full(system, all_members, 0, page_size))
        else:
            current_page = 0
            msg: discord.Message = None
            while True:
                page_count = math.ceil(len(all_members) / page_size)
                embed = embeds.member_list_full(system, all_members, current_page, page_size)

                # Add reactions for moving back and forth
                if not msg:
                    msg = await ctx.reply(embed=embed)
                    await msg.add_reaction("\u2B05")
                    await msg.add_reaction("\u27A1")
                else:
                    await msg.edit(embed=embed)

                def check(reaction, user):
                    return user.id == ctx.message.author.id and reaction.emoji in ["\u2B05", "\u27A1"]

                try:
                    reaction, _ = await ctx.client.wait_for("reaction_add", timeout=5*60, check=check)
                except asyncio.TimeoutError:
                    return

                if reaction.emoji == "\u2B05":
                    current_page = (current_page - 1) % page_count
                elif reaction.emoji == "\u27A1":
                    current_page = (current_page + 1) % page_count

                # If we can, remove the original reaction from the member
                # Don't bother checking permission if we're in DMs (wouldn't work anyway)
                if ctx.message.guild:
                    if ctx.message.channel.permissions_for(ctx.message.guild.get_member(ctx.client.user.id)).manage_messages:
                        await reaction.remove(ctx.message.author)
    else:

        #Basically same code as above
        #25 members at a time seems handy
        page_size = 25
        if len(all_members) <= page_size:
            # If we have less than 25 members, don't bother paginating
            await ctx.reply(embed=embeds.member_list_short(system, all_members, 0, page_size))
        else:
            current_page = 0
            msg: discord.Message = None
            while True:
                page_count = math.ceil(len(all_members) / page_size)
                embed = embeds.member_list_short(system, all_members, current_page, page_size)

                if not msg:
                    msg = await ctx.reply(embed=embed)
                    await msg.add_reaction("\u2B05")
                    await msg.add_reaction("\u27A1")
                else:
                    await msg.edit(embed=embed)

                def check(reaction, user):
                    return user.id == ctx.message.author.id and reaction.emoji in ["\u2B05", "\u27A1"]

                try:
                    reaction, _ = await ctx.client.wait_for("reaction_add", timeout=5*60, check=check)
                except asyncio.TimeoutError:
                    return

                if reaction.emoji == "\u2B05":
                    current_page = (current_page - 1) % page_count
                elif reaction.emoji == "\u27A1":
                    current_page = (current_page + 1) % page_count

                if ctx.message.guild:
                    if ctx.message.channel.permissions_for(ctx.message.guild.get_member(ctx.client.user.id)).manage_messages:
                        await reaction.remove(ctx.message.author)

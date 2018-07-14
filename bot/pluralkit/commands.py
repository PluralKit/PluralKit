from datetime import datetime
import itertools
import re
from urllib.parse import urlparse

import discord
import humanize

from pluralkit import db
from pluralkit.bot import client, logger
from pluralkit.utils import command, generate_hid, generate_member_info_card, generate_system_info_card, member_command, parse_mention, text_input, get_system_fuzzy, get_member_fuzzy, command_map, make_default_embed, parse_channel_mention, bounds_check_member_name, get_fronters, get_fronter_ids, get_front_history

@command(cmd="system", usage="[system]", description="Shows information about a system.", category="System commands")
async def system_info(conn, message, args):
    if len(args) == 0:
        # Use sender's system
        system = await db.get_system_by_account(conn, message.author.id)

        if system is None:
            return False, "No system is registered to this account."
    else:
        # Look one up
        system = await get_system_fuzzy(conn, args[0])

        if system is None:
            return False, "Unable to find system \"{}\".".format(args[0])

    await client.send_message(message.channel, embed=await generate_system_info_card(conn, system))
    return True


@command(cmd="system new", usage="[name]", description="Registers a new system to this account.", category="System commands")
async def new_system(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is not None:
        return False, "You already have a system registered. To remove your system, use `pk;system remove`, or to unlink your system from this account, use `pk;system unlink`."

    system_name = None
    if len(args) > 0:
        system_name = " ".join(args)

    async with conn.transaction():
        # TODO: figure out what to do if this errors out on collision on generate_hid
        hid = generate_hid()

        system = await db.create_system(conn, system_name=system_name, system_hid=hid)

        # Link account
        await db.link_account(conn, system_id=system["id"], account_id=message.author.id)
        return True, "System registered! To begin adding members, use `pk;member new <name>`."

@command(cmd="system set", usage="<name|description|tag> [value]", description="Edits a system property. Leave [value] blank to clear.", category="System commands")
async def system_set(conn, message, args):
    if len(args) == 0: 
        return False

    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    allowed_properties = ["name", "description", "tag"]
    db_properties = {
        "name": "name",
        "description": "description",
        "tag": "tag"
    }

    prop = args[0]
    if prop not in allowed_properties:
        return False, "Unknown property {}. Allowed properties are {}.".format(prop, ", ".join(allowed_properties))

    if len(args) >= 2:
        value = " ".join(args[1:])

        # Sanity checking
        if prop == "tag":
            # Make sure there are no members which would make the combined length exceed 32
            members_exceeding = await db.get_members_exceeding(conn, system_id=system["id"], length=32 - len(value))
            if len(members_exceeding) > 0:
                # If so, error out and warn
                member_names = ", ".join([member["name"]
                                        for member in members_exceeding])
                logger.debug("Members exceeding combined length with tag '{}': {}".format(value, member_names))
                return False, "The maximum length of a name plus the system tag is 32 characters. The following members would exceed the limit: {}. Please reduce the length of the tag, or rename the members.".format(member_names)
    else:
        # Clear from DB
        value = None

    db_prop = db_properties[prop]
    await db.update_system_field(conn, system_id=system["id"], field=db_prop, value=value)
    
    return True, "{} system {}.".format("Updated" if value else "Cleared", prop)

@command(cmd="system link", usage="<account>", description="Links another account to your system.", category="System commands")
async def system_link(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    if len(args) == 0:
        return False

    # Find account to link
    linkee = await parse_mention(args[0])
    if not linkee:
        return False, "Account not found."

    # Make sure account doesn't already have a system
    account_system = await db.get_system_by_account(conn, linkee.id)
    if account_system:
        return False, "Account is already linked to a system (`{}`)".format(account_system["hid"])

    # Send confirmation message
    msg = await client.send_message(message.channel, "{}, please confirm the link by clicking the ✅ reaction on this message.".format(linkee.mention))
    await client.add_reaction(msg, "✅")
    await client.add_reaction(msg, "❌")

    reaction = await client.wait_for_reaction(emoji=["✅", "❌"], message=msg, user=linkee)
    # If account to be linked confirms...
    if reaction.reaction.emoji == "✅":
        async with conn.transaction():
            # Execute the link
            await db.link_account(conn, system_id=system["id"], account_id=linkee.id)
            return True, "Account linked to system."
    else:
        await client.clear_reactions(msg)
        return False, "Account link cancelled."


@command(cmd="system unlink", description="Unlinks your system from this account. There must be at least one other account linked.", category="System commands")
async def system_unlink(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    # Make sure you can't unlink every account
    linked_accounts = await db.get_linked_accounts(conn, system_id=system["id"])
    if len(linked_accounts) == 1:
        return False, "This is the only account on your system, so you can't unlink it."

    async with conn.transaction():
        await db.unlink_account(conn, system_id=system["id"], account_id=message.author.id)
        return True, "Account unlinked."

@command(cmd="system fronter", usage="[system]", description="Gets the current fronter(s) in the system.", category="Switching commands")
async def system_fronter(conn, message, args):
    if len(args) == 0:
        system = await db.get_system_by_account(conn, message.author.id)

        if system is None:
            return False, "No system is registered to this account."
    else:
        system = await get_system_fuzzy(conn, args[0])
        
        if system is None:
            return False, "Can't find system \"{}\".".format(args[0])
    
    fronters, timestamp = await get_fronters(conn, system_id=system["id"])
    fronter_names = [member["name"] for member in fronters]

    embed = make_default_embed(None)

    if len(fronter_names) == 0:
        embed.add_field(name="Current fronter", value="*nobody*")
    elif len(fronter_names) == 1:
        embed.add_field(name="Current fronter", value=fronter_names[0])
    else:
        embed.add_field(name="Current fronters", value=", ".join(fronter_names))

    if timestamp:
        embed.add_field(name="Since", value="{} ({})".format(timestamp.isoformat(sep=" ", timespec="seconds"), humanize.naturaltime(timestamp)))
    return True, embed

@command(cmd="system fronthistory", usage="[system]", description="Shows the past 10 switches in the system.", category="Switching commands")
async def system_fronthistory(conn, message, args):
    if len(args) == 0:
        system = await db.get_system_by_account(conn, message.author.id)

        if system is None:
            return False, "No system is registered to this account."
    else:
        system = await get_system_fuzzy(conn, args[0])
        
        if system is None:
            return False, "Can't find system \"{}\".".format(args[0])
    
    lines = []
    for timestamp, members in await get_front_history(conn, system["id"], count=10):
        # Special case when no one's fronting
        if len(members) == 0:
            name = "*nobody*"
        else:
            name = ", ".join([member["name"] for member in members])

        # Make proper date string
        time_text = timestamp.isoformat(sep=" ", timespec="seconds")
        rel_text = humanize.naturaltime(timestamp)

        lines.append("**{}** ({}, {})".format(name, time_text, rel_text))

    embed = make_default_embed("\n".join(lines) or "(none)")
    embed.title = "Past switches"
    return True, embed


@command(cmd="system delete", description="Deletes your system from the database ***permanently***.", category="System commands")
async def system_delete(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    await client.send_message(message.channel, "Are you sure you want to delete your system? If so, reply to this message with the system's ID (`{}`).".format(system["hid"]))

    msg = await client.wait_for_message(author=message.author, channel=message.channel, timeout=60.0)
    if msg and msg.content == system["hid"]:
        await db.remove_system(conn, system_id=system["id"])
        return True, "System deleted."
    else:
        return True, "System deletion cancelled."

@member_command(cmd="member", description="Shows information about a system member.", system_only=False, category="Member commands")
async def member_info(conn, message, member, args):
    await client.send_message(message.channel, embed=await generate_member_info_card(conn, member))
    return True

@command(cmd="member new", usage="<name>", description="Adds a new member to your system.", category="Member commands")
async def new_member(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    if len(args) == 0:
        return False

    name = " ".join(args)
    bounds_error = bounds_check_member_name(name, system["tag"])
    if bounds_error:
        return False, bounds_error

    async with conn.transaction():
        # TODO: figure out what to do if this errors out on collision on generate_hid
        hid = generate_hid()

        # Insert member row
        await db.create_member(conn, system_id=system["id"], member_name=name, member_hid=hid)
        return True, "Member \"{}\" (`{}`) registered!".format(name, hid)


@member_command(cmd="member set", usage="<name|description|color|pronouns|birthdate|avatar> [value]", description="Edits a member property. Leave [value] blank to clear.", category="Member commands")
async def member_set(conn, message, member, args):
    if len(args) == 0: 
        return False

    allowed_properties = ["name", "description", "color", "pronouns", "birthdate", "avatar"]
    db_properties = {
        "name": "name",
        "description": "description",
        "color": "color",
        "pronouns": "pronouns",
        "birthdate": "birthday",
        "avatar": "avatar_url"
    }

    prop = args[0]
    if prop not in allowed_properties:
        return False, "Unknown property {}. Allowed properties are {}.".format(prop, ", ".join(allowed_properties))

    if len(args) >= 2:
        value = " ".join(args[1:])

        # Sanity/validity checks and type conversions
        if prop == "name":
            system = await db.get_system(conn, member["system"])
            bounds_error = bounds_check_member_name(value, system["tag"])
            if bounds_error:
                return False, bounds_error

        if prop == "color":
            match = re.fullmatch("#?([0-9A-Fa-f]{6})", value)
            if not match:
                return False, "Color must be a valid hex color (eg. #ff0000)"

            value = match.group(1).lower()
        
        if prop == "birthdate":
            try:
                value = datetime.strptime(value, "%Y-%m-%d").date()
            except ValueError:
                return False, "Invalid date. Date must be in ISO-8601 format (eg. 1999-07-25)."

        if prop == "avatar":
            user = await parse_mention(value)
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
                    return False, "Invalid URL."
    else:
        # Can't clear member name
        if prop == "name":
            return False, "Can't clear member name."        

        # Clear from DB
        value = None

    db_prop = db_properties[prop]
    await db.update_member_field(conn, member_id=member["id"], field=db_prop, value=value)
    
    if prop == "avatar":
        response = make_default_embed("Updated {}'s avatar.".format(member["name"])).set_image(url=value)
    else:
        response = "{} {}'s {}.".format("Updated" if value else "Cleared", member["name"], prop)
    return True, response

@member_command(cmd="member proxy", usage="[example]", description="Updates a member's proxy settings. Needs an \"example\" proxied message containing the string \"text\" (eg. [text], |text|, etc).", category="Member commands")
async def member_proxy(conn, message, member, args):
    if len(args) == 0:
        prefix, suffix = None, None
    else:
        # Sanity checking
        example = " ".join(args)
        if "text" not in example:
            return False, "Example proxy message must contain the string 'text'."

        if example.count("text") != 1:
            return False, "Example proxy message must contain the string 'text' exactly once."

        # Extract prefix and suffix
        prefix = example[:example.index("text")].strip()
        suffix = example[example.index("text")+4:].strip()
        logger.debug(
            "Matched prefix '{}' and suffix '{}'".format(prefix, suffix))

        # DB stores empty strings as None, make that work
        if not prefix:
            prefix = None
        if not suffix:
            suffix = None

    async with conn.transaction():
        await db.update_member_field(conn, member_id=member["id"], field="prefix", value=prefix)
        await db.update_member_field(conn, member_id=member["id"], field="suffix", value=suffix)
        return True, "Proxy settings updated." if prefix or suffix else "Proxy settings cleared."

@member_command("member delete", description="Deletes a member from your system ***permanently***.", category="Member commands")
async def member_delete(conn, message, member, args):
    await client.send_message(message.channel, "Are you sure you want to delete {}? If so, reply to this message with the member's ID (`{}`).".format(member["name"], member["hid"]))

    msg = await client.wait_for_message(author=message.author, channel=message.channel, timeout=60.0)
    if msg and msg.content == member["hid"]:
        await db.delete_member(conn, member_id=member["id"])
        return True, "Member deleted."
    else:
        return True, "Member deletion cancelled."

@command(cmd="message", usage="<id>", description="Shows information about a proxied message. Requires the message ID.", category="Message commands")
async def message_info(conn, message, args):
    try:
        mid = int(args[0])
    except ValueError:
        return False

    # Find the message in the DB
    message_row = await db.get_message(conn, mid)
    if not message_row:
        return False, "Message not found."

    # Find the actual message object
    channel = client.get_channel(str(message_row["channel"]))
    message = await client.get_message(channel, str(message_row["mid"]))

    # Get the original sender of the message
    original_sender = await client.get_user_info(str(message_row["sender"]))

    # Get sender member and system
    member = await db.get_member(conn, message_row["member"])
    system = await db.get_system(conn, member["system"])

    embed = discord.Embed()
    embed.timestamp = message.timestamp
    embed.colour = discord.Colour.blue()

    if system["name"]:
        system_value = "`{}`: {}".format(system["hid"], system["name"])
    else:
        system_value = "`{}`".format(system["hid"])
    embed.add_field(name="System", value=system_value)
    embed.add_field(name="Member", value="`{}`: {}".format(
        member["hid"], member["name"]))
    embed.add_field(name="Sent by", value="{}#{}".format(
        original_sender.name, original_sender.discriminator))
    embed.add_field(name="Content", value=message.clean_content, inline=False)

    embed.set_author(name=member["name"], url=member["avatar_url"])

    await client.send_message(message.channel, embed=embed)
    return True

@command(cmd="switch", usage="<name|id> [name|id]...", description="Registers a switch and changes the current fronter.", category="Switching commands")
async def switch_member(conn, message, args):
    if len(args) == 0:
        return False

    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    members = []
    for member_name in args:
        # Find the member
        member = await get_member_fuzzy(conn, system["id"], member_name)
        if not member:
            return False, "Couldn't find member \"{}\".".format(member_name)
        members.append(member)
    
    # Compare requested switch IDs and existing fronter IDs to check for existing switches
    # Lists, because order matters, it makes sense to just swap fronters
    member_ids = [member["id"] for member in members]
    fronter_ids = (await get_fronter_ids(conn, system["id"]))[0]
    if member_ids == fronter_ids:
        if len(members) == 1:
            return False, "{} is already fronting.".format(members[0]["name"])
        return False, "Members {} are already fronting.".format(", ".join([m["name"] for m in members]))
    
    # Log the switch
    async with conn.transaction():
        switch_id = await db.add_switch(conn, system_id=system["id"])
        for member in members:
            await db.add_switch_member(conn, switch_id=switch_id, member_id=member["id"])

    if len(members) == 1:
        return True, "Switch registered. Current fronter is now {}.".format(member["name"])
    else:
        return True, "Switch registered. Current fronters are now {}.".format(", ".join([m["name"] for m in members]))

@command(cmd="switch out", description="Registers a switch out, and leaves current fronter blank.", category="Switching commands")
async def switch_out(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    # Get current fronters
    fronters, _ = await get_fronter_ids(conn, system_id=system["id"])
    if not fronters:
        return False, "There's already no one in front."

    # Log it, and don't log any members
    await db.add_switch(conn, system_id=system["id"])
    return True, "Switch-out registered."

@command(cmd="mod log", usage="[channel]", description="Sets the bot to log events to a specified channel. Leave blank to disable.", category="Moderation commands")
async def set_log(conn, message, args):
    if not message.author.server_permissions.administrator:
        return False, "You must be a server administrator to use this command."
    
    server = message.server
    if len(args) == 0:
        channel_id = None
    else:
        channel = parse_channel_mention(args[0], server=server)
        if not channel:
            return False, "Channel not found."
        channel_id = channel.id

    await db.update_server(conn, server.id, logging_channel_id=channel_id)
    return True, "Updated logging channel." if channel_id else "Cleared logging channel."

def make_help(cmds):
    embed = discord.Embed()
    embed.colour = discord.Colour.blue()
    embed.title = "PluralKit Help"
    embed.set_footer(
        text="<> denotes mandatory arguments, [] denotes optional arguments")

    for cmd, subcommands in cmds:
        for subcmd, (_, usage, description) in subcommands.items():
            embed.add_field(name="{} {} {}".format(
                cmd, subcmd or "", usage or ""), value=description, inline=False)
    return embed

@command(cmd="help", usage="[system|member|proxy|switch|mod]", description="Shows help messages.")
async def show_help(conn, message, args):
    embed = make_default_embed("")
    embed.title = "PluralKit Help"

    category = args[0] if len(args) > 0 else None

    from pluralkit.help import help_pages
    if category in help_pages:
        for name, text in help_pages[category]:
            if name:
                embed.add_field(name=name, value=text)
            else:
                embed.description = text
    else:
        return False

    return True, embed

@command(cmd="import tupperware", description="Import data from Tupperware.")
async def import_tupperware(conn, message, args):
    tupperware_member = message.server.get_member("431544605209788416") or message.server.get_member("433916057053560832")

    if not tupperware_member:
        return False, "This command only works in a server where the Tupperware bot is also present."

    channel_permissions = message.channel.permissions_for(tupperware_member)
    if not (channel_permissions.read_messages and channel_permissions.send_messages):
        return False, "This command only works in a channel where the Tupperware bot has read/send access."

    await client.send_message(message.channel, embed=make_default_embed("Please reply to this message with `tul!list` (or the server equivalent)."))
    
    tw_msg = await client.wait_for_message(author=tupperware_member, channel=message.channel, timeout=60.0)
    if not tw_msg:
        return False, "Tupperware import timed out."

    logger.debug("Importing from Tupperware...")

    # Create new (nameless) system if there isn't any registered
    system = await db.get_system_by_account(conn, message.author.id)
    if system is None:
        hid = generate_hid()
        logger.debug("Creating new system (hid={})...".format(hid))
        system = await db.create_system(conn, system_name=None, system_hid=hid)
        await db.link_account(conn, system_id=system["id"], account_id=message.author.id)

    embed = tw_msg.embeds[0]
    for field in embed["fields"]:
        name = field["name"]
        lines = field["value"].split("\n")

        member_prefix = None
        member_suffix = None
        member_avatar = None
        member_birthdate = None
        member_description = None

        for line in lines:
            if line.startswith("Brackets:"):
                brackets = line[len("Brackets: "):]
                member_prefix = brackets[:brackets.index("text")].strip() or None
                member_suffix = brackets[brackets.index("text")+4:].strip() or None
            elif line.startswith("Avatar URL: "):
                url = line[len("Avatar URL: "):]
                member_avatar = url
            elif line.startswith("Birthday: "):
                bday_str = line[len("Birthday: "):]
                bday = datetime.strptime(bday_str, "%a %b %d %Y")
                if bday:
                    member_birthdate = bday.date()
            else:
                member_description = line

        existing_member = await db.get_member_by_name(conn, system_id=system["id"], member_name=name)
        if not existing_member:
            hid = generate_hid()
            logger.debug("Creating new member {} (hid={})...".format(name, hid))
            existing_member = await db.create_member(conn, system_id=system["id"], member_name=name, member_hid=hid)
        
        logger.debug("Updating fields...")
        await db.update_member_field(conn, member_id=existing_member["id"], field="prefix", value=member_prefix)
        await db.update_member_field(conn, member_id=existing_member["id"], field="suffix", value=member_suffix)
        await db.update_member_field(conn, member_id=existing_member["id"], field="avatar_url", value=member_avatar)
        await db.update_member_field(conn, member_id=existing_member["id"], field="birthday", value=member_birthdate)
        await db.update_member_field(conn, member_id=existing_member["id"], field="description", value=member_description)
    
    return True, "System information imported. Try using `pk;system` now."
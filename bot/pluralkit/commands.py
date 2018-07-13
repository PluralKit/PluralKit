from datetime import datetime
import re
from urllib.parse import urlparse

import discord
import humanize

from pluralkit import db
from pluralkit.bot import client, logger
from pluralkit.utils import command, generate_hid, generate_member_info_card, generate_system_info_card, member_command, parse_mention, text_input, get_system_fuzzy, get_member_fuzzy, command_map, make_default_embed, parse_channel_mention, bounds_check_member_name

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

@command(cmd="system fronter", usage="[system]", description="Gets the current fronter in the system.", category="Switching commands")
async def system_fronter(conn, message, args):
    if len(args) == 0:
        system = await db.get_system_by_account(conn, message.author.id)

        if system is None:
            return False, "No system is registered to this account."
    else:
        system = await get_system_fuzzy(conn, args[0])
        
        if system is None:
            return False, "Can't find system \"{}\".".format(args[0])
    
    current_fronter = await db.current_fronter(conn, system_id=system["id"])
    if not current_fronter:
        return True, make_default_embed(None).add_field(name="Current fronter", value="*(nobody)*")

    fronter_name = "*(nobody)*"
    if current_fronter["member"]:
        member = await db.get_member(conn, member_id=current_fronter["member"])
        fronter_name = member["name"]
    if current_fronter["member_del"]:
        fronter_name = "*(deleted member)*"

    since = current_fronter["timestamp"]

    embed = make_default_embed(None)
    embed.add_field(name="Current fronter", value=fronter_name)
    embed.add_field(name="Since", value="{} ({})".format(since.isoformat(sep=" ", timespec="seconds"), humanize.naturaltime(since)))
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
    
    switches = await db.past_fronters(conn, system_id=system["id"], amount=10)
    
    lines = []
    for switch in switches:
        since = switch["timestamp"]
        time_text = since.isoformat(sep=" ", timespec="seconds")
        rel_text = humanize.naturaltime(since)

        lines.append("**{}** ({}, at {})".format(switch["name"], time_text, rel_text))

    embed = make_default_embed("\n".join(lines))
    embed.title = "Past switches"
    return True, embed


@command(cmd="system delete", description="Deletes your system from the database ***permanently***.", category="System commands")
async def system_delete(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    await client.send_message(message.channel, "Are you sure you want to delete your system? If so, reply to this message with the system's ID (`{}`).".format(system["hid"]))

    msg = await client.wait_for_message(author=message.author, channel=message.channel)
    if msg.content == system["hid"]:
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
            user = await parse_mention(args[0])
            if user:
                # Set the avatar to the mentioned user's avatar
                # Discord doesn't like webp, but also hosts png alternatives
                value = user.avatar_url.replace(".webp", ".png")
            else:
                # Validate URL
                u = urlparse(args[0])
                if u.scheme in ["http", "https"] and u.netloc and u.path:
                    value = args[0]
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

@command(cmd="switch", usage="<name|id>", description="Registers a switch and changes the current fronter.", category="Switching commands")
async def switch_member(conn, message, args):
    if len(args) == 0:
        return False

    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    # Find the member
    member = await get_member_fuzzy(conn, system["id"], " ".join(args))
    if not member:
        return False, "Couldn't find member \"{}\".".format(args[0])

    # Get current fronter
    current_fronter = await db.current_fronter(conn, system_id=system["id"])
    if current_fronter and current_fronter["member"] == member["id"]:
        return False, "Member \"{}\" is already fronting.".format(member["name"])
    
    # Log the switch
    await db.add_switch(conn, system_id=system["id"], member_id=member["id"])
    return True, "Switch registered. Current fronter is now {}.".format(member["name"])

@command(cmd="switch out", description="Registers a switch out, and leaves current fronter blank.", category="Switching commands")
async def switch_out(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    # Get current fronter
    current_fronter = await db.current_fronter(conn, system_id=system["id"])
    if not current_fronter or not current_fronter["member"]:
        return False, "There's already no one in front."

    # Log it
    await db.add_switch(conn, system_id=system["id"], member_id=None)
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

@command(cmd="help", usage="[category]", description="Shows this help message.")
async def show_help(conn, message, args):
    embed = make_default_embed("")
    embed.title = "PluralKit Help"

    categories = {}
    prefix = "pk;"
    for cmd, (_, usage, description, category) in command_map.items():
        if category is None:
            continue

        if category not in categories:
            categories[category] = []
        
        categories[category].append("**{}{} {}** - {}".format(prefix, cmd, usage or "", description or ""))

    for category, lines in categories.items():
        embed.add_field(name=category, value="\n".join(lines), inline=False)

    return True, embed

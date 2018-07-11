from datetime import datetime
import re
from urllib.parse import urlparse

import discord

from pluralkit import db
from pluralkit.bot import client, logger
from pluralkit.utils import command, generate_hid, generate_member_info_card, generate_system_info_card, member_command, parse_mention, text_input, get_system_fuzzy, get_member_fuzzy, command_map

@command(cmd="pk;system", subcommand=None, description="Shows information about your system.")
async def this_system_info(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is None:
        return False, "No system is registered to this account."

    await client.send_message(message.channel, embed=await generate_system_info_card(conn, system))
    return True

@command(cmd="pk;system", subcommand="new", usage="[name]", description="Registers a new system to this account.")
async def new_system(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)

    if system is not None:
        return False, "You already have a system registered. To remove your system, use `pk;system remove`, or to unlink your system from this account, use `pk;system unlink`."

    system_name = None
    if len(args) > 2:
        system_name = " ".join(args[2:])

    async with conn.transaction():
        # TODO: figure out what to do if this errors out on collision on generate_hid
        hid = generate_hid()

        system = await db.create_system(conn, system_name=system_name, system_hid=hid)

        # Link account
        await db.link_account(conn, system_id=system["id"], account_id=message.author.id)
        return True, "System registered! To begin adding members, use `pk;member new <name>`."

@command(cmd="pk;system", subcommand="info", usage="[system]", description="Shows information about a system.")
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

@command(cmd="pk;system", subcommand="name", usage="[name]", description="Renames your system. Leave blank to clear.")
async def system_name(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)
    
    if system is None:
        return False, "No system is registered to this account."

    if len(args) == 0:
        new_name = None
    else:
        new_name = " ".join(args)

    async with conn.transaction():
        await db.update_system_field(conn, system_id=system["id"], field="name", value=new_name)
        return True, "Name updated to {}.".format(new_name) if new_name else "Name cleared."

@command(cmd="pk;system", subcommand="description", usage="[clear]", description="Updates your system description. Add \"clear\" to clear.")
async def system_description(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)
    
    if system is None:
        return False, "No system is registered to this account."

    # If "clear" in args, clear
    if len(args) > 0 and args[0] == "clear":
        new_description = None
    else:
        new_description = await text_input(message, "your system")

        if not new_description:
            return True, "Description update cancelled."

    async with conn.transaction():
        await db.update_system_field(conn, system_id=system["id"], field="description", value=new_description)
        return True, "Description set." if new_description else "Description cleared."

@command(cmd="pk;system", subcommand="tag", usage="[tag]", description="Updates your system tag. Leave blank to clear.")
async def system_tag(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)
    
    if system is None:
        return False, "No system is registered to this account."

    if len(args) == 0:
        tag = None
    else:
        tag = " ".join(args)
        max_length = 32

        # Make sure there are no members which would make the combined length exceed 32
        members_exceeding = await db.get_members_exceeding(conn, system_id=system["id"], length=max_length - len(tag))
        if len(members_exceeding) > 0:
            # If so, error out and warn
            member_names = ", ".join([member["name"] for member in members_exceeding])
            logger.debug("Members exceeding combined length with tag '{}': {}".format(tag, member_names))
            return False, "The maximum length of a name plus the system tag is 32 characters. The following members would exceed the limit: {}. Please reduce the length of the tag, or rename the members.".format(member_names)

    async with conn.transaction():
        await db.update_system_field(conn, system_id=system["id"], field="tag", value=tag)
    
    return True, "Tag updated to {}.".format(tag) if tag else "Tag cleared."

@command(cmd="pk;system", subcommand="remove", description="Removes your system ***permanently***.")
async def system_remove(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)
    
    if system is None:
        return False, "No system is registered to this account."

    await client.send_message(message.channel, "Are you sure you want to remove your system? If so, reply to this message with the system's ID (`{}`).".format(system["hid"]))
    
    msg = await client.wait_for_message(author=message.author, channel=message.channel)
    if msg.content == system["hid"]:
        await db.remove_system(conn, system_id=system["id"])
        return True, "System removed."
    else:
        return True, "System removal cancelled."


@command(cmd="pk;system", subcommand="link", usage="<account>", description="Links another account to your system.")
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


@command(cmd="pk;system", subcommand="unlink", description="Unlinks your system from this account. There must be at least one other account linked.")
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

@command(cmd="pk;member", subcommand="new", usage="<name>", description="Adds a new member to your system.")
async def new_member(conn, message, args):
    system = await db.get_system_by_account(conn, message.author.id)
    
    if system is None:
        return False, "No system is registered to this account."

    if len(args) == 0:
        return False

    name = " ".join(args)
    async with conn.transaction():
        # TODO: figure out what to do if this errors out on collision on generate_hid
        hid = generate_hid()

        # Insert member row
        await db.create_member(conn, system_id=system["id"], member_name=name, member_hid=hid)
        return True, "Member \"{}\" (`{}`) registered!".format(name, hid)

@member_command(cmd="pk;member", subcommand="info", description="Shows information about a system member.", system_only=False)
async def member_info(conn, message, member, args):
    await client.send_message(message.channel, embed=await generate_member_info_card(conn, member))
    return True

@member_command(cmd="pk;member", subcommand="color", usage="[color]", description="Updates a member's associated color. Leave blank to clear.")
async def member_color(conn, message, member, args):
    if len(args) == 0:
        color = None
    else:
        match = re.fullmatch("#?([0-9a-f]{6})", args[0])
        if not match:
            return False, "Color must be a valid hex color (eg. #ff0000)"

        color = match.group(1)

    async with conn.transaction():
        await db.update_member_field(conn, member_id=member["id"], field="color", value=color)
        return True, "Color updated to #{}.".format(color) if color else "Color cleared."
        
@member_command(cmd="pk;member", subcommand="pronouns", usage="[pronouns]", description="Updates a member's pronouns. Leave blank to clear.")
async def member_pronouns(conn, message, member, args):
    if len(args) == 0:
        pronouns = None
    else:
        pronouns = " ".join(args)

    async with conn.transaction():
        await db.update_member_field(conn, member_id=member["id"], field="pronouns", value=pronouns)
        return True, "Pronouns set to {}".format(pronouns) if pronouns else "Pronouns cleared."

@member_command(cmd="pk;member", subcommand="birthdate", usage="[birthdate]", description="Updates a member's birthdate. Must be in ISO-8601 format (eg. 1999-07-25). Leave blank to clear.")
async def member_birthday(conn, message, member, args):
    if len(args) == 0:
        new_date = None
    else:
        # Parse date 
        try:
            new_date = datetime.strptime(args[0], "%Y-%m-%d").date()
        except ValueError:
            return False, "Invalid date. Date must be in ISO-8601 format (eg. 1999-07-25)."

    async with conn.transaction():
        await db.update_member_field(conn, member_id=member["id"], field="birthday", value=new_date)
        return True, "Birthdate set to {}".format(new_date) if new_date else "Birthdate cleared."

@member_command(cmd="pk;member", subcommand="description", description="Updates a member's description. Add \"clear\" to clear.")
async def member_description(conn, message, member, args):
    if len(args) > 0 and args[0] == "clear":
        new_description = None
    else:
        new_description = await text_input(message, member["name"])

        if not new_description:
            return True, "Description update cancelled."

    async with conn.transaction():
        await db.update_member_field(conn, member_id=member["id"], field="description", value=new_description)
        return True, "Description set." if new_description else "Description cleared."
        
@member_command(cmd="pk;member", subcommand="remove", description="Removes a member from your system.")
async def member_remove(conn, message, member, args):
    await client.send_message(message.channel, "Are you sure you want to remove {}? If so, reply to this message with the member's name.".format(member["name"]))
    
    msg = await client.wait_for_message(author=message.author, channel=message.channel)
    if msg.content == member["name"]:
        await db.delete_member(conn, member_id=member["id"])
        return True, "Member removed."
    else:
        return True, "Member removal cancelled."

@member_command(cmd="pk;member", subcommand="avatar", usage="[user|url]", description="Updates a member's avatar. Can be an account mention (which will use that account's avatar), or a link to an image. Leave blank to clear.")
async def member_avatar(conn, message, member, args):
    if len(args) == 0:
        avatar_url = None
    else:
        user = await parse_mention(args[0])
        if user:
            # Set the avatar to the mentioned user's avatar
            # Discord doesn't like webp, but also hosts png alternatives
            avatar_url = user.avatar_url.replace(".webp", ".png")
        else:
            # Validate URL
            u = urlparse(args[0])
            if u.scheme in ["http", "https"] and u.netloc and u.path:
                avatar_url = args[0]
            else:
                return False, "Invalid URL."
    
    async with conn.transaction():
        await db.update_member_field(conn, member_id=member["id"], field="avatar_url", value=avatar_url)
        return True, "Avatar set." if avatar_url else "Avatar cleared."
        

@member_command(cmd="pk;member", subcommand="proxy", usage="[example]", description="Updates a member's proxy settings. Needs an \"example\" proxied message containing the string \"text\" (eg. [text], |text|, etc).")
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
        logger.debug("Matched prefix '{}' and suffix '{}'".format(prefix, suffix))

        # DB stores empty strings as None, make that work
        if not prefix:
            prefix = None
        if not suffix:
            suffix = None

    async with conn.transaction():
        await db.update_member_field(conn, member_id=member["id"], field="prefix", value=prefix)
        await db.update_member_field(conn, member_id=member["id"], field="suffix", value=suffix)
        return True, "Proxy settings updated." if prefix or suffix else "Proxy settings cleared."

@command(cmd="pk;message", subcommand=None, usage="<id>", description="Shows information about a proxied message. Requires the message ID.")
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
    embed.add_field(name="Member", value="`{}`: {}".format(member["hid"], member["name"]))
    embed.add_field(name="Sent by", value="{}#{}".format(original_sender.name, original_sender.discriminator))
    embed.add_field(name="Content", value=message.clean_content, inline=False)

    embed.set_author(name=member["name"], url=member["avatar_url"])

    await client.send_message(message.channel, embed=embed)
    return True

@command(cmd="pk;help", subcommand=None, usage="[system|member|message]", description="Shows this help message.")
async def show_help(conn, message, args):
    embed = discord.Embed()
    embed.colour = discord.Colour.blue()
    embed.title = "PluralKit Help"
    embed.set_footer(text="<> denotes mandatory arguments, [] denotes optional arguments")

    if len(args) > 0 and ("pk;" + args[0]) in command_map:
        cmds = ["", ("pk;" + args[0], command_map["pk;" + args[0]])]
    else:
        cmds = command_map.items()

    for cmd, subcommands in cmds:
        for subcmd, (_, usage, description) in subcommands.items():
            embed.add_field(name="{} {} {}".format(cmd, subcmd or "", usage or ""), value=description, inline=False)
    
    await client.send_message(message.channel, embed=embed)
    return True
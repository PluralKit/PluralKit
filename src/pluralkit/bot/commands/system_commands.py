import logging
from typing import List
from urllib.parse import urlparse

import humanize

from pluralkit.bot import utils
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")

@command(cmd="system", usage="[system]", description="Shows information about a system.", category="System commands", system_required=False)
async def system_info(ctx: CommandContext, args: List[str]):
    if len(args) == 0:
        if not ctx.system:
            raise NoSystemRegistered()
        system = ctx.system
    else:
        # Look one up
        system = await utils.get_system_fuzzy(ctx.conn, ctx.client, args[0])

        if system is None:
            raise CommandError("Unable to find system \"{}\".".format(args[0]))
    
    await ctx.reply(embed=await utils.generate_system_info_card(ctx.conn, ctx.client, system))

@command(cmd="system new", usage="[name]", description="Registers a new system to this account.", category="System commands", system_required=False)
async def new_system(ctx: CommandContext, args: List[str]):
    if ctx.system:
        raise CommandError("You already have a system registered. To delete your system, use `pk;system delete`, or to unlink your system from this account, use `pk;system unlink`.")

    system_name = None
    if len(args) > 0:
        system_name = " ".join(args)

    async with ctx.conn.transaction():
        # TODO: figure out what to do if this errors out on collision on generate_hid
        hid = utils.generate_hid()

        system = await db.create_system(ctx.conn, system_name=system_name, system_hid=hid)

        # Link account
        await db.link_account(ctx.conn, system_id=system.id, account_id=ctx.message.author.id)
        return "System registered! To begin adding members, use `pk;member new <name>`."

@command(cmd="system set", usage="<name|description|tag|avatar> [value]", description="Edits a system property. Leave [value] blank to clear.", category="System commands")
async def system_set(ctx: CommandContext, args: List[str]):
    if len(args) == 0: 
        raise InvalidCommandSyntax()

    allowed_properties = ["name", "description", "tag", "avatar"]
    db_properties = {
        "name": "name",
        "description": "description",
        "tag": "tag",
        "avatar": "avatar_url"
    }

    prop = args[0]
    if prop not in allowed_properties:
        raise CommandError("Unknown property {}. Allowed properties are {}.".format(prop, ", ".join(allowed_properties)))

    if len(args) >= 2:
        value = " ".join(args[1:])
        # Sanity checking
        if prop == "tag":
            if len(value) > 32:
                raise CommandError("Can't have system tag longer than 32 characters.")

            # Make sure there are no members which would make the combined length exceed 32
            members_exceeding = await db.get_members_exceeding(ctx.conn, system_id=ctx.system.id, length=32 - len(value) - 1)
            if len(members_exceeding) > 0:
                # If so, error out and warn
                member_names = ", ".join([member.name
                                        for member in members_exceeding])
                logger.debug("Members exceeding combined length with tag '{}': {}".format(value, member_names))
                raise CommandError("The maximum length of a name plus the system tag is 32 characters. The following members would exceed the limit: {}. Please reduce the length of the tag, or rename the members.".format(member_names))

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
                    raise CommandError("Invalid URL.")
    else:
        # Clear from DB
        value = None

    db_prop = db_properties[prop]
    await db.update_system_field(ctx.conn, system_id=ctx.system.id, field=db_prop, value=value)
    
    response = utils.make_default_embed("{} system {}.".format("Updated" if value else "Cleared", prop))
    if prop == "avatar" and value:
        response.set_image(url=value)
    return response

@command(cmd="system link", usage="<account>", description="Links another account to your system.", category="System commands")
async def system_link(ctx: CommandContext, args: List[str]):
    if len(args) == 0:
        raise InvalidCommandSyntax()

    # Find account to link
    linkee = await utils.parse_mention(ctx.client, args[0])
    if not linkee:
        raise CommandError("Account not found.")

    # Make sure account doesn't already have a system
    account_system = await db.get_system_by_account(ctx.conn, linkee.id)
    if account_system:
        raise CommandError("Account is already linked to a system (`{}`)".format(account_system.hid))

    # Send confirmation message
    msg = await ctx.reply("{}, please confirm the link by clicking the ✅ reaction on this message.".format(linkee.mention))
    await ctx.client.add_reaction(msg, "✅")
    await ctx.client.add_reaction(msg, "❌")

    reaction = await ctx.client.wait_for_reaction(emoji=["✅", "❌"], message=msg, user=linkee, timeout=60.0)
    # If account to be linked confirms...
    if not reaction:
        raise CommandError("Account link timed out.")
    if not reaction.reaction.emoji == "✅":
        raise CommandError("Account link cancelled.")

    await db.link_account(ctx.conn, system_id=ctx.system.id, account_id=linkee.id)
    return "Account linked to system."

@command(cmd="system unlink", description="Unlinks your system from this account. There must be at least one other account linked.", category="System commands")
async def system_unlink(ctx: CommandContext, args: List[str]):
    # Make sure you can't unlink every account
    linked_accounts = await db.get_linked_accounts(ctx.conn, system_id=ctx.system.id)
    if len(linked_accounts) == 1:
        raise CommandError("This is the only account on your system, so you can't unlink it.")

    await db.unlink_account(ctx.conn, system_id=ctx.system.id, account_id=ctx.message.author.id)
    return "Account unlinked."

@command(cmd="system fronter", usage="[system]", description="Gets the current fronter(s) in the system.", category="Switching commands", system_required=False)
async def system_fronter(ctx: CommandContext, args: List[str]):
    if len(args) == 0:
        if not ctx.system:
            raise NoSystemRegistered()
        system = ctx.system
    else:
        system = await utils.get_system_fuzzy(ctx.conn, ctx.client, args[0])
        
        if system is None:
            raise CommandError("Can't find system \"{}\".".format(args[0]))
    
    fronters, timestamp = await utils.get_fronters(ctx.conn, system_id=system.id)
    fronter_names = [member.name for member in fronters]

    embed = utils.make_default_embed(None)

    if len(fronter_names) == 0:
        embed.add_field(name="Current fronter", value="(no fronter)")
    elif len(fronter_names) == 1:
        embed.add_field(name="Current fronter", value=fronter_names[0])
    else:
        embed.add_field(name="Current fronters", value=", ".join(fronter_names))

    if timestamp:
        embed.add_field(name="Since", value="{} ({})".format(timestamp.isoformat(sep=" ", timespec="seconds"), humanize.naturaltime(timestamp)))
    return embed

@command(cmd="system fronthistory", usage="[system]", description="Shows the past 10 switches in the system.", category="Switching commands", system_required=False)
async def system_fronthistory(ctx: CommandContext, args: List[str]):
    if len(args) == 0:
        if not ctx.system:
            raise NoSystemRegistered()
        system = ctx.system
    else:
        system = await utils.get_system_fuzzy(ctx.conn, ctx.client, args[0])
        
        if system is None:
            raise CommandError("Can't find system \"{}\".".format(args[0]))
    
    lines = []
    front_history = await utils.get_front_history(ctx.conn, system.id, count=10)
    for i, (timestamp, members) in enumerate(front_history):
        # Special case when no one's fronting
        if len(members) == 0:
            name = "(no fronter)"
        else:
            name = ", ".join([member.name for member in members])

        # Make proper date string
        time_text = timestamp.isoformat(sep=" ", timespec="seconds")
        rel_text = humanize.naturaltime(timestamp)

        delta_text = ""
        if i > 0:
            last_switch_time = front_history[i-1][0]
            delta_text = ", for {}".format(humanize.naturaldelta(timestamp - last_switch_time))
        lines.append("**{}** ({}, {}{})".format(name, time_text, rel_text, delta_text))

    embed = utils.make_default_embed("\n".join(lines) or "(none)")
    embed.title = "Past switches"
    return embed


@command(cmd="system delete", description="Deletes your system from the database ***permanently***.", category="System commands")
async def system_delete(ctx: CommandContext, args: List[str]):
    await ctx.reply("Are you sure you want to delete your system? If so, reply to this message with the system's ID (`{}`).".format(ctx.system.hid))

    msg = await ctx.client.wait_for_message(author=ctx.message.author, channel=ctx.message.channel, timeout=60.0)
    if msg and msg.content == ctx.system.hid:
        await db.remove_system(ctx.conn, system_id=ctx.system.id)
        return "System deleted."
    else:
        return "System deletion cancelled."
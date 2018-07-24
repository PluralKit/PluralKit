import logging
import re
from datetime import datetime
from typing import List
from urllib.parse import urlparse

from pluralkit.bot import utils
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")

@member_command(cmd="member", description="Shows information about a system member.", system_only=False, category="Member commands")
async def member_info(ctx: MemberCommandContext, args: List[str]):
    await ctx.reply(embed=await utils.generate_member_info_card(ctx.conn, ctx.member))

@command(cmd="member new", usage="<name>", description="Adds a new member to your system.", category="Member commands")
async def new_member(ctx: MemberCommandContext, args: List[str]):
    if len(args) == 0:
        raise InvalidCommandSyntax()

    name = " ".join(args)
    bounds_error = utils.bounds_check_member_name(name, ctx.system.tag)
    if bounds_error:
        raise CommandError(bounds_error)

    # TODO: figure out what to do if this errors out on collision on generate_hid
    hid = utils.generate_hid()

    # Insert member row
    await db.create_member(ctx.conn, system_id=ctx.system.id, member_name=name, member_hid=hid)
    return "Member \"{}\" (`{}`) registered!".format(name, hid)


@member_command(cmd="member set", usage="<name|description|color|pronouns|birthdate|avatar> [value]", description="Edits a member property. Leave [value] blank to clear.", category="Member commands")
async def member_set(ctx: MemberCommandContext, args: List[str]):
    if len(args) == 0: 
        raise InvalidCommandSyntax()

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
        raise CommandError("Unknown property {}. Allowed properties are {}.".format(prop, ", ".join(allowed_properties)))

    if len(args) >= 2:
        value = " ".join(args[1:])

        # Sanity/validity checks and type conversions
        if prop == "name":
            bounds_error = utils.bounds_check_member_name(value, ctx.system.tag)
            if bounds_error:
                raise CommandError(bounds_error)

        if prop == "color":
            match = re.fullmatch("#?([0-9A-Fa-f]{6})", value)
            if not match:
                raise CommandError("Color must be a valid hex color (eg. #ff0000)")

            value = match.group(1).lower()
        
        if prop == "birthdate":
            try:
                value = datetime.strptime(value, "%Y-%m-%d").date()
            except ValueError:
                try:
                    # Try again, adding 0001 as a placeholder year
                    # This is considered a "null year" and will be omitted from the info card
                    # Useful if you want your birthday to be displayed yearless.
                    value = datetime.strptime("0001-" + value, "%Y-%m-%d").date()
                except ValueError:
                    raise CommandError("Invalid date. Date must be in ISO-8601 format (eg. 1999-07-25).")

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
        # Can't clear member name
        if prop == "name":
            raise CommandError("Can't clear member name.")

        # Clear from DB
        value = None

    db_prop = db_properties[prop]
    await db.update_member_field(ctx.conn, member_id=ctx.member.id, field=db_prop, value=value)
    
    response = utils.make_default_embed("{} {}'s {}.".format("Updated" if value else "Cleared", ctx.member.name, prop))
    if prop == "avatar" and value:
        response.set_image(url=value)
    if prop == "color" and value:
        response.colour = int(value, 16)
    return response

@member_command(cmd="member proxy", usage="[example]", description="Updates a member's proxy settings. Needs an \"example\" proxied message containing the string \"text\" (eg. [text], |text|, etc).", category="Member commands")
async def member_proxy(ctx: MemberCommandContext, args: List[str]):
    if len(args) == 0:
        prefix, suffix = None, None
    else:
        # Sanity checking
        example = " ".join(args)
        if "text" not in example:
            raise CommandError("Example proxy message must contain the string 'text'.")

        if example.count("text") != 1:
            raise CommandError("Example proxy message must contain the string 'text' exactly once.")

        # Extract prefix and suffix
        prefix = example[:example.index("text")].strip()
        suffix = example[example.index("text")+4:].strip()
        logger.debug("Matched prefix '{}' and suffix '{}'".format(prefix, suffix))

        # DB stores empty strings as None, make that work
        if not prefix:
            prefix = None
        if not suffix:
            suffix = None

    async with ctx.conn.transaction():
        await db.update_member_field(ctx.conn, member_id=ctx.member.id, field="prefix", value=prefix)
        await db.update_member_field(ctx.conn, member_id=ctx.member.id, field="suffix", value=suffix)
        return "Proxy settings updated." if prefix or suffix else "Proxy settings cleared."

@member_command("member delete", description="Deletes a member from your system ***permanently***.", category="Member commands")
async def member_delete(ctx: MemberCommandContext, args: List[str]):
    await ctx.reply("Are you sure you want to delete {}? If so, reply to this message with the member's ID (`{}`).".format(ctx.member.name, ctx.member.hid))

    msg = await ctx.client.wait_for_message(author=ctx.message.author, channel=ctx.message.channel, timeout=60.0)
    if msg and msg.content == ctx.member.hid:
        await db.delete_member(ctx.conn, member_id=ctx.member.id)
        return "Member deleted."
    else:
        return "Member deletion cancelled."
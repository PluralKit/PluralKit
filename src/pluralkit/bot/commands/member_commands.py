import re
from datetime import datetime
from typing import List
from urllib.parse import urlparse

from pluralkit.bot.commands import *
from pluralkit.bot import help

logger = logging.getLogger("pluralkit.commands")


async def member_info(ctx: CommandContext):
    member = await ctx.pop_member(
        error=CommandError("You must pass a member name or ID.", help=help.lookup_member), system_only=False)
    await ctx.reply(embed=await utils.generate_member_info_card(ctx.conn, member))


async def new_member(ctx: CommandContext):
    system = await ctx.ensure_system()
    if not ctx.has_next():
        return CommandError("You must pass a name for the new member.", help=help.add_member)

    name = ctx.remaining()
    bounds_error = utils.bounds_check_member_name(name, system.tag)
    if bounds_error:
        return CommandError(bounds_error)

    # TODO: figure out what to do if this errors out on collision on generate_hid
    hid = utils.generate_hid()

    # Insert member row
    await db.create_member(ctx.conn, system_id=system.id, member_name=name, member_hid=hid)
    return CommandSuccess(
        "Member \"{}\" (`{}`) registered! To register their proxy tags, use `pk;member proxy`.".format(name, hid))


async def member_set(ctx: CommandContext):
    system = await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))
    prop = ctx.pop_str(CommandError("You must pass a property name to set.", help=help.edit_member))

    allowed_properties = ["name", "description", "color", "pronouns", "birthdate", "avatar"]
    db_properties = {
        "name": "name",
        "description": "description",
        "color": "color",
        "pronouns": "pronouns",
        "birthdate": "birthday",
        "avatar": "avatar_url"
    }

    if prop not in allowed_properties:
        return CommandError(
            "Unknown property {}. Allowed properties are {}.".format(prop, ", ".join(allowed_properties)),
            help=help.edit_member)

    if ctx.has_next():
        value = " ".join(ctx.remaining())

        # Sanity/validity checks and type conversions
        if prop == "name":
            bounds_error = utils.bounds_check_member_name(value, system.tag)
            if bounds_error:
                return CommandError(bounds_error)

        if prop == "color":
            match = re.fullmatch("#?([0-9A-Fa-f]{6})", value)
            if not match:
                return CommandError("Color must be a valid hex color (eg. #ff0000)")

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
                    return CommandError("Invalid date. Date must be in ISO-8601 format (eg. 1999-07-25).")

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
        # Can't clear member name
        if prop == "name":
            return CommandError("You can't clear the member name.")

        # Clear from DB
        value = None

    db_prop = db_properties[prop]
    await db.update_member_field(ctx.conn, member_id=member.id, field=db_prop, value=value)

    response = CommandSuccess("{} {}'s {}.".format("Updated" if value else "Cleared", member.name, prop))
    if prop == "avatar" and value:
        response.set_image(url=value)
    if prop == "color" and value:
        response.colour = int(value, 16)
    return response


async def member_proxy(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.member_proxy))

    if not ctx.has_next():
        prefix, suffix = None, None
    else:
        # Sanity checking
        example = ctx.remaining()
        if "text" not in example:
            return CommandError("Example proxy message must contain the string 'text'.", help=help.member_proxy)

        if example.count("text") != 1:
            return CommandError("Example proxy message must contain the string 'text' exactly once.",
                                help=help.member_proxy)

        # Extract prefix and suffix
        prefix = example[:example.index("text")].strip()
        suffix = example[example.index("text") + 4:].strip()
        logger.debug("Matched prefix '{}' and suffix '{}'".format(prefix, suffix))

        # DB stores empty strings as None, make that work
        if not prefix:
            prefix = None
        if not suffix:
            suffix = None

    async with ctx.conn.transaction():
        await db.update_member_field(ctx.conn, member_id=member.id, field="prefix", value=prefix)
        await db.update_member_field(ctx.conn, member_id=member.id, field="suffix", value=suffix)
        return CommandSuccess("Proxy settings updated." if prefix or suffix else "Proxy settings cleared.")


async def member_delete(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))

    await ctx.reply(
        "Are you sure you want to delete {}? If so, reply to this message with the member's ID (`{}`).".format(
            member.name, member.hid))

    msg = await ctx.client.wait_for_message(author=ctx.message.author, channel=ctx.message.channel, timeout=60.0)
    if msg and msg.content.lower() == member.hid.lower():
        await db.delete_member(ctx.conn, member_id=member.id)
        return CommandSuccess("Member deleted.")
    else:
        return CommandError("Member deletion cancelled.")

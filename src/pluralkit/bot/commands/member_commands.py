from datetime import datetime

import pluralkit.bot.embeds
from pluralkit.bot import help
from pluralkit.bot.commands import *
from pluralkit.errors import PluralKitError

logger = logging.getLogger("pluralkit.commands")


async def member_info(ctx: CommandContext):
    member = await ctx.pop_member(
        error=CommandError("You must pass a member name or ID.", help=help.lookup_member), system_only=False)
    await ctx.reply(embed=await pluralkit.bot.embeds.member_card(ctx.conn, member))


async def new_member(ctx: CommandContext):
    system = await ctx.ensure_system()
    if not ctx.has_next():
        return CommandError("You must pass a name for the new member.", help=help.add_member)

    new_name = ctx.remaining()

    try:
        member = await system.create_member(ctx.conn, new_name)
    except PluralKitError as e:
        return CommandError(e.message)

    return CommandSuccess(
        "Member \"{}\" (`{}`) registered! To register their proxy tags, use `pk;member proxy`.".format(new_name, member.hid))


async def member_set(ctx: CommandContext):
    system = await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))

    property_name = ctx.pop_str(CommandError("You must pass a property name to set.", help=help.edit_member))

    async def name_setter(conn, new_name):
        if not new_name:
            raise CommandError("You can't clear the member name.")
        await member.set_name(conn, system, new_name)

    async def avatar_setter(conn, url):
        if url:
            user = await utils.parse_mention(ctx.client, url)
            if user:
                # Set the avatar to the mentioned user's avatar
                # Discord pushes webp by default, which isn't supported by webhooks, but also hosts png alternatives
                url = user.avatar_url.replace(".webp", ".png")

        await member.set_avatar(conn, url)

    async def birthdate_setter(conn, date_str):
        if date_str:
            try:
                date = datetime.strptime(date_str, "%Y-%m-%d").date()
            except ValueError:
                try:
                    # Try again, adding 0001 as a placeholder year
                    # This is considered a "null year" and will be omitted from the info card
                    # Useful if you want your birthday to be displayed yearless.
                    date = datetime.strptime("0001-" + date_str, "%Y-%m-%d").date()
                except ValueError:
                    raise CommandError("Invalid date. Date must be in ISO-8601 format (eg. 1999-07-25).")
        else:
            date = None

        await member.set_birthdate(conn, date)

    properties = {
        "name": name_setter,
        "description": member.set_description,
        "avatar": avatar_setter,
        "color": member.set_color,
        "pronouns": member.set_pronouns,
        "birthdate": birthdate_setter,
    }

    if property_name not in properties:
        return CommandError(
            "Unknown property {}. Allowed properties are {}.".format(property_name, ", ".join(properties.keys())),
            help=help.edit_system)

    value = ctx.remaining() or None

    try:
        await properties[property_name](ctx.conn, value)
    except PluralKitError as e:
        return CommandError(e.message)

    response = CommandSuccess("{} member {}.".format("Updated" if value else "Cleared", property_name))
    # if prop == "avatar" and value:
    #    response.set_image(url=value)
    # if prop == "color" and value:
    #    response.colour = int(value, 16)
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
        await member.set_proxy_tags(ctx.conn, prefix, suffix)
        return CommandSuccess("Proxy settings updated." if prefix or suffix else "Proxy settings cleared.")


async def member_delete(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.remove_member))

    delete_confirm_msg = "Are you sure you want to delete {}? If so, reply to this message with the member's ID (`{}`).".format(member.name, member.hid)
    if not await ctx.confirm_text(ctx.message.author, ctx.message.channel, member.hid, delete_confirm_msg):
        return CommandError("Member deletion cancelled.")

    await member.delete(ctx.conn)
    return CommandSuccess("Member deleted.")

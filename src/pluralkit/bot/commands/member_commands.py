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
        raise CommandError("You must pass a name for the new member.", help=help.add_member)

    new_name = ctx.remaining()

    existing_member = await Member.get_member_by_name(ctx.conn, system.id, new_name)
    if existing_member:
        msg = await ctx.reply_warn("There is already a member with this name, with the ID `{}`. Do you want to create a duplicate member anyway?".format(existing_member.hid))
        if not await ctx.confirm_react(ctx.message.author, msg):
            raise CommandError("Member creation cancelled.")

    try:
        member = await system.create_member(ctx.conn, new_name)
    except PluralKitError as e:
        raise CommandError(e.message)

    await ctx.reply_ok(
        "Member \"{}\" (`{}`) registered! To register their proxy tags, use `pk;member proxy`.".format(new_name,
                                                                                                       member.hid))


async def member_set(ctx: CommandContext):
    raise CommandError(
        "`pk;member set` has been retired. Please use the new member modifying commands: `pk;member [name|description|avatar|color|pronouns|birthdate]`.")


async def member_name(ctx: CommandContext):
    system = await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))
    new_name = ctx.pop_str(CommandError("You must pass a new member name.", help=help.edit_member))

    # Warn if there's a member by the same name already
    existing_member = await Member.get_member_by_name(ctx.conn, system.id, new_name)
    if existing_member:
        msg = await ctx.reply_warn("There is already a member with this name, with the ID `{}`. Do you want to rename this member anyway? This will result in two members with the same name.".format(existing_member.hid))
        if not await ctx.confirm_react(ctx.message.author, msg):
            raise CommandError("Member renaming cancelled.")

    await member.set_name(ctx.conn, new_name)
    await ctx.reply_ok("Member name updated.")

    if len(new_name) < 2 and not system.tag:
        await ctx.reply_warn("This member's new name is under 2 characters, and thus cannot be proxied. To prevent this, use a longer member name, or add a system tag.")
    elif len(new_name) > 32:
        exceeds_by = len(new_name) - 32
        await ctx.reply_warn("This member's new name is longer than 32 characters, and thus cannot be proxied. To prevent this, shorten the member name by {} characters.".format(exceeds_by))
    elif len(new_name) > system.get_member_name_limit():
        exceeds_by = len(new_name) - system.get_member_name_limit()
        await ctx.reply_warn("This member's new name, when combined with the system tag `{}`, is longer than 32 characters, and thus cannot be proxied. To prevent this, shorten the name or system tag by at least {} characters.".format(system.tag, exceeds_by))


async def member_description(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))
    new_description = ctx.remaining() or None

    await member.set_description(ctx.conn, new_description)
    await ctx.reply_ok("Member description {}.".format("updated" if new_description else "cleared"))


async def member_avatar(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))
    new_avatar_url = ctx.remaining() or None

    if new_avatar_url:
        user = await utils.parse_mention(ctx.client, new_avatar_url)
        if user:
            new_avatar_url = user.avatar_url_as(format="png")

    await member.set_avatar(ctx.conn, new_avatar_url)
    await ctx.reply_ok("Member avatar {}.".format("updated" if new_avatar_url else "cleared"))


async def member_color(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))
    new_color = ctx.remaining() or None

    await member.set_color(ctx.conn, new_color)
    await ctx.reply_ok("Member color {}.".format("updated" if new_color else "cleared"))


async def member_pronouns(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))
    new_pronouns = ctx.remaining() or None

    await member.set_pronouns(ctx.conn, new_pronouns)
    await ctx.reply_ok("Member pronouns {}.".format("updated" if new_pronouns else "cleared"))


async def member_birthdate(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.edit_member))
    new_birthdate = ctx.remaining() or None

    await member.set_birthdate(ctx.conn, new_birthdate)
    await ctx.reply_ok("Member birthdate {}.".format("updated" if new_birthdate else "cleared"))


async def member_proxy(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.member_proxy))

    if not ctx.has_next():
        prefix, suffix = None, None
    else:
        # Sanity checking
        example = ctx.remaining()
        if "text" not in example:
            raise CommandError("Example proxy message must contain the string 'text'.", help=help.member_proxy)

        if example.count("text") != 1:
            raise CommandError("Example proxy message must contain the string 'text' exactly once.",
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
        await ctx.reply_ok("Proxy settings updated." if prefix or suffix else "Proxy settings cleared.")


async def member_delete(ctx: CommandContext):
    await ctx.ensure_system()
    member = await ctx.pop_member(CommandError("You must pass a member name.", help=help.remove_member))

    delete_confirm_msg = "Are you sure you want to delete {}? If so, reply to this message with the member's ID (`{}`).".format(
        member.name, member.hid)
    if not await ctx.confirm_text(ctx.message.author, ctx.message.channel, member.hid, delete_confirm_msg):
        raise CommandError("Member deletion cancelled.")

    await member.delete(ctx.conn)
    await ctx.reply_ok("Member deleted.")

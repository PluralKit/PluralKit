import pluralkit.bot.embeds
from pluralkit.bot import help
from pluralkit.bot.commands import *
from pluralkit.errors import PluralKitError


async def member_root(ctx: CommandContext):
    if ctx.match("new") or ctx.match("create") or ctx.match("add") or ctx.match("register"):
        await new_member(ctx)
    elif ctx.match("help"):
        await ctx.reply(help.member_commands)
    elif ctx.match("set"):
        await member_set(ctx)
    # TODO "pk;member list"
    elif not ctx.has_next():
        raise CommandError("Must pass a subcommand. For a list of subcommands, type `pk;help member`.")
    else:
        await specific_member_root(ctx)


async def specific_member_root(ctx: CommandContext):
    member = await ctx.pop_member(system_only=False)

    if ctx.has_next():
        # Following commands operate on members only in the caller's own system
        # error if not, to make sure you can't destructively edit someone else's member
        system = await ctx.ensure_system()
        if not member.system == system.id:
            raise CommandError("Member must be in your own system.")

        if ctx.match("name") or ctx.match("rename"):
            await member_name(ctx, member)
        elif ctx.match("description"):
            await member_description(ctx, member)
        elif ctx.match("avatar") or ctx.match("icon"):
            await member_avatar(ctx, member)
        elif ctx.match("proxy") or ctx.match("tags"):
            await member_proxy(ctx, member)
        elif ctx.match("pronouns") or ctx.match("pronoun"):
            await member_pronouns(ctx, member)
        elif ctx.match("color") or ctx.match("colour"):
            await member_color(ctx, member)
        elif ctx.match("birthday") or ctx.match("birthdate"):
            await member_birthdate(ctx, member)
        elif ctx.match("delete") or ctx.match("remove") or ctx.match("destroy") or ctx.match("erase"):
            await member_delete(ctx, member)
        else:
            raise CommandError(
                "Unknown subcommand {}. For a list of all commands, type `pk;help member`".format(ctx.pop_str()))
    else:
        # Basic lookup
        await member_info(ctx, member)


async def member_info(ctx: CommandContext, member: Member):
    await ctx.reply(embed=await pluralkit.bot.embeds.member_card(ctx.conn, member))


async def new_member(ctx: CommandContext):
    system = await ctx.ensure_system()
    if not ctx.has_next():
        raise CommandError("You must pass a name for the new member.")

    new_name = ctx.remaining()

    existing_member = await Member.get_member_by_name(ctx.conn, system.id, new_name)
    if existing_member:
        msg = await ctx.reply_warn(
            "There is already a member with this name, with the ID `{}`. Do you want to create a duplicate member anyway?".format(
                existing_member.hid))
        if not await ctx.confirm_react(ctx.message.author, msg):
            raise CommandError("Member creation cancelled.")

    try:
        member = await system.create_member(ctx.conn, new_name)
    except PluralKitError as e:
        raise CommandError(e.message)

    await ctx.reply_ok(
        "Member \"{}\" (`{}`) registered! Type `pk;help member` for a list of commands to edit this member.".format(new_name, member.hid))


async def member_set(ctx: CommandContext):
    raise CommandError(
        "`pk;member set` has been retired. Please use the new member modifying commands. Type `pk;help member` for a list.")


async def member_name(ctx: CommandContext, member: Member):
    system = await ctx.ensure_system()
    new_name = ctx.pop_str(CommandError("You must pass a new member name."))

    # Warn if there's a member by the same name already
    existing_member = await Member.get_member_by_name(ctx.conn, system.id, new_name)
    if existing_member and existing_member.id != member.id:
        msg = await ctx.reply_warn(
            "There is already another member with this name, with the ID `{}`. Do you want to rename this member anyway? This will result in two members with the same name.".format(
                existing_member.hid))
        if not await ctx.confirm_react(ctx.message.author, msg):
            raise CommandError("Member renaming cancelled.")

    await member.set_name(ctx.conn, new_name)
    await ctx.reply_ok("Member name updated.")

    if len(new_name) < 2 and not system.tag:
        await ctx.reply_warn(
            "This member's new name is under 2 characters, and thus cannot be proxied. To prevent this, use a longer member name, or add a system tag.")
    elif len(new_name) > 32:
        exceeds_by = len(new_name) - 32
        await ctx.reply_warn(
            "This member's new name is longer than 32 characters, and thus cannot be proxied. To prevent this, shorten the member name by {} characters.".format(
                exceeds_by))
    elif len(new_name) > system.get_member_name_limit():
        exceeds_by = len(new_name) - system.get_member_name_limit()
        await ctx.reply_warn(
            "This member's new name, when combined with the system tag `{}`, is longer than 32 characters, and thus cannot be proxied. To prevent this, shorten the name or system tag by at least {} characters.".format(
                system.tag, exceeds_by))


async def member_description(ctx: CommandContext, member: Member):
    new_description = ctx.remaining() or None

    await member.set_description(ctx.conn, new_description)
    await ctx.reply_ok("Member description {}.".format("updated" if new_description else "cleared"))


async def member_avatar(ctx: CommandContext, member: Member):
    new_avatar_url = ctx.remaining() or None

    if new_avatar_url:
        user = await utils.parse_mention(ctx.client, new_avatar_url)
        if user:
            new_avatar_url = user.avatar_url_as(format="png")

    await member.set_avatar(ctx.conn, new_avatar_url)
    await ctx.reply_ok("Member avatar {}.".format("updated" if new_avatar_url else "cleared"))


async def member_color(ctx: CommandContext, member: Member):
    new_color = ctx.remaining() or None

    await member.set_color(ctx.conn, new_color)
    await ctx.reply_ok("Member color {}.".format("updated" if new_color else "cleared"))


async def member_pronouns(ctx: CommandContext, member: Member):
    new_pronouns = ctx.remaining() or None

    await member.set_pronouns(ctx.conn, new_pronouns)
    await ctx.reply_ok("Member pronouns {}.".format("updated" if new_pronouns else "cleared"))


async def member_birthdate(ctx: CommandContext, member: Member):
    new_birthdate = ctx.remaining() or None

    await member.set_birthdate(ctx.conn, new_birthdate)
    await ctx.reply_ok("Member birthdate {}.".format("updated" if new_birthdate else "cleared"))


async def member_proxy(ctx: CommandContext, member: Member):
    if not ctx.has_next():
        prefix, suffix = None, None
    else:
        # Sanity checking
        example = ctx.remaining()
        if "text" not in example:
            raise CommandError("Example proxy message must contain the string 'text'. For help, type `pk;help proxy`.")

        if example.count("text") != 1:
            raise CommandError("Example proxy message must contain the string 'text' exactly once. For help, type `pk;help proxy`.")

        # Extract prefix and suffix
        prefix = example[:example.index("text")].strip()
        suffix = example[example.index("text") + 4:].strip()

        # DB stores empty strings as None, make that work
        if not prefix:
            prefix = None
        if not suffix:
            suffix = None

    async with ctx.conn.transaction():
        await member.set_proxy_tags(ctx.conn, prefix, suffix)
    await ctx.reply_ok(
        "Proxy settings updated." if prefix or suffix else "Proxy settings cleared. If you meant to set your proxy tags, type `pk;help proxy` for help.")


async def member_delete(ctx: CommandContext, member: Member):
    delete_confirm_msg = "Are you sure you want to delete {}? If so, reply to this message with the member's ID (`{}`).".format(
        member.name, member.hid)
    if not await ctx.confirm_text(ctx.message.author, ctx.message.channel, member.hid, delete_confirm_msg):
        raise CommandError("Member deletion cancelled.")

    await member.delete(ctx.conn)
    await ctx.reply_ok("Member deleted.")

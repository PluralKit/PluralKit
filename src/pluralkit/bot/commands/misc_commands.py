import io
import json
import os
from discord.utils import oauth_url

from pluralkit.bot import help
from pluralkit.bot.commands import *
from pluralkit.bot.embeds import help_footer_embed

prefix = "pk;" # TODO: configurable

def make_footer_embed():
    embed = discord.Embed()
    embed.set_footer(text=help.helpfile["footer"])
    return embed

def make_command_embed(command):
    embed = make_footer_embed()
    embed.title = prefix + command["usage"]
    embed.description = (command["description"] + "\n" + command.get("longdesc", "")).strip()
    if "aliases" in command:
        embed.add_field(name="Aliases" if len(command["aliases"]) > 1 else "Alias", value="\n".join([prefix + cmd for cmd in command["aliases"]]), inline=False)
    embed.add_field(name="Usage", value=prefix + command["usage"], inline=False)
    if "examples" in command:
        embed.add_field(name="Examples" if len(command["examples"]) > 1 else "Example", value="\n".join([prefix + cmd for cmd in command["examples"]]), inline=False)
    if "subcommands" in command:
        embed.add_field(name="Subcommands", value="\n".join([command["name"] + " " + sc["name"] for sc in command["subcommands"]]), inline=False)
    return embed

def find_command(command_list, name):
    for command in command_list:
        if command["name"].lower().strip() == name.lower().strip():
            return command

async def help_root(ctx: CommandContext):
    for page_name, page_content in help.helpfile["pages"].items():
        if ctx.match(page_name):
            return await help_page(ctx, page_content)

    if not ctx.has_next():
        return await help_page(ctx, help.helpfile["pages"]["root"])

    return await help_command(ctx, ctx.remaining())

async def help_page(ctx, sections):
    msg = ""
    for section in sections:
        msg += "__**{}**__\n{}\n\n".format(section["name"], section["content"])

    return await ctx.reply(content=msg, embed=make_footer_embed())

async def help_command(ctx, command_name):
    name_parts = command_name.replace(prefix, "").split(" ")
    command = find_command(help.helpfile["commands"], name_parts[0])
    name_parts = name_parts[1:]
    if not command:
        raise CommandError("Could not find command '{}'.".format(command_name))
    while len(name_parts) > 0:
        found_command = find_command(command["subcommands"], name_parts[0])
        if not found_command:
            break
        command = found_command
        name_parts = name_parts[1:]

    return await ctx.reply(embed=make_command_embed(command))

async def command_list(ctx):
    cmds = []

    categories = {}
    def make_command_list(lst):
        for cmd in lst:
            if not cmd["category"] in categories:
                categories[cmd["category"]] = []
            categories[cmd["category"]].append("**{}{}** - {}".format(prefix, cmd["usage"], cmd["description"]))
            if "subcommands" in cmd:
                make_command_list(cmd["subcommands"])
    make_command_list(help.helpfile["commands"])

    embed = discord.Embed()
    embed.title = "PluralKit Commands"
    embed.description = "Type `pk;help <command>` for more information."
    for cat_name, cat_cmds in categories.items():
        embed.add_field(name=cat_name, value="\n".join(cat_cmds))
    await ctx.reply(embed=embed)


async def invite_link(ctx: CommandContext):
    client_id = (await ctx.client.application_info()).id

    permissions = discord.Permissions()

    # So the bot can actually add the webhooks it needs to do the proxy functionality
    permissions.manage_webhooks = True

    # So the bot can respond with status, error, and success messages
    permissions.send_messages = True

    # So the bot can delete channels
    permissions.manage_messages = True

    # So the bot can respond with extended embeds, ex. member cards
    permissions.embed_links = True

    # So the bot can send images too
    permissions.attach_files = True

    # (unsure if it needs this, actually, might be necessary for message lookup)
    permissions.read_message_history = True

    # So the bot can add reactions for confirm/deny prompts
    permissions.add_reactions = True

    url = oauth_url(client_id, permissions)
    await ctx.reply_ok("Use this link to add PluralKit to your server: {}".format(url))


async def export(ctx: CommandContext):
    working_msg = await ctx.message.channel.send("Working...")

    system = await ctx.ensure_system()

    members = await system.get_members(ctx.conn)
    accounts = await system.get_linked_account_ids(ctx.conn)
    switches = await system.get_switches(ctx.conn, 999999)

    data = {
        "name": system.name,
        "id": system.hid,
        "description": system.description,
        "tag": system.tag,
        "avatar_url": system.avatar_url,
        "created": system.created.isoformat(),
        "members": [
            {
                "name": member.name,
                "id": member.hid,
                "color": member.color,
                "avatar_url": member.avatar_url,
                "birthday": member.birthday.isoformat() if member.birthday else None,
                "pronouns": member.pronouns,
                "description": member.description,
                "prefix": member.prefix,
                "suffix": member.suffix,
                "created": member.created.isoformat(),
                "message_count": await member.message_count(ctx.conn)
            } for member in members
        ],
        "accounts": [str(uid) for uid in accounts],
        "switches": [
            {
                "timestamp": switch.timestamp.isoformat(),
                "members": [member.hid for member in await switch.fetch_members(ctx.conn)]
            } for switch in switches
        ]  # TODO: messages
    }

    await working_msg.delete()

    try:
        # Try sending the file to the user in a DM first
        f = io.BytesIO(json.dumps(data).encode("utf-8"))
        await ctx.message.author.send(content="Here you go! \u2709", file=discord.File(fp=f, filename="pluralkit_system.json"))
        if not isinstance(ctx.message.channel, discord.DMChannel):
            await ctx.reply_ok("DM'd!")
    except discord.Forbidden:
        # If that fails, warn the user and ask whether the want the file posted in the channel
        fallback_msg = await ctx.reply_warn("I'm not allowed to DM you! Do you want me to post the exported data here instead? I can delete the file after you save it if you want.")
        # Use reactions to get their response
        if not await ctx.confirm_react(ctx.message.author, fallback_msg):
            raise CommandError("Export cancelled.")
        f = io.BytesIO(json.dumps(data).encode("utf-8"))
        # If they reacted with ✅, post the file in the channel and give them the option to delete it
        fallback_data = await ctx.message.channel.send(content="Here you go! \u2709\nReact with \u2705 if you want me to delete the file.", file=discord.File(fp=f, filename="pluralkit_system.json"))
        if not await ctx.confirm_react(ctx.message.author, fallback_data):
            await fallback_data.add_reaction("👌")
            return
        await fallback_data.delete()
        await ctx.reply_ok("Export file deleted.")


async def tell(ctx: CommandContext):
    # Dev command only
    # This is used to tell members of servers I'm not in when something is broken so they can contact me with debug info
    if ctx.message.author.id != 102083498529026048:
        # Just silently fail, not really a public use command
        return

    channel = ctx.pop_str()
    message = ctx.remaining()

    # lol error handling
    await ctx.client.get_channel(int(channel)).send(content="[dev message] " + message)
    await ctx.reply_ok("Sent!")


# Easter eggs lmao because why not
async def pkfire(ctx: CommandContext):
    await ctx.message.channel.send("*A giant lightning bolt promptly erupts into a pillar of fire as it hits your opponent.*")

async def pkthunder(ctx: CommandContext):
    await ctx.message.channel.send("*A giant ball of lightning is conjured and fired directly at your opponent, vanquishing them.*")

async def pkfreeze(ctx: CommandContext):
    await ctx.message.channel.send("*A giant crystal ball of ice is charged and hurled toward your opponent, bursting open and freezing them solid on contact.*")

async def pkstarstorm(ctx: CommandContext):
    await ctx.message.channel.send("*Vibrant colours burst forth from the sky as meteors rain down upon your opponent.*")

async def pkmn(ctx: CommandContext):
    await ctx.message.channel.send("Gotta catch 'em all!")
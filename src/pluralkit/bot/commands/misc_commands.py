import io
import json
import os
from discord.utils import oauth_url

from pluralkit.bot import help
from pluralkit.bot.commands import *
from pluralkit.bot.embeds import help_footer_embed


async def help_root(ctx: CommandContext):
    if ctx.match("commands"):
        await ctx.reply(help.all_commands, embed=help_footer_embed())
    elif ctx.match("proxy"):
        await ctx.reply(help.proxy_guide, embed=help_footer_embed())
    elif ctx.match("system"):
        await ctx.reply(help.system_commands, embed=help_footer_embed())
    elif ctx.match("member"):
        await ctx.reply(help.member_commands + "\n\n" + help.command_notes, embed=help_footer_embed())
    else:
        await ctx.reply(help.root, embed=help_footer_embed())


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

    f = io.BytesIO(json.dumps(data).encode("utf-8"))
    await ctx.reply_ok("DM'd!")
    await ctx.message.author.send(content="Here you go!", file=discord.File(fp=f, filename="pluralkit_system.json"))


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

import io
import json
import logging
import os
from typing import List

from discord.utils import oauth_url

from pluralkit.bot import utils
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")

@command(cmd="help", usage="[system|member|proxy|switch|mod]", description="Shows help messages.")
async def show_help(ctx: CommandContext, args: List[str]):
    embed = utils.make_default_embed("")
    embed.title = "PluralKit Help"
    embed.set_footer(text="By Astrid (Ske#6201, or 'qoxvy' on PK) | GitHub: https://github.com/xSke/PluralKit/")

    category = args[0] if len(args) > 0 else None

    from pluralkit.bot.help import help_pages
    if category in help_pages:
        for name, text in help_pages[category]:
            if name:
                embed.add_field(name=name, value=text)
            else:
                embed.description = text
    else:
        raise InvalidCommandSyntax()

    return embed

@command(cmd="invite", description="Generates an invite link for this bot.")
async def invite_link(ctx: CommandContext, args: List[str]):
    client_id = os.environ["CLIENT_ID"]

    permissions = discord.Permissions()
    permissions.manage_webhooks = True
    permissions.send_messages = True
    permissions.manage_messages = True
    permissions.embed_links = True
    permissions.attach_files = True
    permissions.read_message_history = True
    permissions.add_reactions = True

    url = oauth_url(client_id, permissions)
    logger.debug("Sending invite URL: {}".format(url))
    return url

@command(cmd="export", description="Exports system data to a machine-readable format.")
async def export(ctx: CommandContext, args: List[str]):
    members = await db.get_all_members(ctx.conn, ctx.system.id)
    accounts = await db.get_linked_accounts(ctx.conn, ctx.system.id)
    switches = await utils.get_front_history(ctx.conn, ctx.system.id, 999999)

    system = ctx.system
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
                "created": member.created.isoformat()
            } for member in members
        ],
        "accounts": [str(uid) for uid in accounts],
        "switches": [
            {
                "timestamp": timestamp.isoformat(),
                "members": [member.hid for member in members]
            } for timestamp, members in switches
        ]
    }

    f = io.BytesIO(json.dumps(data).encode("utf-8"))
    await ctx.client.send_file(ctx.message.channel, f, filename="system.json")
import logging
from typing import List

from pluralkit.bot import utils, embeds
from pluralkit.bot.commands import *

logger = logging.getLogger("pluralkit.commands")

@command(cmd="mod log", usage="[channel]", description="Sets the bot to log events to a specified channel. Leave blank to disable.", category="Moderation commands", system_required=False)
async def set_log(ctx: CommandContext, args: List[str]):
    if not ctx.message.author.server_permissions.administrator:
        return embeds.error("You must be a server administrator to use this command.")
    
    server = ctx.message.server
    if len(args) == 0:
        channel_id = None
    else:
        channel = utils.parse_channel_mention(args[0], server=server)
        if not channel:
            return embeds.error("Channel not found.")
        channel_id = channel.id

    await db.update_server(ctx.conn, server.id, logging_channel_id=channel_id)
    return embeds.success("Updated logging channel." if channel_id else "Cleared logging channel.")

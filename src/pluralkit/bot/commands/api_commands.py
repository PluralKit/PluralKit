import logging
from discord import DMChannel

from pluralkit.bot.commands import CommandContext, CommandSuccess

logger = logging.getLogger("pluralkit.commands")
disclaimer = "Please note that this grants access to modify (and delete!) all your system data, so keep it safe and secure. If it leaks or you need a new one, you can invalidate this one with `pk;token refresh`."

async def reply_dm(ctx: CommandContext, message: str):
    await ctx.message.author.send(message)

    if not isinstance(ctx.message.channel, DMChannel):
        return CommandSuccess("DM'd!")

async def get_token(ctx: CommandContext):
    system = await ctx.ensure_system()

    if system.token:
        token = system.token
    else:
        token = await system.refresh_token(ctx.conn)

    token_message = "Here's your API token: \n**`{}`**\n{}".format(token, disclaimer)
    return await reply_dm(ctx, token_message)

async def refresh_token(ctx: CommandContext):
    system = await ctx.ensure_system()

    token = await system.refresh_token(ctx.conn)
    token_message = "Your previous API token has been invalidated. You will need to change it anywhere it's currently used.\nHere's your new API token:\n**`{}`**\n{}".format(token, disclaimer)
    return await reply_dm(ctx, token_message)